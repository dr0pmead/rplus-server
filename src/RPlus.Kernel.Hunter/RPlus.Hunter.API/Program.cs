using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using RPlus.Core.Options;
using RPlus.Hunter.API.HeadHunter;
using RPlus.Hunter.API.Persistence;
using RPlus.Hunter.API.Services;
using RPlus.Hunter.API.Waba;
using RPlus.Hunter.API.Workers;
using RPlus.SDK.Hunter.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ─── Database ──────────────────────────────────────────────────────────────
builder.Services.AddDbContextFactory<HunterDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
            npgsqlOptions => npgsqlOptions.UseVector())
           .ConfigureWarnings(w => w.Ignore(
               Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Scoped DbContext via factory — for controllers and inline endpoints
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<HunterDbContext>>().CreateDbContext());

// ─── Redis ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(
    StackExchange.Redis.ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

// ─── Kafka ─────────────────────────────────────────────────────────────────
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection(KafkaOptions.SectionName));
builder.Services.AddSingleton<KafkaEventPublisher>();

// ─── HeadHunter Integration ────────────────────────────────────────────────
builder.Services.Configure<HhOptions>(builder.Configuration.GetSection(HhOptions.SectionName));
builder.Services.AddTransient<HhTokenDelegatingHandler>();

var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
        + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
        onRetry: (outcome, delay, retryCount, _) =>
        {
            Log.Warning("HH API retry {RetryCount} after {Delay}s: {Status}",
                retryCount, delay.TotalSeconds,
                outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.Message);
        });

builder.Services.AddHttpClient<HeadHunterClient>(client =>
    {
        var hhOptions = builder.Configuration.GetSection(HhOptions.SectionName).Get<HhOptions>() ?? new HhOptions();
        client.BaseAddress = new Uri(hhOptions.ApiBaseUrl);
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"RPlus.Hunter/1.0 ({hhOptions.ContactEmail})");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<HhTokenDelegatingHandler>()
    .AddPolicyHandler(retryPolicy);

// ─── HTTP Client: AI Brain (External GPU Server) ───────────────────────────
builder.Services.AddHttpClient("RPlus.AI", client =>
{
    var aiBaseUrl = builder.Configuration["AI:BaseUrl"] ?? "https://ai.rubikom.kz";
    client.BaseAddress = new Uri(aiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(120);

    var internalSecret = builder.Configuration["AI:InternalSecret"];
    if (!string.IsNullOrEmpty(internalSecret))
        client.DefaultRequestHeaders.Add("X-Internal-Service-Secret", internalSecret);
});

// ─── WABA (Meta Cloud API — Direct Graph API v21.0) ────────────────────────
builder.Services.Configure<WabaOptions>(builder.Configuration.GetSection(WabaOptions.SectionName));
builder.Services.AddHttpClient<WabaCloudClient>(client =>
{
    var wabaOpts = builder.Configuration.GetSection(WabaOptions.SectionName).Get<WabaOptions>() ?? new WabaOptions();

    if (string.IsNullOrEmpty(wabaOpts.AccessToken) || string.IsNullOrEmpty(wabaOpts.PhoneNumberId))
        Log.Warning("WABA AccessToken or PhoneNumberId not configured — outbound messages will fail");

    // https://graph.facebook.com/v21.0/{PhoneNumberId}/
    client.BaseAddress = new Uri($"https://graph.facebook.com/v21.0/{wabaOpts.PhoneNumberId}/");
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", wabaOpts.AccessToken);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ─── Webhook Signature Validator ───────────────────────────────────────────
builder.Services.AddSingleton<WabaSignatureValidator>();

// ─── AI Recruiter Orchestrator (Webhook → AI → WABA loop) ──────────────────
builder.Services.AddScoped<AiBrainService>();
builder.Services.AddScoped<AiRecruiterService>();

// ─── SignalR ───────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ─── Controllers (for Webhook) ─────────────────────────────────────────────
builder.Services.AddControllers();

// ─── gRPC ──────────────────────────────────────────────────────────────────
builder.Services.AddGrpc();

// ─── Workers ───────────────────────────────────────────────────────────────
builder.Services.AddHostedService<JudgeWorker>();
builder.Services.AddHostedService<HarvesterWorker>();
builder.Services.AddHostedService<StalkerWorker>();

// Vault — load secrets from HashiCorp Vault KV v2
builder.Configuration.AddVault("hunter");

var app = builder.Build();

// ─── Database Migration (Dev only) ─────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    await using var db = await app.Services.GetRequiredService<IDbContextFactory<HunterDbContext>>().CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

// ─── Endpoints ─────────────────────────────────────────────────────────────
app.MapGrpcService<HunterGrpcService>();
app.MapControllers();  // WhatsApp webhook controller
app.MapHub<HunterHub>("/hubs/hunter");  // SignalR
app.MapGet("/health", () => "Healthy");

// ─── HH OAuth Endpoints (one-time setup) ───────────────────────────────────
app.MapGet("/api/hunter/hh/auth", (IConfiguration config) =>
{
    var hhOptions = config.GetSection(HhOptions.SectionName).Get<HhOptions>() ?? new HhOptions();
    var authUrl = $"{hhOptions.AuthorizeUrl}?response_type=code&client_id={hhOptions.ClientId}&redirect_uri={Uri.EscapeDataString(hhOptions.RedirectUri)}";
    return Results.Ok(new { authUrl });
});

app.MapGet("/api/hunter/hh/callback", async (string code, HeadHunterClient hhClient, HunterDbContext db) =>
{
    var tokenResponse = await hhClient.ExchangeCodeAsync(code);
    if (tokenResponse is null)
        return Results.BadRequest(new { error = "Failed to exchange code for tokens" });

    var credential = new HhCredential
    {
        AccessToken = tokenResponse.AccessToken,
        RefreshToken = tokenResponse.RefreshToken,
        ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
    };

    db.HhCredentials.Add(credential);
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        message = "HH OAuth connected successfully",
        expiresAt = credential.ExpiresAt
    });
});

app.MapGet("/api/hunter/hh/status", async (HunterDbContext db) =>
{
    var cred = await db.HhCredentials
        .OrderByDescending(c => c.UpdatedAt)
        .FirstOrDefaultAsync();

    return Results.Ok(new
    {
        configured = cred is not null,
        expiresAt = cred?.ExpiresAt,
        isExpired = cred?.ExpiresAt < DateTime.UtcNow
    });
});

// ─── Chat History Endpoint ─────────────────────────────────────────────────
app.MapGet("/api/hunter/profiles/{profileId}/chat", async (Guid profileId, HunterDbContext db) =>
{
    var messages = await db.ChatMessages
        .Where(m => m.ProfileId == profileId)
        .OrderBy(m => m.CreatedAt)
        .Select(m => new ChatMessageDto
        {
            Id = m.Id,
            ProfileId = m.ProfileId,
            Direction = m.Direction,
            SenderType = m.SenderType,
            Content = m.Content,
            WabaMessageId = m.WabaMessageId,
            Status = m.Status,
            CreatedAt = m.CreatedAt
        })
        .ToListAsync();

    return Results.Ok(messages);
});

// ─── Test Endpoint: Seed profile + send invite (DEV ONLY) ──────────────────
app.MapPost("/api/hunter/test/invite", async (
    string phone,
    IDbContextFactory<HunterDbContext> dbFactory,
    WabaCloudClient waba,
    IConfiguration config) =>
{
    // Normalize phone: remove + prefix
    var normalizedPhone = phone.TrimStart('+');

    await using var db = await dbFactory.CreateDbContextAsync();

    // Check if profile with this phone already exists
    var existing = await db.ParsedProfiles
        .FirstOrDefaultAsync(p => p.ContactPhone == normalizedPhone);

    if (existing is not null)
    {
        return Results.Ok(new
        {
            message = "Profile already exists, re-sending template",
            profileId = existing.Id,
            taskId = existing.TaskId,
            status = existing.Status.ToString()
        });
    }

    // Create sourcing task
    var task = new SourcingTaskEntity
    {
        PositionName = "Senior .NET Developer (Test)",
        SearchQuery = "C# .NET Senior",
        Conditions = "Опыт > 3 лет, удалёнка, зп до 1.5 млн",
        MessageTemplate = "Здравствуйте! Мы ищем Senior .NET Developer. Интересно?",
        Status = SourcingTaskStatus.Active,
        CreatedByUserId = Guid.Empty
    };
    db.SourcingTasks.Add(task);

    // Create profile with phone in AI_AUTO mode
    var profile = new ParsedProfileEntity
    {
        TaskId = task.Id,
        ExternalId = $"test-{normalizedPhone}",
        Source = "manual-test",
        RawData = "Test candidate profile for E2E verification",
        ContentHash = $"test-{normalizedPhone}-{DateTime.UtcNow:yyyyMMdd}",
        ContactPhone = normalizedPhone,
        ConversationMode = "AI_AUTO",
        Status = ProfileStatus.ContactOpened,
        AiScore = 100,
        AiVerdict = "Test profile — perfect match",
        PreferredChannel = OutreachChannel.WhatsApp
    };
    db.ParsedProfiles.Add(profile);
    await db.SaveChangesAsync();

    Log.Information("Test profile created: {ProfileId} for phone {Phone}", profile.Id, normalizedPhone);

    // Send template invite
    var templateName = config["Waba:InviteTemplateName"] ?? "hello_world";
    var wabaMessageId = await waba.SendTemplateAsync(
        normalizedPhone,
        templateName,
        new List<string>()); // hello_world has no params

    if (wabaMessageId is not null)
    {
        // Update status to InviteSent
        profile.Status = ProfileStatus.InviteSent;
        profile.ContactedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        Log.Information("Template '{Template}' sent to {Phone}, wamid={WabaId}",
            templateName, normalizedPhone, wabaMessageId);
    }
    else
    {
        Log.Error("Failed to send template to {Phone}", normalizedPhone);
    }

    return Results.Ok(new
    {
        message = wabaMessageId is not null
            ? $"✅ Template '{templateName}' sent to +{normalizedPhone}. Reply to it and AI will respond!"
            : $"❌ Template send failed. Check WABA config.",
        profileId = profile.Id,
        taskId = task.Id,
        phone = normalizedPhone,
        conversationMode = profile.ConversationMode,
        templateSent = wabaMessageId is not null,
        wabaMessageId
    });
});

Log.Information("RPlus.Hunter starting — Kafka={Kafka}, Redis={Redis}, AI={AI}, WABA={Waba}",
    builder.Configuration["Kafka:BootstrapServers"] ?? "default",
    builder.Configuration.GetConnectionString("Redis") ?? "default",
    builder.Configuration["AI:BaseUrl"] ?? "rplus-ai:8080",
    string.IsNullOrEmpty(builder.Configuration["Waba:AccessToken"]) ? "not configured" : "Meta Cloud API");

app.Run();
