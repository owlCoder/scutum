namespace T08.Domain.Exposures;

/// <summary>
/// R1-18-UC3: kako je izloženost dostupna spolja (endpoint ili domen, port, protokol, namena).
/// Nepromenljiva vrednost: ili je ceo opis ispravan, ili ne postoji.
/// </summary>
public sealed record ExposureSurface
{
    public const int MaxEndpointLength = 253;   // najduže DNS ime
    public const int MaxPurposeLength = 200;

    /// <summary>Dozvoljeni protokoli (kandidat za ADR). Čuvaju se velikim slovima.</summary>
    public static readonly IReadOnlySet<string> AllowedProtocols =
        new HashSet<string>(StringComparer.Ordinal) { "HTTP", "HTTPS", "TCP", "UDP", "SSH", "TLS", "GRPC" };

    public string Endpoint { get; }
    public int Port { get; }
    public string Protocol { get; }
    public string Purpose { get; }

    private ExposureSurface(string endpoint, int port, string protocol, string purpose)
    {
        Endpoint = endpoint;
        Port = port;
        Protocol = protocol;
        Purpose = purpose;
    }

    /// <exception cref="DomainRuleException">sa stabilnim kodom prvog narušenog pravila.</exception>
    public static ExposureSurface Create(string? endpoint, int port, string? protocol, string? purpose)
    {
        var host = endpoint?.Trim() ?? "";
        if (host.Length == 0 || host.Length > MaxEndpointLength || host.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new DomainRuleException(ExposureErrorCodes.InvalidEndpoint);

        if (port is < 1 or > 65535)
            throw new DomainRuleException(ExposureErrorCodes.InvalidPort);

        var proto = protocol?.Trim().ToUpperInvariant() ?? "";
        if (!AllowedProtocols.Contains(proto))
            throw new DomainRuleException(ExposureErrorCodes.InvalidProtocol);

        var why = purpose?.Trim() ?? "";
        if (why.Length == 0 || why.Length > MaxPurposeLength)
            throw new DomainRuleException(ExposureErrorCodes.InvalidPurpose);

        return new ExposureSurface(host, port, proto, why);
    }
}