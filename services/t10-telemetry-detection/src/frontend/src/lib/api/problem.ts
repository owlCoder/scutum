// Greške API-ja stižu kao RFC 9457 ProblemDetails sa stabilnim poljem `code` (SEC-CONV-03).
// Korisniku prikazujemo poruku po kodu ili HTTP statusu, NIKAD tekst sa servera (`title`/`detail`):
// on može da sadrži interne detalje, a consumer ne sme da zavisi od proizvoljnog teksta greške.

export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  correlationId?: string;
};

export type UserFacingError = {
  code: string;
  message: string;
  /** Prikazuje se korisniku da bi prijava problema mogla da se poveže sa logovima. */
  correlationId?: string;
};

const MESSAGES: Record<string, string> = {
  UNAUTHENTICATED: "Sesija je istekla. Prijavite se ponovo.",
  FORBIDDEN: "Nemate dozvolu za ovu operaciju.",
  NOT_FOUND: "Traženi zapis ne postoji ili mu nemate pristup.",
  VALIDATION_FAILED: "Podaci nisu ispravni. Proverite označena polja.",
  CONCURRENCY_CONFLICT: "Neko je u međuvremenu izmenio podatke. Osvežite stranicu i pokušajte ponovo.",
  DEPENDENCY_UNAVAILABLE: "Servis od kog zavisimo trenutno nije dostupan. Pokušajte kasnije.",
  UNEXPECTED: "Došlo je do neočekivane greške.",
};

const CODE_BY_STATUS: Record<number, string> = {
  400: "VALIDATION_FAILED",
  401: "UNAUTHENTICATED",
  403: "FORBIDDEN",
  404: "NOT_FOUND",
  409: "CONCURRENCY_CONFLICT",
  422: "VALIDATION_FAILED",
  503: "DEPENDENCY_UNAVAILABLE",
};

export function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** Pretvara odgovor sa greškom u poruku za korisnika, bez ikakvog teksta koji je poslao server. */
export function toUserFacingError(body: unknown, httpStatus?: number): UserFacingError {
  const problem = isProblemDetails(body) ? body : {};
  const status = typeof problem.status === "number" ? problem.status : httpStatus;
  const knownCode = typeof problem.code === "string" && problem.code in MESSAGES ? problem.code : undefined;
  const code = knownCode ?? (status !== undefined ? CODE_BY_STATUS[status] : undefined) ?? "UNEXPECTED";
  const correlationId =
    typeof problem.correlationId === "string" && /^[0-9a-f-]{36}$/i.test(problem.correlationId)
      ? problem.correlationId
      : undefined;
  return { code, message: MESSAGES[code], correlationId };
}
