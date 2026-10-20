import "server-only";

import createClient from "openapi-fetch";

import type { paths } from "./schema";

// Klijent ka t10-api radi SAMO na serveru (BFF): browser nikad ne dobija token niti adresu API-ja.
// Zato adresa nije NEXT_PUBLIC_ promenljiva, a 'server-only' obara build ako se modul uveze u klijentsku komponentu.

export type ApiCallContext = {
  /** Token iz httpOnly sesije (T10-020); bez njega API vraća 401. */
  accessToken?: string;
  /** Korelacioni ID za praćenje zahteva kroz servise (R1-13); generiše se ako ga nema. */
  correlationId?: string;
};

export function apiBaseUrl(env: Record<string, string | undefined> = process.env): string {
  const url = env.T10_API_BASE_URL;
  if (!url) {
    throw new Error("T10_API_BASE_URL nije postavljen (npr. http://t10-api:8080 u compose-u)");
  }
  return url.replace(/\/+$/, "");
}

export function buildHeaders(context: ApiCallContext = {}): Record<string, string> {
  const headers: Record<string, string> = {
    "X-Correlation-Id": context.correlationId ?? crypto.randomUUID(),
  };
  if (context.accessToken) {
    headers.Authorization = `Bearer ${context.accessToken}`;
  }
  return headers;
}

export function createApiClient(context: ApiCallContext = {}) {
  return createClient<paths>({ baseUrl: apiBaseUrl(), headers: buildHeaders(context) });
}
