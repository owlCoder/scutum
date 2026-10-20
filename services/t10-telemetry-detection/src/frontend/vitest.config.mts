import { fileURLToPath } from "node:url";

import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// TEST_RESULTS_DIR postavlja Docker 'test' stage (/results): tada se uz konzolni izlaz piše i JUnit XML,
// koji CI čuva kao dokaz (services/t10-*/TestResults).
const resultsDir = process.env.TEST_RESULTS_DIR;

export default defineConfig({
  plugins: [react()],
  resolve: {
    tsconfigPaths: true, // alias @/* iz tsconfig.json
    alias: {
      // 'server-only' namerno puca van React Server okruženja; u unit testovima ga menjamo praznim modulom.
      "server-only": fileURLToPath(new URL("./src/test/server-only.ts", import.meta.url)),
    },
  },
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: ["./src/test/setup.ts"],
    reporters: resultsDir ? ["default", "junit"] : ["default"],
    outputFile: resultsDir ? { junit: `${resultsDir}/frontend-junit.xml` } : undefined,
  },
});
