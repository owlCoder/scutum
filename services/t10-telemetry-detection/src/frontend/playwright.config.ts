import { defineConfig, devices } from "@playwright/test";

// E2E i demo scenariji (KT prikazi). Nisu u CI profilu 'tests' jer traže browsere;
// pokreću se lokalno: `npx playwright install chromium` (jednom), pa `npm run e2e`.
const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:8110";

export default defineConfig({
  testDir: "./e2e",
  reporter: [["list"], ["html", { open: "never" }]],
  use: { baseURL, trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  // Bez E2E_BASE_URL testovi sami podižu produkcioni build; sa njim koriste već pokrenut UI (npr. scutum.sh up t10).
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: "npm run build && npm run start",
        url: `${baseURL}/health`,
        reuseExistingServer: true,
        timeout: 180_000,
        env: { T10_API_BASE_URL: process.env.T10_API_BASE_URL ?? "http://localhost:8010" },
      },
});
