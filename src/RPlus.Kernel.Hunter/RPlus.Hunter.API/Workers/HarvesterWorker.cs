using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RPlus.Hunter.API.HeadHunter;
using RPlus.Hunter.API.Persistence;
using RPlus.Hunter.API.Services;
using RPlus.SDK.Hunter.Events;
using RPlus.SDK.Hunter.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RPlus.Hunter.API.Workers;

/// <summary>
/// Harvester Worker — periodically searches HH.ru for active sourcing tasks.
/// Timer-based (every 10 min), iterates active tasks, searches HH, parses → Kafka.
/// Implements Smart Dedup (ContentHash) to avoid re-processing unchanged resumes.
/// </summary>
public sealed class HarvesterWorker : BackgroundService
{
    private readonly IDbContextFactory<HunterDbContext> _dbFactory;
    private readonly HeadHunterClient _hhClient;
    private readonly KafkaEventPublisher _eventPublisher;
    private readonly ILogger<HarvesterWorker> _logger;
    private readonly HhOptions _options;

    private static readonly TimeSpan HarvestInterval = TimeSpan.FromMinutes(10);

    public HarvesterWorker(
        IDbContextFactory<HunterDbContext> dbFactory,
        HeadHunterClient hhClient,
        KafkaEventPublisher eventPublisher,
        IOptions<HhOptions> options,
        ILogger<HarvesterWorker> logger)
    {
        _dbFactory = dbFactory;
        _hhClient = hhClient;
        _eventPublisher = eventPublisher;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HarvesterWorker started, interval: {Interval}", HarvestInterval);

        // Initial delay to let other services start
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await HarvestAllActiveTasksAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "HarvesterWorker cycle failed");
            }

            await Task.Delay(HarvestInterval, stoppingToken);
        }
    }

    private async Task HarvestAllActiveTasksAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var activeTasks = await db.SourcingTasks
            .AsNoTracking()
            .Where(t => t.Status == SourcingTaskStatus.Active)
            .ToListAsync(ct);

        if (activeTasks.Count == 0)
        {
            _logger.LogDebug("No active sourcing tasks, skipping harvest cycle");
            return;
        }

        _logger.LogInformation("Harvesting {Count} active tasks", activeTasks.Count);

        foreach (var task in activeTasks)
        {
            ct.ThrowIfCancellationRequested();
            await HarvestForTaskAsync(task, ct);

            // Rate limiting between tasks — don't hammer HH
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }

    private async Task HarvestForTaskAsync(SourcingTaskEntity task, CancellationToken ct)
    {
        _logger.LogInformation("Harvesting for task {TaskId}: '{Position}' query='{Query}'",
            task.Id, task.PositionName, task.SearchQuery);

        try
        {
            // Parse area from task conditions (simple keyword match)
            var areaId = ResolveAreaId(task.Conditions);

            // Parse salary range from conditions
            var (salaryFrom, salaryTo) = ParseSalaryRange(task.Conditions);

            // Search HH — first 2 pages (up to 200 resumes per cycle)
            for (var page = 0; page < 2; page++)
            {
                var searchResult = await _hhClient.SearchResumesAsync(
                    text: task.SearchQuery,
                    areaId: areaId,
                    salaryFrom: salaryFrom,
                    salaryTo: salaryTo,
                    page: page,
                    perPage: 100,
                    ct: ct);

                if (searchResult.Items.Count == 0)
                {
                    _logger.LogDebug("No more results on page {Page} for task {TaskId}", page, task.Id);
                    break;
                }

                _logger.LogInformation("Found {Count}/{Total} resumes on page {Page} for task {TaskId}",
                    searchResult.Items.Count, searchResult.Found, page, task.Id);

                foreach (var resumeShort in searchResult.Items)
                {
                    ct.ThrowIfCancellationRequested();
                    await ProcessResumeAsync(task, resumeShort, ct);

                    // Rate limiting between resume fetches
                    await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
                }

                // Don't fetch more if we got all results
                if (page + 1 >= searchResult.Pages)
                    break;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HH API error for task {TaskId}", task.Id);
        }
    }

    private async Task ProcessResumeAsync(SourcingTaskEntity task, HhResumeShort resumeShort, CancellationToken ct)
    {
        var externalId = resumeShort.Id;

        // Fetch full resume details
        var fullResume = await _hhClient.GetResumeAsync(externalId, ct);
        if (fullResume is null)
            return;

        var plainText = fullResume.ToPlainText();
        var contentHash = ComputeHash(plainText);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Smart Dedup: check if we already have this resume for this task
        var existing = await db.ParsedProfiles
            .FirstOrDefaultAsync(p => p.TaskId == task.Id && p.ExternalId == externalId, ct);

        if (existing is not null)
        {
            if (existing.ContentHash == contentHash)
            {
                _logger.LogDebug("Resume {ExternalId} unchanged for task {TaskId}, skipping", externalId, task.Id);
                return;
            }

            // Content changed — update and trigger re-scoring
            existing.RawData = plainText;
            existing.ContentHash = contentHash;
            existing.Status = ProfileStatus.New;
            existing.AiScore = null;
            existing.AiVerdict = null;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Resume {ExternalId} updated for task {TaskId}, re-scoring", externalId, task.Id);
        }
        else
        {
            // Detect Telegram handle
            var tgHandle = ExtractTelegramHandle(plainText);

            var profile = new ParsedProfileEntity
            {
                TaskId = task.Id,
                ExternalId = externalId,
                Source = "hh.ru",
                RawData = plainText,
                ContentHash = contentHash,
                TelegramHandle = tgHandle,
                PreferredChannel = !string.IsNullOrEmpty(tgHandle)
                    ? OutreachChannel.Telegram
                    : OutreachChannel.WhatsApp
            };

            db.ParsedProfiles.Add(profile);
            await db.SaveChangesAsync(ct);

            existing = profile;

            // Update counter
            await db.SourcingTasks
                .Where(t => t.Id == task.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.CandidatesFound, t => t.CandidatesFound + 1), ct);

            _logger.LogInformation("New resume {ExternalId} parsed for task {TaskId} (TG: {HasTg})",
                externalId, task.Id, tgHandle is not null);
        }

        // Publish to Kafka for JudgeWorker
        await _eventPublisher.PublishAsync(
            new ProfileParsedEvent
            {
                ProfileId = existing.Id,
                TaskId = task.Id,
                ExternalId = externalId,
                RawData = plainText,
                ContentHash = contentHash,
                Conditions = task.Conditions,
                MinScore = task.MinScore
            },
            HunterTopics.ProfilesParsed,
            task.Id.ToString(),
            ct);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves HH area ID from task conditions text.
    /// Scans for known city names and returns the first match.
    /// </summary>
    private int? ResolveAreaId(string conditions)
    {
        var lower = conditions.ToLowerInvariant();
        foreach (var (cityName, areaId) in _options.AreaCodes)
        {
            if (lower.Contains(cityName.ToLowerInvariant()))
                return areaId;
        }
        return null;
    }

    /// <summary>
    /// Parses salary range from conditions text.
    /// Supports patterns like "ЗП 500000-800000", "от 300 тыс", "до 1.5 млн".
    /// </summary>
    private static (int? From, int? To) ParseSalaryRange(string conditions)
    {
        int? from = null;
        int? to = null;

        // Pattern: "от X" or "from X"
        var fromMatch = Regex.Match(conditions, @"(?:от|from)\s*([\d.,]+)\s*(?:тыс|k)?(?:\s*(млн|m))?",
            RegexOptions.IgnoreCase);
        if (fromMatch.Success && double.TryParse(fromMatch.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var f))
        {
            from = (int)(fromMatch.Groups[2].Success ? f * 1_000_000 : f < 1000 ? f * 1000 : f);
        }

        // Pattern: "до X" or "to X"
        var toMatch = Regex.Match(conditions, @"(?:до|to)\s*([\d.,]+)\s*(?:тыс|k)?(?:\s*(млн|m))?",
            RegexOptions.IgnoreCase);
        if (toMatch.Success && double.TryParse(toMatch.Groups[1].Value.Replace(",", "."),
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t))
        {
            to = (int)(toMatch.Groups[2].Success ? t * 1_000_000 : t < 1000 ? t * 1000 : t);
        }

        return (from, to);
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    private static string? ExtractTelegramHandle(string text)
    {
        var tmeMatch = Regex.Match(text, @"t\.me/([a-zA-Z0-9_]{5,32})", RegexOptions.IgnoreCase);
        if (tmeMatch.Success)
            return "@" + tmeMatch.Groups[1].Value;

        var atMatch = Regex.Match(text, @"(?<!\S)@([a-zA-Z][a-zA-Z0-9_]{4,31})(?!\S)");
        if (atMatch.Success && !text.Contains(atMatch.Value + "."))
            return atMatch.Value;

        return null;
    }
}
