import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { createHash } from "node:crypto";
import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";

export default defineConfig({
  plugins: [
    react(),
    {
      name: "precache-application-shell",
      apply: "build",
      closeBundle() {
        const dist = resolve(import.meta.dirname, "dist");
        const assets = readdirSync(resolve(dist, "assets")).map((file) => `/assets/${file}`);
        const worker = readFileSync(resolve(import.meta.dirname, "public/sw.js"), "utf8");
        const version = createHash("sha256")
          .update(readFileSync(resolve(dist, "index.html")))
          .update(JSON.stringify(assets))
          .update(worker)
          .update(readFileSync(resolve(import.meta.dirname, "public/theme-init.js")))
          .update(readFileSync(resolve(import.meta.dirname, "public/icon.svg")))
          .update(readFileSync(resolve(import.meta.dirname, "public/icon-192.png")))
          .update(readFileSync(resolve(import.meta.dirname, "public/icon-512.png")))
          .update(readFileSync(resolve(import.meta.dirname, "public/manifest.webmanifest")))
          .digest("hex")
          .slice(0, 16);
        writeFileSync(
          resolve(dist, "sw.js"),
          `self.__PRECACHE_ASSETS__=${JSON.stringify(assets)};\nself.__SHELL_VERSION__=${JSON.stringify(version)};\n${worker}`,
        );
      },
    },
  ],
  server: {
    proxy: {
      "/api": "http://127.0.0.1:5080",
      "/hubs": { target: "http://127.0.0.1:5080", ws: true },
    },
  },
});
