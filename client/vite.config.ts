import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // Dev-time convenience: forward API calls to the ASP.NET Core server.
      "/api": {
        target: "http://localhost:5080",
        changeOrigin: true,
      },
      "/health": {
        target: "http://localhost:5080",
        changeOrigin: true,
      },
    },
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    css: false,
  },
});
