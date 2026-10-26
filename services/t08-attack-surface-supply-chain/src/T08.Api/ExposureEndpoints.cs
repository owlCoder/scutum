using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using T08.Application.Audit;
using T08.Application.Exposures;
using T08.Domain.Exposures;
using T08.Infrastructure.Security;

namespace T08.Api;

/// <summary>R1-18-UC4 preko HTTP-a. Identitet i scope dolaze samo iz validiranog tokena, nikad iz tela (SEC-CONV-02).</summary>
public static class ExposureEndpoints
{
    private const int MaxBodyBytes = 4096;

    private sealed record ClassifyRequest(string? Classification, DateTimeOffset? ExpiresAt);
    private sealed record RegisterRequest(string? AssetId, string? ZoneId, string? Justification);
    private sealed record CloseRequest(string? ReasonCode);

    /// <summary>Stabilni kodovi greške (SEC-CONV-03) → HTTP status. Nepoznat kod nikad ne postaje uspeh: 500.</summary>
    private static readonly Dictionary<string, int> StatusByCode = new()
    {
        [ErrorCodes.NotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.Forbidden] = StatusCodes.Status403Forbidden,
        [ErrorCodes.Conflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.AssetNotUsable] = StatusCodes.Status409Conflict,
        [ErrorCodes.AssetScopeMismatch] = StatusCodes.Status409Conflict,
        [ErrorCodes.ZoneNotUsable] = StatusCodes.Status409Conflict,
        [ErrorCodes.UpstreamUnavailable] = StatusCodes.Status503ServiceUnavailable,
        [ExposureErrorCodes.ExposureClosed] = StatusCodes.Status409Conflict,
        [ExposureErrorCodes.InvalidClassification] = StatusCodes.Status400BadRequest,
        [ExposureErrorCodes.InvalidExpiry] = StatusCodes.Status400BadRequest,
        [ExposureErrorCodes.InvalidExposure] = StatusCodes.Status400BadRequest,
        ["INVALID_REQUEST"] = StatusCodes.Status400BadRequest,
        ["INVALID_CORRELATION_ID"] = StatusCodes.Status400BadRequest,
        ["UNAUTHENTICATED"] = StatusCodes.Status401Unauthorized
    };

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/exposures", RegisterAsync);
        app.MapPost("/api/v1/exposures/{id}/classification", ClassifyAsync);
        app.MapPost("/api/v1/exposures/{id}/confirmation", ConfirmAsync);
        app.MapPost("/api/v1/exposures/{id}/closure", CloseAsync);
    }

    private static async Task<IResult> RegisterAsync(
        HttpContext http, DevActorTokens tokens, RegisterExposureHandler handler)
    {
        if (!TryAuthenticate(http, tokens, out var actor, out var correlationId, out var failure)) return failure!;

        var (body, bodyError) = await ReadBodyAsync<RegisterRequest>(http).ConfigureAwait(false);
        if (body is null) return bodyError!;

        var result = await handler.HandleAsync(
            actor!, new RegisterExposureCommand(body.AssetId ?? "", body.ZoneId ?? "", body.Justification ?? "", correlationId),
            http.RequestAborted).ConfigureAwait(false);

        return result.Succeeded
            ? Results.Json(new { exposureId = result.ExposureId, auditDelivered = result.AuditDelivered },
                statusCode: StatusCodes.Status201Created)
            : Error(result.ErrorCode!);
    }

    private static async Task<IResult> ClassifyAsync(
        string id, HttpContext http, DevActorTokens tokens, ClassifyExposureHandler handler)
    {
        if (!TryAuthenticate(http, tokens, out var actor, out var correlationId, out var failure)) return failure!;

        var (body, bodyError) = await ReadBodyAsync<ClassifyRequest>(http).ConfigureAwait(false);
        if (body is null) return bodyError!;

        // Samo tačno ime vrednosti: brojevi i nepoznate vrednosti se odbijaju (isto pravilo kao kod upstream odgovora).
        if (!TryParseClassification(body.Classification, out var classification))
            return Error(ExposureErrorCodes.InvalidClassification);

        // Odluka je na serveru: scope, dozvola, asset, domenska pravila, audit.
        var result = await handler.HandleAsync(
            actor!, new ClassifyExposureCommand(id, classification, body.ExpiresAt, correlationId), http.RequestAborted)
            .ConfigureAwait(false);

        return result.Succeeded
            ? Results.Json(new
            {
                classification = result.Classification!.Value.ToString(),
                changed = result.Changed,
                auditDelivered = result.AuditDelivered
            })
            : Error(result.ErrorCode!);
    }

    /// <summary>Potvrda nema telo: sve što treba je ID resursa i akter iz tokena.</summary>
    private static async Task<IResult> ConfirmAsync(
        string id, HttpContext http, DevActorTokens tokens, ConfirmExposureHandler handler)
    {
        if (!TryAuthenticate(http, tokens, out var actor, out var correlationId, out var failure)) return failure!;

        var result = await handler.HandleAsync(actor!, new ConfirmExposureCommand(id, correlationId), http.RequestAborted)
            .ConfigureAwait(false);

        return result.Succeeded ? Results.Json(new { auditDelivered = result.AuditDelivered }) : Error(result.ErrorCode!);
    }

    private static async Task<IResult> CloseAsync(
        string id, HttpContext http, DevActorTokens tokens, CloseExposureHandler handler)
    {
        if (!TryAuthenticate(http, tokens, out var actor, out var correlationId, out var failure)) return failure!;

        var (body, bodyError) = await ReadBodyAsync<CloseRequest>(http).ConfigureAwait(false);
        if (body is null) return bodyError!;

        var result = await handler.HandleAsync(
            actor!, new CloseExposureCommand(id, body.ReasonCode ?? "", correlationId), http.RequestAborted)
            .ConfigureAwait(false);

        return result.Succeeded ? Results.Json(new { auditDelivered = result.AuditDelivered }) : Error(result.ErrorCode!);
    }

    /// <summary>
    /// Zajednički početak: ko poziva, pa oblik zahteva. Bez validnog tokena se ne ide dalje (nema audita: nepoznat akter,
    /// a svako može da šalje smeće). Greške oblika ne zavise od postojanja resursa, pa ništa ne otkrivaju.
    /// </summary>
    private static bool TryAuthenticate(
        HttpContext http, DevActorTokens tokens, out ActorContext? actor, out Guid correlationId, out IResult? failure)
    {
        actor = null;
        correlationId = Guid.Empty;
        failure = null;

        // Token čiji sub liči na tajnu se ne prihvata: akter ide u audit, a AuditEvent.Create bi bacio (500) umesto 401.
        if (!TryGetBearer(http, out var bearer) || !tokens.TryValidate(bearer, out actor)
            || AuditEvent.LooksLikeSecret(actor!.ActorId))
        {
            actor = null;
            http.Response.Headers.WWWAuthenticate = "Bearer";
            failure = Error("UNAUTHENTICATED");
            return false;
        }

        if (!Guid.TryParse(http.Request.Headers["X-Correlation-Id"].ToString(), out correlationId) || correlationId == Guid.Empty)
        {
            failure = Error("INVALID_CORRELATION_ID");
            return false;
        }

        return true;
    }

    private static async Task<(T? Body, IResult? Failure)> ReadBodyAsync<T>(HttpContext http) where T : class
    {
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = MaxBodyBytes;

        try
        {
            var body = await http.Request.ReadFromJsonAsync<T>(http.RequestAborted).ConfigureAwait(false);
            return body is null ? (null, Error("INVALID_REQUEST")) : (body, null);
        }
        catch (Exception ex) when (ex is JsonException or BadHttpRequestException or InvalidOperationException)
        {
            return (null, Error("INVALID_REQUEST"));
        }
    }

    private static IResult Error(string code) =>
        Results.Json(new { code }, statusCode: StatusByCode.GetValueOrDefault(code, StatusCodes.Status500InternalServerError));

    private static bool TryGetBearer(HttpContext http, out string? token)
    {
        token = null;
        var header = http.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.Ordinal)) return false;

        token = header[prefix.Length..].Trim();
        return token.Length > 0;
    }

    private static bool TryParseClassification(string? raw, out Classification value)
    {
        value = default;
        return !string.IsNullOrEmpty(raw)
               && !char.IsDigit(raw[0]) && raw[0] != '-'
               && Enum.TryParse(raw, ignoreCase: false, out value)
               && Enum.IsDefined(value);
    }
}
