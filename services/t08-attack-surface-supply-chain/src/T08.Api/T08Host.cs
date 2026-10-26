using T08.Application.Audit;
using T08.Application.Directories;
using T08.Application.Exposures;
using T08.Infrastructure.Audit;
using T08.Infrastructure.Directories;
using T08.Infrastructure.Http;
using T08.Infrastructure.Persistence;
using T08.Infrastructure.Security;
using T08.Infrastructure.Stubs;

namespace T08.Api;

/// <summary>Sastavljanje aplikacije na jednom mestu da ga koriste <c>Program.cs</c> i testovi (pravi HTTP, bez izmišljenog hosta).</summary>
public static class T08Host
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var config = builder.Configuration;

        // Identitet servisa ka T01/T04/T11 dolazi iz okruženja (nikad iz koda ili repozitorijuma).
        string? ServiceToken() => config["T08_SERVICE_TOKEN"];

        // Svaki upstream poziv ima kratak timeout (SEC-OPS-05): spor ili mrtav servis se tumači kao Unavailable, nikad kao uspeh.
        void AddUpstream<TPort, TImpl, TStub>(string baseUrlKey)
            where TPort : class
            where TImpl : class, TPort
            where TStub : class, TPort, new()
        {
            var baseUrl = config[baseUrlKey];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                // Razvoj bez upstream servisa: stub (samo normalan/failure ishod, bez tuđe security logike).
                builder.Services.AddSingleton<TStub>();
                builder.Services.AddSingleton<TPort>(sp => sp.GetRequiredService<TStub>());
                return;
            }

            builder.Services.AddHttpClient<TPort, TImpl>(client =>
                {
                    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
                    client.Timeout = TimeSpan.FromSeconds(5);
                })
                .AddHttpMessageHandler(_ => new BearerTokenHandler(ServiceToken));
        }

        AddUpstream<IAssetReader, HttpAssetDirectory, StubAssetDirectory>("T01_BASE_URL");
        AddUpstream<IZoneReader, HttpZoneDirectory, StubZoneDirectory>("T11_BASE_URL");

        // Audit: ka T04 ako je podešen, inače lokalni stub; u oba slučaja sa baferom da događaj ne nestane kad T04 padne.
        if (string.IsNullOrWhiteSpace(config["T04_BASE_URL"]))
        {
            builder.Services.AddSingleton<InMemoryAuditSink>();
            builder.Services.AddSingleton(sp => new BufferedAuditSink(sp.GetRequiredService<InMemoryAuditSink>()));
            builder.Services.AddSingleton<IAuditSink>(sp => sp.GetRequiredService<BufferedAuditSink>());
        }
        else
        {
            builder.Services.AddHttpClient<HttpAuditSink>(client =>
                {
                    var baseUrl = config["T04_BASE_URL"]!;
                    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
                    client.Timeout = TimeSpan.FromSeconds(5);
                })
                .AddHttpMessageHandler(_ => new BearerTokenHandler(ServiceToken));
            builder.Services.AddSingleton(sp => new BufferedAuditSink(sp.GetRequiredService<HttpAuditSink>()));
            builder.Services.AddSingleton<IAuditSink>(sp => sp.GetRequiredService<BufferedAuditSink>());
        }

        // Pozadinski flush (podrazumevano na 10 s; T08_AUDIT_FLUSH_SECONDS <= 0 ga gasi).
        var flushSeconds = int.TryParse(config["T08_AUDIT_FLUSH_SECONDS"], out var s) ? s : 10;
        if (flushSeconds > 0)
        {
            builder.Services.AddHostedService(sp => new AuditFlushService(
                sp.GetRequiredService<BufferedAuditSink>(), TimeSpan.FromSeconds(flushSeconds), sp.GetRequiredService<ILogger<AuditFlushService>>()));
        }

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<InMemoryExposureRepository>();   // privremeno, do prave baze
        builder.Services.AddSingleton<IExposureRepository>(sp => sp.GetRequiredService<InMemoryExposureRepository>());
        builder.Services.AddSingleton<ClassifyExposureHandler>();
        builder.Services.AddSingleton<RegisterExposureHandler>(sp => new RegisterExposureHandler(
            sp.GetRequiredService<IExposureRepository>(), sp.GetRequiredService<IAssetReader>(), sp.GetRequiredService<IZoneReader>(),
            sp.GetRequiredService<IAuditSink>(), sp.GetRequiredService<TimeProvider>()));
        builder.Services.AddSingleton<ConfirmExposureHandler>();
        builder.Services.AddSingleton<CloseExposureHandler>();

        // Razvojni identitet dok T12/T02 nemaju ugovor. Bez ključa (>= 32 bajta) svaki zahtev je 401.
        builder.Services.AddSingleton(sp => new DevActorTokens(config["T08_DEV_TOKEN_KEY"], sp.GetRequiredService<TimeProvider>()));

        var app = builder.Build();

        // Odbačen audit događaj = audit nepotpun (В.2): log nivoa Error, bez sadržaja događaja (samo akcija, ključ, brojač).
        var dropLog = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("T08.Audit");
        app.Services.GetRequiredService<BufferedAuditSink>().OnDropped = (e, total) =>
            dropLog.LogError("Audit događaj odbačen jer je red pun (action={Action}, key={Key}, ukupno odbačenih={Total}).",
                e.Action, e.IdempotencyKey, total);

        app.MapGet("/health", () => Results.Text("OK"));

        // Vidljivost audit reda (nalaz В.2): pokusa flush, pa javlja Pending/Dropped. Dropped > 0 = audit nepotpun -> 503.
        app.MapGet("/health/audit", async (BufferedAuditSink audit, CancellationToken ct) =>
        {
            await audit.FlushAsync(ct);
            var body = new { pending = audit.Pending, dropped = audit.Dropped, status = audit.Dropped > 0 ? "incomplete" : audit.Pending > 0 ? "degraded" : "ok" };
            return audit.Dropped > 0 ? Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable) : Results.Json(body);
        });
        ExposureEndpoints.Map(app);

        return app;
    }
}
