import { spawn } from "node:child_process";
import { mkdirSync, rmSync, writeFileSync } from "node:fs";
import { deflateSync } from "node:zlib";
import {
  BASE,
  DAEMON_DLL,
  DATA_DIR,
  PID_FILE,
  SEED_IMG,
  SEED_ITEMS,
  seedTimestamp,
  WEBUI_DIST,
} from "./fixtures";

const CRC = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();

function crc32(buf: Buffer): number {
  let c = 0xffffffff;
  for (let i = 0; i < buf.length; i++) c = CRC[(c ^ buf[i]) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type: string, data: Buffer): Buffer {
  const len = Buffer.alloc(4);
  len.writeUInt32BE(data.length, 0);
  const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body), 0);
  return Buffer.concat([len, body, crc]);
}

/** Minimal solid-colour PNG (no deps) so the image item shows a stable thumbnail. */
function makePng(w: number, h: number, rgb: [number, number, number]): Buffer {
  const sig = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 2; // colour type RGB
  const raw = Buffer.alloc(h * (1 + w * 3));
  for (let y = 0; y < h; y++) {
    const row = y * (1 + w * 3);
    raw[row] = 0; // filter: none
    for (let x = 0; x < w; x++) {
      const p = row + 1 + x * 3;
      raw[p] = rgb[0];
      raw[p + 1] = rgb[1];
      raw[p + 2] = rgb[2];
    }
  }
  return Buffer.concat([
    sig,
    chunk("IHDR", ihdr),
    chunk("IDAT", deflateSync(raw)),
    chunk("IEND", Buffer.alloc(0)),
  ]);
}

async function post(path: string, body: unknown): Promise<void> {
  const res = await fetch(BASE + path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new Error(`POST ${path} → ${res.status}`);
}

async function waitForHealth(): Promise<void> {
  for (let i = 0; i < 60; i++) {
    try {
      const res = await fetch(`${BASE}/health`);
      if (res.ok) return;
    } catch {
      /* not up yet */
    }
    await new Promise((r) => setTimeout(r, 400));
  }
  throw new Error("daemon did not become healthy");
}

export default async function globalSetup(): Promise<void> {
  rmSync(DATA_DIR, { recursive: true, force: true });
  mkdirSync(DATA_DIR, { recursive: true });
  writeFileSync(SEED_IMG, makePng(320, 160, [60, 90, 200]));

  // `dotnet <dll>` runs the daemon in-process (cross-platform, killable by PID —
  // unlike `dotnet run`, which forks an apphost).
  const child = spawn("dotnet", [DAEMON_DLL], {
    stdio: "ignore",
    env: {
      ...process.env,
      CLIPWELL_URL: BASE,
      CLIPWELL_WEBUI_DIR: WEBUI_DIST,
      CLIPWELL_DATA_DIR: DATA_DIR,
      CLIPWELL_NO_WATCH: "1",
      CLIPWELL_NO_SWEEP: "1",
      CLIPWELL_ALLOW_SEED: "1",
    },
  });
  writeFileSync(PID_FILE, String(child.pid));

  await waitForHealth();

  for (let i = 0; i < SEED_ITEMS.length; i++) {
    const it = SEED_ITEMS[i];
    const timestamp = seedTimestamp(i);
    await post("/api/clipboard/_seed", {
      timestamp,
      text: it.text ?? null,
      hasImage: !!it.image,
      imagePath: it.image ? SEED_IMG : null,
      sourceApp: it.src,
    });
    if (it.pin) await post("/api/clipboard/pin", { timestamp, pinned: true });
    if (it.sensitive) await post("/api/clipboard/sensitive", { timestamp, sensitive: true });
  }
}
