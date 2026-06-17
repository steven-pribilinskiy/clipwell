// Mirrors protocol/ClipItem.cs and protocol/ClipboardSettings.cs (camelCase JSON).

export interface ClipItem {
  id: string;
  timestamp: string;
  formats: string[];
  kind: string | null;
  textContent: string | null;
  textLength: number;
  htmlContent: string | null;
  hasImage: boolean;
  isPinned: boolean;
  isUserPinned: boolean;
  isSensitive: boolean;
  sourceApp: string;
  alias: string | null;
  isEdited: boolean;
}

export interface ClipboardSettings {
  retentionDays: number | null;
  openAtCursor: boolean;
  theme: "system" | "light" | "dark";
  defaultView: "compact" | "detail";
  defaultGroup: "none" | "date" | "source";
  showSource: boolean;
  showTime: boolean;
  hotkey: string;
}

export interface HealthInfo {
  status: string;
  db: string;
  subscribers: number;
}

export type Tab = "all" | "pinned" | "sensitive";
export type GroupMode = "none" | "date" | "source";
export type ViewMode = "compact" | "detail";

export const KIND_OPTIONS: { label: string; value: string }[] = [
  { label: "All types", value: "all" },
  { label: "Text", value: "text" },
  { label: "Link", value: "url" },
  { label: "GitHub PR", value: "github-pr" },
  { label: "Jira issue", value: "jira-issue" },
  { label: "Email", value: "email" },
  { label: "Color", value: "color" },
  { label: "Path", value: "path" },
  { label: "Code", value: "code" },
  { label: "Image", value: "image" },
];
