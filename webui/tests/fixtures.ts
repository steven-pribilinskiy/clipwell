import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

/**
 * Shared constants for the web visual-regression / docs-capture suite.
 * Imported by playwright.config.ts, the global setup/teardown, and the spec.
 */

const here = import.meta.dirname; // …/webui/tests
const webui = resolve(here, "..");
const repo = resolve(webui, "..");

const PORT = 8799;
export const BASE = `http://127.0.0.1:${PORT}`;

/** Frozen wall-clock so relative timestamps ("just now", "2m ago") never drift. */
export const REF_MS = Date.UTC(2024, 5, 15, 12, 0, 30); // 2024-06-15T12:00:30Z

export const WEBUI_DIST = join(webui, "dist");
export const DAEMON_DLL = join(repo, "daemon", "bin", "Release", "net10.0", "Clipwell.Daemon.dll");
/** Where the exported docs screenshots land: media/shots/<state>/web-<theme>.png */
export const MEDIA_DIR = join(repo, "docs", "public", "media", "shots");

/** Isolated, recreated-each-run daemon data dir (never the real history). */
export const DATA_DIR = join(tmpdir(), "clipwell-pw-shots");
export const PID_FILE = join(DATA_DIR, "daemon.pid");
export const SEED_IMG = join(DATA_DIR, "seed.png");

export interface SeedItem {
  text?: string;
  image?: boolean;
  src: string;
  pin?: boolean;
  sensitive?: boolean;
}

/**
 * The fixed seed set — identical content to the native capture script
 * (bench/capture-shots.ps1) so every surface shows the same items. Oldest first;
 * the daemon shows newest on top (pinned float above).
 */
export const SEED_ITEMS: SeedItem[] = [
  { text: "The quick brown fox jumps over the lazy dog.", src: "Notepad" },
  { text: "const sum = (a, b) => a + b;", src: "VS Code", pin: true },
  { text: "C:\\Users\\you\\Documents\\report.txt", src: "Explorer" },
  { text: "PROJ-1234", src: "Chrome" },
  { text: "#3366ff", src: "Figma" },
  { text: "you@example.com", src: "Outlook", sensitive: true },
  { text: "https://github.com/AvaloniaUI/Avalonia/pull/1234", src: "Chrome" },
  { text: "https://avaloniaui.net", src: "Chrome" },
  { image: true, src: "Snipping Tool" },
];

/** Timestamp for seed item `i` (spread back from REF so the list shows a time range). */
export function seedTimestamp(i: number): string {
  return new Date(REF_MS - (SEED_ITEMS.length - i) * 40_000).toISOString();
}
