import { connection } from "next/server";

// GET /health — konvencija repoa za proveru da servis radi. connection() sprečava da se odgovor
// prerenderuje pri build-u, pa 200 znači da server stvarno odgovara u trenutku zahteva.
export async function GET() {
  await connection();
  return Response.json({ status: "ok" }, { headers: { "Cache-Control": "no-store" } });
}
