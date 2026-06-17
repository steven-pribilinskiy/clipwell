import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";
import solid from "vite-plugin-solid";

// Built to ../daemon/wwwroot/app so the daemon can serve the SPA in-browser at /app.
// The base is relative so the same bundle works under /app and in the Tauri webview.
export default defineConfig({
  base: "./",
  plugins: [solid(), tailwindcss()],
  build: {
    // Single output consumed by both the daemon (serves it at /app — it resolves
    // <repo>/webui/dist) and Tauri (frontendDist: ../dist).
    outDir: "dist",
    emptyOutDir: true,
    target: "esnext",
  },
  server: {
    port: 1420,
    strictPort: true,
  },
});
