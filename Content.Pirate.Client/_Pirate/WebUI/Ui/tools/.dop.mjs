process.env.TUI_IFACE = "GalaxyMap";
const { chromium } = await import("playwright-core");
const { createServer } = await import("vite");
const fs = await import("node:fs/promises");
const vite = await createServer({ server: { host: "127.0.0.1", port: 5515, strictPort: true }, logLevel: "error" });
await vite.listen();
const b = await chromium.launch({ headless: true, channel: "chrome", args: ["--use-gl=angle", "--enable-unsafe-swiftshader"] });
const p = await (await b.newContext({ viewport: { width: 1400, height: 700 }, deviceScaleFactor: 2 })).newPage();
await p.goto("http://127.0.0.1:5515/", { waitUntil: "load" });
await p.waitForSelector("svg.chart");
await p.waitForTimeout(500);
// Render the LIVE animated black hole twice, side by side, through the dev hook,
// so what is shown is what the chart draws.
await p.evaluate(() => {
  const host = document.createElement("div");
  host.id = "ab";
  host.style.cssText = "position:fixed;inset:0;z-index:99999;background:#0a0d14;display:flex;align-items:center;justify-content:center;gap:40px";
  document.body.appendChild(host);
  const mk = (doppler, label) => {
    const wrap = document.createElement("div");
    wrap.style.cssText = "display:flex;flex-direction:column;align-items:center;gap:6px";
    const c = document.createElement("canvas");
    c.width = 420; c.height = 420; c.style.cssText = "width:420px;height:420px";
    const ctx = c.getContext("2d");
    const g = window.__galaxyBlackHoleGL;
    const inst = g ? g({ seed: 7, px: 420, doppler, animate: false, time: 3.7 }) : null;
    if (inst) { ctx.drawImage(inst.canvas, 0, 0); wrap.appendChild(inst.canvas); }
    const t = document.createElement("div");
    t.textContent = label;
    t.style.cssText = "color:#cfd6e4;font:13px monospace";
    wrap.appendChild(t);
    return wrap;
  };
  host.appendChild(mk(0.0, "doppler OFF"));
  host.appendChild(mk(0.24, "doppler ON"));
});
await p.waitForTimeout(900);
await fs.writeFile("/tmp/dop.png", await (await p.$("#ab")).screenshot());
console.log("ok");
await b.close(); await vite.close();
