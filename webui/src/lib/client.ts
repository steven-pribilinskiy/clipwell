import type { ClipboardSettings, ClipItem, HealthInfo } from "../types";

/**
 * Thin client for the Clipwell daemon's public API — the same REST + WebSocket
 * surface the Avalonia picker (ui/ClipwellClient.cs) uses. Plain fetch + native
 * WebSocket (the daemon serves loopback CORS), so this runs both in the Tauri
 * webview and in any browser.
 */
function resolveBase(): string {
  // When the daemon serves this SPA (…:8787/app), use its origin. In the Tauri
  // webview (tauri://localhost) or vite dev, fall back to the default daemon URL.
  // Override with ?api=… for ad-hoc testing.
  const override = new URLSearchParams(location.search).get("api");
  if (override) return override.replace(/\/$/, "");
  if (location.protocol.startsWith("http") && location.port === "8787") return location.origin;
  return "http://127.0.0.1:8787";
}

export const API_BASE = resolveBase();

async function getJson<T>(path: string): Promise<T> {
  const res = await fetch(API_BASE + path);
  if (!res.ok) throw new Error(`GET ${path} → ${res.status}`);
  return (await res.json()) as T;
}

async function postJson(path: string, body: unknown): Promise<void> {
  await fetch(API_BASE + path, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

export async function getHealth(): Promise<HealthInfo | null> {
  try {
    return await getJson<HealthInfo>("/health");
  } catch {
    return null;
  }
}

export async function getPage(limit = 100, before?: string): Promise<ClipItem[]> {
  const q = before ? `?limit=${limit}&before=${encodeURIComponent(before)}` : `?limit=${limit}`;
  const page = await getJson<{ items: ClipItem[] }>(`/api/clipboard${q}`);
  return page.items ?? [];
}

export async function getSettings(): Promise<ClipboardSettings> {
  return getJson<ClipboardSettings>("/api/clipboard/settings");
}

export function saveSettings(s: ClipboardSettings): Promise<void> {
  return postJson("/api/clipboard/settings", s);
}

export function deleteItem(timestamp: string): Promise<void> {
  return postJson("/api/clipboard/delete", { timestamp });
}

export function clearAll(): Promise<void> {
  return postJson("/api/clipboard/clear", {});
}

export function pinItem(timestamp: string, pinned: boolean): Promise<void> {
  return postJson("/api/clipboard/pin", { timestamp, pinned });
}

export function setSensitive(timestamp: string, sensitive: boolean): Promise<void> {
  return postJson("/api/clipboard/sensitive", { timestamp, sensitive });
}

export function renameItem(timestamp: string, alias: string | null): Promise<void> {
  return postJson("/api/clipboard/rename", { timestamp, alias });
}

export function imageUrl(timestamp: string): string {
  return `${API_BASE}/api/clipboard/image/${encodeURIComponent(timestamp)}`;
}

/**
 * Connect to the daemon WebSocket and call onChange on every clipboard change,
 * reconnecting with backoff. Returns a disposer.
 */
export function listen(onChange: () => void): () => void {
  let socket: WebSocket | null = null;
  let timer: ReturnType<typeof setTimeout> | undefined;
  let closed = false;
  const wsUrl = API_BASE.replace(/^http/, "ws") + "/api/clipboard/ws";

  const connect = () => {
    if (closed) return;
    socket = new WebSocket(wsUrl);
    socket.onmessage = () => onChange();
    socket.onclose = () => {
      if (!closed) timer = setTimeout(connect, 2000);
    };
    socket.onerror = () => socket?.close();
  };
  connect();

  return () => {
    closed = true;
    if (timer) clearTimeout(timer);
    socket?.close();
  };
}
