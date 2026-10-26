using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using T08.Application.Audit;

namespace T08.Infrastructure.Audit;

/// <summary>
/// Adapter ka R1-09 (T04), ugovor C-AUDIT-v1: <c>POST api/v1/events</c> (202 prihvaćen, 400 odbijen).
/// Sve ostalo, uključujući mrežnu grešku i timeout, je <see cref="AuditWriteOutcome.Unavailable"/>: događaj nije upisan.
/// </summary>
public sealed class HttpAuditSink(HttpClient http) : IAuditSink
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<AuditWriteOutcome> RecordAsync(AuditEvent auditEvent, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/events")
        {
            Content = JsonContent.Create(new
            {
                occurredAt = auditEvent.OccurredAt,
                actor = auditEvent.Actor,
                action = auditEvent.Action,
                target = auditEvent.Target,
                outcome = auditEvent.Outcome,
                correlationId = auditEvent.CorrelationId,
                sensitivity = auditEvent.Sensitivity,
                details = auditEvent.Details
            }, options: Json)
        };
        request.Headers.Add("X-Correlation-Id", auditEvent.CorrelationId.ToString());

        try
        {
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            return (int)response.StatusCode switch
            {
                202 => AuditWriteOutcome.Accepted,
                400 => AuditWriteOutcome.Rejected,
                _ => AuditWriteOutcome.Unavailable
            };
        }
        catch (Exception ex) when (ex is HttpRequestException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return AuditWriteOutcome.Unavailable;
        }
    }
}
