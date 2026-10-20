import { expect, test } from "@playwright/test";

test("health odgovara bez keširanja", async ({ request }) => {
  const response = await request.get("/health");

  expect(response.status()).toBe(200);
  expect(response.headers()["cache-control"]).toBe("no-store");
  expect(await response.json()).toEqual({ status: "ok" });
});

test("početna strana prikazuje sve tri celine i šalje bezbednosna zaglavlja", async ({ page }) => {
  const response = await page.goto("/");

  expect(response?.headers()["x-frame-options"]).toBe("DENY");
  expect(response?.headers()["x-powered-by"]).toBeUndefined();
  await expect(page.getByRole("heading", { name: "Pregled" })).toBeVisible();
  for (const unit of ["R1-19", "R2-17", "R3-15"]) {
    await expect(page.getByText(unit, { exact: true })).toBeVisible();
  }
});
