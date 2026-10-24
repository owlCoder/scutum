// PRIVREMENA KOPIJA iz plana KT1 (shared kernel, Stojan).
// Posle merge-a feature/T01-shared-kernel: rebase i obrisati ovaj fajl.
namespace Scutum.T01.Domain.Shared;

public readonly record struct AssetId(Guid Value) { public static AssetId New() => new(Guid.NewGuid()); }
public readonly record struct DomainId(Guid Value);   // bezbednosni domen iz R1-01 (T11)
public readonly record struct IdentityId(Guid Value); // identitet iz R1-03 (T12)

// Vrednosti počinju od 1: podrazumevana vrednost 0 nikad nije validna kritičnost.
public enum Criticality { Low = 1, Medium = 2, High = 3, Critical = 4 }

// SEC-CONV-03: stabilni mašinski kodovi, consumer ne parsira tekst
public static class ErrorCodes
{
    public const string Validation = "T01.VALIDATION";
    public const string NotFound = "T01.NOT_FOUND";
    public const string Forbidden = "T01.FORBIDDEN";
    public const string InvalidTransition = "T01.INVALID_TRANSITION";
    public const string Conflict = "T01.CONFLICT";
}

public sealed class Result<T>
{
    private Result(T? value, string? code, string? message) { Value = value; ErrorCode = code; Message = message; }

    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? Message { get; }
    public bool Success => ErrorCode is null;

    public static Result<T> Ok(T value) => new(value, null, null);
    public static Result<T> Fail(string code, string message) => new(default, code, message);
}
