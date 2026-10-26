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
            builder.Services.AddSingleton<IAuditSink>(sp => new BufferedAuditSink(sp.GetRequiredService<InMemoryAuditSink>()));
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
            builder.Services.AddSingleton<IAuditSink>(sp => new BufferedAuditSink(sp.GetRequiredService<HttpAuditSink>()));
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

        app.MapGet("/health", () => Results.Text("OK"));
        ExposureEndpoints.Map(app);

        return app;
    }
}
