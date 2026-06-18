import { mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { expect, type Page, test } from "@playwright/test";
import { BASE, MEDIA_DIR, REF_MS } from "./fixtures";

/**
 * Every test: freeze the clock (stable relative times), hide the text caret,
 * block external requests (favicons → deterministic glyph fallback), then load
 * the SPA the daemon serves at /app and wait for the seeded list.
 */
test.beforeEach(async ({ page }) => {
  await page.clock.setFixedTime(REF_MS);
  await page.addInitScript(() => {
    const s = document.createElement("style");
    s.textContent = "*{caret-color:transparent !important}";
    document.documentElement.appendChild(s);
  });
  await page.route("**/*", (route) => {
    const host = new URL(route.request().url()).hostname;
    if (host === "127.0.0.1" || host === "localhost") route.continue();
    else route.abort();
  });
  await page.goto(`/app/?api=${encodeURIComponent(BASE)}`);
  await page.getByText("const sum").waitFor();
});

/** Capture: export the docs PNG, then assert against the committed baseline. */
async function shot(page: Page, state: string): Promise<void> {
  const theme = test.info().project.name; // "light" | "dark"
  const file = join(MEDIA_DIR, state, `web-${theme}.png`);
  mkdirSync(dirname(file), { recursive: true });
  await page.screenshot({ path: file });
  await expect(page).toHaveScreenshot(`${state}.png`);
}

test("picker", async ({ page }) => {
  await shot(page, "picker");
});

test("detail", async ({ page }) => {
  await page.getByTitle("Compact / Detail").click();
  await shot(page, "detail");
});

test("grouped", async ({ page }) => {
  await page.locator("select").first().selectOption("source");
  await shot(page, "grouped");
});

test("actions", async ({ page }) => {
  await page.getByText("https://avaloniaui.net").click();
  await page.keyboard.press("Control+k");
  await page.getByPlaceholder("Actions…").waitFor();
  await shot(page, "actions");
});

test("quicklook", async ({ page }) => {
  await page.getByText("The quick brown fox").click();
  await page.keyboard.press("Control+y");
  await shot(page, "quicklook");
});

test("settings", async ({ page }) => {
  await page.getByRole("button", { name: "⚙" }).click();
  await page.getByText("Settings…").click();
  await shot(page, "settings");
});

test("diagnostics", async ({ page }) => {
  await page.getByRole("button", { name: "⚙" }).click();
  await page.getByText("Diagnostics…").click();
  // The DB path is a volatile temp dir — pin it to a friendly placeholder.
  await page
    .getByTestId("diag-db")
    .evaluate((el) => {
      el.textContent = "db: …/Clipwell/history.db";
    });
  await shot(page, "diagnostics");
});
