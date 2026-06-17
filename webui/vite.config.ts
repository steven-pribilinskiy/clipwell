import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";
import solid from "vite-plugin-solid";

// Built to ../daemon/wwwroot/app so the daemon can serve the SPA in-browser at /app.
// The base is relative so the same bundle works under /app and in the Tauri webview.
export default defineConfig({
  base: "./",
  plugins: [solid(), tailwindcss()],
  build: {
    outDir: "dist",
    emptyOutDir: true,
    target: "esnext",
  },
  server: {
    port: 1420,
    strictPort: true,
  },
});
