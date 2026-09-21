import path from "node:path";
import { defineConfig } from "vite";
import solid from "vite-plugin-solid";

// One build per interface; TUI_IFACE selects it (default "Uplink").
// TUI_OUT_DIR points at Resources/_Pirate/WebUI/<interface name>.
export default defineConfig(() => {
  const iface = process.env.TUI_IFACE ?? "Uplink";
  const root = path.resolve(__dirname, "src", iface);
  const outDir =
    process.env.TUI_OUT_DIR ?? path.resolve(__dirname, "dist_resources");
  return {
    base: "./",
    root,
    plugins: [solid()],
    build: {
      outDir,
      emptyOutDir: true,
    },
  };
});
