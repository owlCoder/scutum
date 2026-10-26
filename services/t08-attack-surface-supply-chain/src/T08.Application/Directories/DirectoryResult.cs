namespace T08.Application.Directories;

/// <summary>Ishod čitanja iz tuđeg servisa (ugovor C-ASSET-v1 / C-ZONE-v1). Nedostupnost nikad nije uspeh.</summary>
public enum DirectoryOutcome
{
    Found,
    NotFound,
    /// <summary>Resurs postoji, ali je dekomisionisan/neaktivan; nova veza se odbija.</summary>
    Inactive,
    Unavailable
}

public sealed record DirectoryResult<T>(DirectoryOutcome Outcome, T? Value) where T : class
{
    public static DirectoryResult<T> Found(T value) => new(DirectoryOutcome.Found, value);
    public static DirectoryResult<T> NotFound() => new(DirectoryOutcome.NotFound, null);
    public static DirectoryResult<T> Inactive() => new(DirectoryOutcome.Inactive, null);
    public static DirectoryResult<T> Unavailable() => new(DirectoryOutcome.Unavailable, null);
}

/// <summary>
/// SEC-CONV-01: format identifikatora iz javnih ugovora: mala slova, cifre i crtica, do 64 znaka.
/// Cifra je dozvoljena na početku jer T01 koristi GUID (kanonski, mala slova), a on često počinje cifrom.
/// </summary>
public static class ContractIdentifier
{
    private static readonly System.Text.RegularExpressions.Regex Pattern =
        new(@"^[a-z0-9][a-z0-9-]{0,63}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static bool IsValid(string? value) => value is not null && Pattern.IsMatch(value);
}
