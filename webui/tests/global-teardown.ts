import { readFileSync, rmSync } from "node:fs";
import { DATA_DIR, PID_FILE } from "./fixtures";

export default async function globalTeardown(): Promise<void> {
  try {
    const pid = Number(readFileSync(PID_FILE, "utf8").trim());
    if (pid) process.kill(pid);
  } catch {
    /* already gone */
  }
  // Best-effort: Windows may briefly hold the SQLite file after the daemon dies.
  // The next run recreates DATA_DIR anyway, so a lingering temp dir is harmless.
  try {
    rmSync(DATA_DIR, { recursive: true, force: true });
  } catch {
    /* file handles not released yet — ignore */
  }
}
