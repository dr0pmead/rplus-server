using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RPlus.Hunter.API.Persistence;

namespace RPlus.Hunter.API.Services;

/// <summary>
/// The "Brain" of the AI recruiter.
/// Implements RAG (Retrieval Augmented Generation) + MCP Tools + DeepSeek R1 thought cleaning.
///
/// Architecture:
///   1. RAG: Embed user question → cosine search company_knowledge → inject facts into prompt
///   2. MCP Tools: Function calling for structured data extraction (salary, stack, experience)
///   3. Thought Cleaning: Strip DeepSeek R1 internal monologue (&lt;think&gt; tags) from response
///
/// Host: External GPU server (ai.rubikom.kz, RTX 5090)
/// Models: deepseek-r1:32b (reasoning), nomic-embed-text (embeddings)
/// </summary>
public sealed partial class AiBrainService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IDbContextFactory<HunterDbContext> _dbFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AiBrainService> _logger;

    public AiBrainService(
        IHttpClientFactory httpFactory,
        IDbContextFactory<HunterDbContext> dbFactory,
        IConfiguration config,
        ILogger<AiBrainService> logger)
    {
        _httpFactory = httpFactory;
        _dbFactory = dbFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Generates an AI response using RAG-augmented context and DeepSeek R1.
    /// </summary>
    /// <param name="profileId">Candidate profile ID (for logging).</param>
    /// <param name="conversationMessages">Full conversation history (system + user/assistant).</param>
    /// <returns>Clean AI response text (thought tags stripped).</returns>
    public async Task<string?> GenerateResponseAsync(
        Guid profileId,
        List<ConversationMessage> conversationMessages,
        CancellationToken ct = default)
    {
        var client = _httpFactory.CreateClient("RPlus.AI");
        var model = _config["AI:Model"] ?? "deepseek-r1:32b";
        var embedModel = _config["AI:EmbeddingModel"] ?? "nomic-embed-text";
        var contextSize = _config.GetValue("AI:ContextSize", 32768);
        var temperature = _config.GetValue("AI:Temperature", 0.6);
        var ragTopK = _config.GetValue("AI:RagTopK", 3);

        // Extract latest user message for RAG query
        var latestUserMessage = conversationMessages
            .LastOrDefault(m => m.Role == "user")?.Content ?? "";

        // ── Step 1: RAG — Retrieval Augmented Generation ────────────────────
        var ragContext = await RetrieveRagContextAsync(
            client, embedModel, latestUserMessage, ragTopK, ct);

        // ── Step 2: Build System Prompt with RAG facts ──────────────────────
        var systemPrompt = BuildSystemPrompt(ragContext);

        // Replace system prompt in conversation if present, or prepend
        var messages = new List<object>();
        var hasSystem = false;

        foreach (var msg in conversationMessages)
        {
            if (msg.Role == "system")
            {
                messages.Add(new { role = "system", content = systemPrompt });
                hasSystem = true;
            }
            else
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        if (!hasSystem)
            messages.Insert(0, new { role = "system", content = systemPrompt });

        // NOTE: DeepSeek R1 does NOT support native tool/function calling.
        // MCP tools (save_candidate_fact) will be implemented via prompt engineering in v2.

        // ── Step 3: AI Request ──────────────────────────────────────────────
        var payload = new
        {
            model,
            messages,
            stream = false,
            options = new { num_ctx = contextSize, temperature }
        };

        _logger.LogInformation(
            "Calling DeepSeek R1 for profile {ProfileId}, model={Model}, context={Context}",
            profileId, model, contextSize);

        var response = await client.PostAsJsonAsync("/api/chat", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("AI Brain returned {StatusCode}: {Body}",
                response.StatusCode, errorBody[..Math.Min(errorBody.Length, 200)]);
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var content = json.GetProperty("message").GetProperty("content").GetString() ?? "";

        // ── Step 4: Thought Cleaning (DeepSeek R1 Specific) ─────────────────
        var cleanResponse = CleanDeepSeekThoughts(content).Trim();

        _logger.LogInformation(
            "AI Brain response for profile {ProfileId}: {Response}",
            profileId, cleanResponse[..Math.Min(cleanResponse.Length, 100)]);

        return string.IsNullOrWhiteSpace(cleanResponse) ? null : cleanResponse;
    }

    // ─── RAG Retrieval ──────────────────────────────────────────────────────

    /// <summary>
    /// Embeds the user question and searches company_knowledge for relevant facts.
    /// Returns formatted context string or empty if no matches / RAG fails.
    /// </summary>
    private async Task<string> RetrieveRagContextAsync(
        HttpClient client,
        string embedModel,
        string query,
        int topK,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "";

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            // Check if we have any knowledge entries at all
            var hasKnowledge = await db.CompanyKnowledge.AnyAsync(ct);
            if (!hasKnowledge)
            {
                _logger.LogDebug("No company knowledge entries — RAG skipped");
                return "";
            }

            // Generate query embedding
            var vectorPayload = new { model = embedModel, prompt = query };
            var embedRes = await client.PostAsJsonAsync("/api/embeddings", vectorPayload, ct);

            if (!embedRes.IsSuccessStatusCode)
            {
                _logger.LogWarning("Embedding request failed: {Status}", embedRes.StatusCode);
                return "";
            }

            var embedJson = await embedRes.Content.ReadFromJsonAsync<JsonElement>(ct);
            var vectorData = embedJson.GetProperty("embedding").EnumerateArray()
                .Select(x => x.GetSingle()).ToArray();
            var vector = new Vector(vectorData);

            // Cosine similarity search — closest facts first
            var facts = await db.CompanyKnowledge
                .Where(k => k.Embedding != null)
                .OrderBy(k => k.Embedding!.CosineDistance(vector))
                .Take(topK)
                .Select(k => k.Content)
                .ToListAsync(ct);

            if (facts.Count == 0)
                return "";

            _logger.LogInformation("RAG retrieved {Count} facts for query: {Query}",
                facts.Count, query[..Math.Min(query.Length, 50)]);

            return string.Join("\n- ", facts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG retrieval failed, proceeding without context");
            return "";
        }
    }

    // ─── System Prompt ──────────────────────────────────────────────────────

    private static string BuildSystemPrompt(string ragContext)
    {
        var knowledgeSection = string.IsNullOrEmpty(ragContext)
            ? "Нет доступных фактов в базе знаний. Если не знаешь ответ — скажи, что уточнишь у коллег."
            : ragContext;

        return $"""
            ТЫ: Александр, Senior Talent Acquisition Partner компании RPlus.
            ЦЕЛЬ: Интервью с кандидатом в WhatsApp.
            
            🧠 БАЗА ЗНАНИЙ (ФАКТЫ О КОМПАНИИ):
            {knowledgeSection}

            ⚠️ ИНСТРУКЦИИ:
            1. Ты — человек. Официально-деловой стиль. Обращение строго на "Вы".
            2. Отвечай кратко (1-3 предложения). Кандидат читает с телефона.
            3. Если в Базе Знаний нет ответа — скажи, что уточните у коллег.
            4. Не используй Markdown (жирный шрифт, списки, заголовки).
            5. Отвечай на том же языке, на котором пишет кандидат.
            6. Если кандидат проявляет интерес — предложи удобное время для собеседования.
            7. Если кандидат отказывается — поблагодари за уделённое время.
            """;
    }

    // ─── MCP Tools ──────────────────────────────────────────────────────────

    private static object[] BuildMcpTools()
    {
        return
        [
            new
            {
                type = "function",
                function = new
                {
                    name = "save_candidate_fact",
                    description = "Сохранить важный факт о кандидате (зарплатные ожидания, стек, опыт)",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            category = new
                            {
                                type = "string",
                                @enum = new[] { "salary", "stack", "experience", "availability", "location" }
                            },
                            value = new { type = "string", description = "Значение факта" }
                        },
                        required = new[] { "category", "value" }
                    }
                }
            }
        ];
    }

    /// <summary>
    /// Processes tool calls from the AI response (e.g., save_candidate_fact).
    /// </summary>
    private async Task ProcessToolCallsAsync(
        Guid profileId, JsonElement toolCalls, CancellationToken ct)
    {
        try
        {
            foreach (var call in toolCalls.EnumerateArray())
            {
                var funcName = call.GetProperty("function").GetProperty("name").GetString();
                var argsJson = call.GetProperty("function").GetProperty("arguments").GetString();

                if (funcName == "save_candidate_fact" && argsJson is not null)
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(argsJson);
                    var category = args.GetProperty("category").GetString() ?? "unknown";
                    var value = args.GetProperty("value").GetString() ?? "";

                    _logger.LogInformation(
                        "MCP Tool: save_candidate_fact for profile {ProfileId}: {Category}={Value}",
                        profileId, category, value[..Math.Min(value.Length, 80)]);

                    // TODO: Persist to candidate_facts table when schema is ready
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tool call processing failed for profile {ProfileId}", profileId);
        }
    }

    // ─── DeepSeek R1 Thought Cleaning ───────────────────────────────────────

    /// <summary>
    /// Strips DeepSeek R1 internal monologue tags from response.
    /// DeepSeek R1 wraps its reasoning in &lt;think&gt;...&lt;/think&gt; blocks.
    /// Candidates must never see the AI's internal thoughts.
    /// </summary>
    private static string CleanDeepSeekThoughts(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return ThinkTagRegex().Replace(input, "");
    }

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ThinkTagRegex();
}

/// <summary>
/// Simple conversation message DTO for AiBrainService.
/// </summary>
public sealed record ConversationMessage(string Role, string Content);
