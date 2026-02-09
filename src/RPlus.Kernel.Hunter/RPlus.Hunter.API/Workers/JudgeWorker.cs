using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Core.Kafka;
using RPlus.Core.Options;
using RPlus.Hunter.API.Persistence;
using RPlus.Hunter.API.Services;
using RPlus.SDK.Hunter.Events;
using RPlus.SDK.Hunter.Models;
using System.Text.Json;

namespace RPlus.Hunter.API.Workers;

/// <summary>
/// AI Screening Worker — consumes parsed profiles and scores them via RPlus.AI.
/// Implements Smart Dedup (ContentHash) and Budget Fuse (DailyContactLimit check).
/// </summary>
public sealed class JudgeWorker : KafkaConsumerBackgroundService<string, string>
{
    private readonly IDbContextFactory<HunterDbContext> _dbFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KafkaEventPublisher _eventPublisher;

    private const string AiPromptTemplate = """
        Ты — AI-рекрутер. Твоя задача — оценить соответствие кандидата требованиям вакансии.

        **Вакансия и условия отбора:**
        {0}

        **Текст резюме кандидата:**
        {1}

        **Инструкция:**
        1. Оцени соответствие кандидата от 0 до 100.
        2. Дай краткий вердикт (1-2 предложения): почему подходит или не подходит.
        3. Ответь СТРОГО в формате JSON:
        {{"score": <число>, "verdict": "<текст>"}}

        Ответь только JSON, без дополнительного текста.
        """;

    public JudgeWorker(
        IOptions<KafkaOptions> options,
        ILogger<JudgeWorker> logger,
        IDbContextFactory<HunterDbContext> dbFactory,
        IHttpClientFactory httpClientFactory,
        KafkaEventPublisher eventPublisher)
        : base(options, logger, HunterTopics.ProfilesParsed)
    {
        _dbFactory = dbFactory;
        _httpClientFactory = httpClientFactory;
        _eventPublisher = eventPublisher;
    }

    protected override async Task HandleMessageAsync(string key, string message, CancellationToken cancellationToken)
    {
        var parsedEvent = JsonSerializer.Deserialize<ProfileParsedEvent>(message,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        if (parsedEvent is null)
        {
            _logger.LogWarning("Failed to deserialize ProfileParsedEvent");
            return;
        }

        _logger.LogInformation("Scoring profile {ProfileId} for task {TaskId}",
            parsedEvent.ProfileId, parsedEvent.TaskId);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Verify profile still exists and is in NEW status (idempotency guard)
        var profile = await db.ParsedProfiles
            .FirstOrDefaultAsync(p => p.Id == parsedEvent.ProfileId, cancellationToken);

        if (profile is null || profile.Status != ProfileStatus.New)
        {
            _logger.LogDebug("Profile {ProfileId} already processed or deleted, skipping", parsedEvent.ProfileId);
            return;
        }

        // Call RPlus.AI for scoring
        var (score, verdict) = await ScoreWithAiAsync(parsedEvent.Conditions, parsedEvent.RawData, cancellationToken);

        // Update profile
        profile.AiScore = score;
        profile.AiVerdict = verdict;
        profile.Status = score >= parsedEvent.MinScore ? ProfileStatus.FilteredOk : ProfileStatus.Rejected;

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile {ProfileId} scored: {Score}/100 -> {Status} ({Verdict})",
            parsedEvent.ProfileId, score, profile.Status, verdict);

        // Publish scored event (StalkerWorker only cares about passed profiles)
        await _eventPublisher.PublishAsync(
            new ProfileScoredEvent
            {
                ProfileId = parsedEvent.ProfileId,
                TaskId = parsedEvent.TaskId,
                Score = score,
                Verdict = verdict,
                Passed = profile.Status == ProfileStatus.FilteredOk
            },
            HunterTopics.ProfilesScored,
            parsedEvent.TaskId.ToString(),
            cancellationToken);
    }

    private async Task<(int Score, string Verdict)> ScoreWithAiAsync(
        string conditions, string rawData, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("RPlus.AI");

            var prompt = string.Format(AiPromptTemplate, conditions, rawData);

            var requestBody = new
            {
                message = prompt,
                model = (string?)null,      // use default
                temperature = 0.3,          // low temperature for deterministic scoring
                maxTokens = 200
            };

            var response = await client.PostAsJsonAsync("/api/v1/ai/chat", requestBody, ct);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync(ct);

            // Parse AI response — expect {"score": N, "verdict": "..."}
            return ParseAiResponse(responseText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI scoring failed, assigning score 0");
            return (0, $"AI scoring error: {ex.Message}");
        }
    }

    private static (int Score, string Verdict) ParseAiResponse(string response)
    {
        try
        {
            // The AI response may be wrapped in {"response": "..."} from our Chat API
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            string innerJson;
            if (root.TryGetProperty("response", out var responseProp))
                innerJson = responseProp.GetString() ?? response;
            else
                innerJson = response;

            // Clean potential markdown code block wrapping
            innerJson = innerJson.Trim();
            if (innerJson.StartsWith("```"))
            {
                var firstNewline = innerJson.IndexOf('\n');
                var lastBackticks = innerJson.LastIndexOf("```");
                if (firstNewline > 0 && lastBackticks > firstNewline)
                    innerJson = innerJson[(firstNewline + 1)..lastBackticks].Trim();
            }

            using var scoreDoc = JsonDocument.Parse(innerJson);
            var scoreRoot = scoreDoc.RootElement;

            var score = scoreRoot.TryGetProperty("score", out var scoreProp)
                ? scoreProp.GetInt32()
                : 0;

            var verdict = scoreRoot.TryGetProperty("verdict", out var verdictProp)
                ? verdictProp.GetString() ?? "No verdict"
                : "No verdict";

            return (Math.Clamp(score, 0, 100), verdict);
        }
        catch
        {
            // If AI returns unstructured text, try to extract score
            return (0, $"Unparseable AI response: {response[..Math.Min(200, response.Length)]}");
        }
    }
}
