import { describe, expect, it } from "vitest";

import { apiBaseUrl, buildHeaders } from "./client";

describe("apiBaseUrl", () => {
  it("zahteva T10_API_BASE_URL umesto da tiho pogađa adresu", () => {
    expect(() => apiBaseUrl({})).toThrow(/T10_API_BASE_URL/);
  });

  it("uklanja završnu kosu crtu", () => {
    expect(apiBaseUrl({ T10_API_BASE_URL: "http://t10-api:8080/" })).toBe("http://t10-api:8080");
  });
});

describe("buildHeaders", () => {
  it("uvek šalje korelacioni ID, a token samo kada postoji", () => {
    const anonymous = buildHeaders();

    expect(anonymous["X-Correlation-Id"]).toMatch(/^[0-9a-f-]{36}$/);
    expect(anonymous).not.toHaveProperty("Authorization");
  });

  it("prosleđuje postojeći korelacioni ID i bearer token", () => {
    const headers = buildHeaders({ accessToken: "test-token", correlationId: "corr-1" });

    expect(headers).toEqual({ "X-Correlation-Id": "corr-1", Authorization: "Bearer test-token" });
  });
});
