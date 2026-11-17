import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  timeout: 45_000,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [["list"]],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? "http://127.0.0.1:3200",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      use: { browserName: "chromium" },
    },
  ],
  webServer: [
    {
      command: "dotnet run --project ../../src/Orphac.Athera.Api --no-build --no-launch-profile",
      url: "http://127.0.0.1:5200/api/health",
      reuseExistingServer: false,
      timeout: 60_000,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: "http://127.0.0.1:5200",
        ConnectionStrings__Runtime:
          process.env.ConnectionStrings__Runtime ??
          "Host=127.0.0.1;Port=55433;Database=athera_dev;Username=athera_app;Password=athera_dev_local_app_pw",
      },
    },
    {
      command: "npm run start",
      url: "http://127.0.0.1:3200/login",
      reuseExistingServer: false,
      timeout: 60_000,
      env: {
        ATHERA_API_UPSTREAM: "http://127.0.0.1:5200",
      },
    },
  ],
});
