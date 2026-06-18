import { defineConfig } from "@playwright/test";
import { BASE } from "./tests/fixtures";

/**
 * Web visual-regression + docs-screenshot capture for the Solid web UI.
 *
 * - Two projects, `light`/`dark`, drive the picker via `colorScheme` emulation
 *   (the app's theme is "system", so it follows the emulated preference).
 * - `toHaveScreenshot` baselines (committed under tests/__screenshots__, keyed by
 *   project AND platform) are the regression gate. Generate the per-OS baseline in
 *   CI on first run with `--update-snapshots`; an unintended change then fails CI.
 * - Each test ALSO exports its capture to docs/public/media/shots/<state>/web-<theme>.png
 *   so the docs <ShotSwitcher> can show the web surface.
 *
 * Run from the `webui/` directory with Node (not the Bun runner):
 *   npx playwright test          # or: bunx playwright test
 */
export default defineConfig({
  testDir: "./tests",
  globalSetup: "./tests/global-setup.ts",
  globalTeardown: "./tests/global-teardown.ts",
  fullyParallel: false,
  workers: 1, // one shared daemon; no port races
  forbidOnly: !!process.env.CI,
  reporter: "list",
  snapshotPathTemplate: "tests/__screenshots__/{arg}-{projectName}-{platform}{ext}",
  use: {
    baseURL: BASE,
    viewport: { width: 900, height: 820 },
    deviceScaleFactor: 2, // crisp, hiDPI-like docs media
  },
  expect: {
    toHaveScreenshot: { animations: "disabled", maxDiffPixelRatio: 0.01 },
  },
  projects: [
    { name: "light", use: { colorScheme: "light" } },
    { name: "dark", use: { colorScheme: "dark" } },
  ],
});
