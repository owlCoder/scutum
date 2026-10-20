import { describe, expect, it } from "vitest";

import { toUserFacingError } from "./problem";

describe("toUserFacingError", () => {
  it("prikazuje poruku za poznat kod greške", () => {
    const error = toUserFacingError({ status: 409, code: "CONCURRENCY_CONFLICT" });

    expect(error.code).toBe("CONCURRENCY_CONFLICT");
    expect(error.message).toMatch(/izmenio podatke/);
  });

  it("za nepoznat kod koristi HTTP status, ne tekst sa servera", () => {
    const error = toUserFacingError({ status: 403, code: "SOME_NEW_CODE", title: "Zabranjeno" });

    expect(error.code).toBe("FORBIDDEN");
  });

  it("ne prikazuje title i detail sa servera, čak ni kada sadrže interne podatke", () => {
    const leaky = {
      status: 500,
      title: "NpgsqlException",
      detail: "relation t10_sources.security_data_source; password=dev-only-app",
    };

    const error = toUserFacingError(leaky);

    expect(error.code).toBe("UNEXPECTED");
    expect(error.message).not.toContain("Npgsql");
    expect(error.message).not.toContain("password");
    expect(JSON.stringify(error)).not.toContain(leaky.detail);
  });

  it("isti odgovor za nepostojeći i tuđi resurs (404) ne otkriva razliku", () => {
    expect(toUserFacingError({ status: 404, code: "NOT_FOUND" })).toEqual(toUserFacingError(undefined, 404));
  });

  it("prosleđuje samo ispravan correlationId (UUID)", () => {
    const id = "6f1c2a3b-4d5e-4f60-8a7b-9c0d1e2f3a4b";

    expect(toUserFacingError({ status: 503, correlationId: id }).correlationId).toBe(id);
    expect(toUserFacingError({ status: 503, correlationId: "<script>" }).correlationId).toBeUndefined();
  });

  it("telo koje nije ProblemDetails daje opštu poruku", () => {
    expect(toUserFacingError("Internal Server Error").code).toBe("UNEXPECTED");
    expect(toUserFacingError(["x"], 401).code).toBe("UNAUTHENTICATED");
  });
});
