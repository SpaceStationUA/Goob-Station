/**
 * DOM-level checks for the galaxy chart.
 *
 * `npm run check` covers geometry, data and the source contract. None of it can
 * see the view layer, and that is where the worst bug lived: every marker was
 * frozen at the pixel position it got for the initial 1200x700 default, because
 * Solid runs a <For>/<Show> child body inside untrack() and the transform had
 * been read into a local there. Every geometry check passed, the chart looked
 * correct at exactly one window size, and resizing or browser-zooming made the
 * stars drift out of their countries. `getScreenCTM()` compounded it, since that
 * matrix includes the document zoom.
 *
 * So these assertions drive a real browser and compare what the page DREW
 * against what it should have drawn. That catches the whole family: frozen
 * geometry, mis-scaled pointer mapping, and highlight lag.
 *
 * Browser zoom is emulated the way Cmd+/- actually behaves — by changing the
 * size of the CSS viewport. Setting deviceScaleFactor does NOT do this; it only
 * changes DPR, which is why an earlier attempt at reproducing the zoom report
 * saw three identical results.
 *
 * Needs `npm i -D playwright-core` and a browser. Skips cleanly without them.
 */
process.env.TUI_IFACE = "GalaxyMap";

let chromium;
try {
  ({ chromium } = await import("playwright-core"));
} catch {
  console.log("\ncheck:dom skipped — playwright-core not installed (npm i -D playwright-core)\n");
  process.exit(0);
}

const { createServer } = await import("vite");

const PORT = 5199;

const vite = await createServer({
  // Bind explicitly. Vite's default host resolves `localhost`, which on a
  // dual-stack machine is ::1 while Playwright dials 127.0.0.1.
  server: { host: "127.0.0.1", port: PORT, strictPort: true },
  logLevel: "error",
});
await vite.listen();
const BASE = vite.resolvedUrls?.local?.[0] ?? `http://127.0.0.1:${PORT}`;
console.log(`check:dom — serving ${BASE}`);

let failures = 0;
function check(name, ok, detail = "") {
  console.log(`  ${ok ? "ok  " : "FAIL"}  ${name}${detail ? ` — ${detail}` : ""}`);
  if (!ok) failures++;
}

/**
 * Resolve a browser, degrading to a skip rather than an error.
 *
 * Playwright's bundled Chromium is keyed to the driver version, so a machine
 * whose browser cache came from a different release has a download the driver
 * will not accept. Falling back to the system Chrome covers that, and skipping
 * covers a machine with neither — a check that cannot run should say so, not
 * take the whole suite down.
 */
async function launchBrowser() {
  const env = process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE;
  const attempts = [env ? { executablePath: env } : {}, { channel: "chrome" }, { channel: "msedge" }];
  for (const opts of attempts) {
    try {
      return await chromium.launch({ headless: true, ...opts });
    } catch {
      /* try the next one */
    }
  }
  console.log(
    "\ncheck:dom skipped — no usable browser. Run `npx playwright install chromium`,\n" +
      "or set PLAYWRIGHT_CHROMIUM_EXECUTABLE to a Chrome/Chromium binary.\n",
  );
  process.exit(0);
}

/** Sizes a browser zoom of each percentage produces, in CSS pixels. */
const ZOOMS = [
  { label: "100%", w: 1600, h: 1000 },
  { label: "125%", w: 1280, h: 800 },
  { label: "80%", w: 2000, h: 1250 },
];

const browser = await launchBrowser();

try {
  for (const zoom of ZOOMS) {
    const ctx = await browser.newContext({ viewport: { width: zoom.w, height: zoom.h } });
    const page = await ctx.newPage();
    const errors = [];
    page.on("pageerror", e => errors.push(e.message));
    await page.goto(BASE, { waitUntil: "load" });
    await page.waitForSelector("svg.chart");
    await page.waitForTimeout(250);

    console.log(`\nzoom ${zoom.label} (${zoom.w}x${zoom.h} css px):`);

    // --- markers track the live transform, not a stale one ------------------
    // Capitals are the only circles drawn at r=10, and their stroke is the owner
    // territory's colour, which gives us both a handle on each marker and the
    // identity of the nation it belongs to.
    const marks = await page.evaluate(() => {
      const svg = document.querySelector("svg.chart");
      const w = +svg.getAttribute("width");
      const h = +svg.getAttribute("height");
      const pad = 56;
      const scale = Math.min((w - pad * 2) / 132, (h - pad * 2) / 74);
      const toPx = (lx, ly) => ({ x: lx * scale + w / 2, y: ly * scale + h / 2 });

      const rings = [...svg.querySelectorAll("circle")]
        .filter(c => c.getAttribute("r") === "10")
        .map(c => ({
          x: +c.getAttribute("cx"),
          y: +c.getAttribute("cy"),
          owner: c.getAttribute("stroke"),
        }));

      // Expected on-screen position of every capital, from the model data the
      // page was given. Sol is a capital at (-2, 11) LY.
      const sol = toPx(-2, 11);
      let solErr = Infinity;
      for (const r of rings) solErr = Math.min(solErr, Math.hypot(r.x - sol.x, r.y - sol.y));

      // For each ring, which flat fill paths contain its centre?
      const flats = [...svg.querySelectorAll('path[fill-rule="evenodd"]')].filter(
        p => !(p.getAttribute("fill") ?? "").startsWith("url("),
      );
      const contained = rings.map(r => {
        const hits = flats.filter(p => {
          try {
            return p.isPointInFill(new DOMPoint(r.x, r.y));
          } catch {
            return false;
          }
        });
        return { owner: r.owner, insideOwnTerritory: hits.some(p => p.getAttribute("fill") === r.owner) };
      });

      return { scale, solErr, rings: rings.length, contained };
    });

    check(
      "star markers track the live transform",
      marks.solErr < 2,
      `Sol off by ${marks.solErr.toFixed(1)}px across ${marks.rings} capitals`,
    );

    const strays = marks.contained.filter(c => !c.insideOwnTerritory);
    check(
      "every capital is drawn inside its own territory",
      strays.length === 0,
      strays.length ? `${strays.length} outside: ${strays.map(s => s.owner).join(", ")}` : `${marks.contained.length} checked`,
    );

    // --- the highlight follows the pointer ----------------------------------
    const seen = new Set();
    for (const [fx, fy] of [
      [0.5, 0.5],
      [0.3, 0.4],
      [0.7, 0.6],
      [0.4, 0.7],
    ]) {
      await page.mouse.move(zoom.w * fx, zoom.h * fy);
      await page.waitForTimeout(40);
      const at = await page.evaluate(() => {
        const h = document.querySelector(".cell-hover");
        if (!h || h.classList.contains("off")) return null;
        const b = h.getBoundingClientRect();
        return `${(b.x + b.width / 2).toFixed(1)},${(b.y + b.height / 2).toFixed(1)}`;
      });
      if (at) seen.add(at);
    }
    check("hover highlight moves with the pointer", seen.size >= 3, `${seen.size} distinct cells`);

    await page.mouse.move(zoom.w * 0.45, zoom.h * 0.55);
    await page.waitForTimeout(60);
    const under = await page.evaluate(() => {
      const h = document.querySelector(".cell-hover");
      const b = h.getBoundingClientRect();
      return { cx: b.x + b.width / 2, cy: b.y + b.height / 2 };
    });
    const hexR = 2 * marks.scale;
    const off = Math.hypot(under.cx - zoom.w * 0.45, under.cy - zoom.h * 0.55);
    check(
      "hover highlight is the cell under the cursor",
      off < hexR,
      `off by ${off.toFixed(1)}px, hex radius ${hexR.toFixed(1)}px`,
    );

    // --- brushes: nation, unclaim, contest ---------------------------------
    await page.mouse.click(zoom.w * 0.45, zoom.h * 0.55);
    await page.waitForTimeout(120);
    const panel = await page.evaluate(() => document.querySelector(".panel")?.textContent ?? null);
    check("clicking a cell selects its territory", panel !== null, panel?.slice(0, 32));

    const armed = await page.evaluate(async () => {
      const swatches = [...document.querySelectorAll(".toolbar .swatch")];
      swatches[0].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      return document.querySelector(".armed-note")?.textContent ?? "";
    });
    check("a nation brush arms and says what it will do", /PAINTING/i.test(armed), armed.slice(0, 34));

    const svgBox = await page.evaluate(() => {
      const s = document.querySelector("svg.chart");
      const r = s.getBoundingClientRect();
      return { x: r.x, y: r.y, w: r.width, h: r.height };
    });
    await page.mouse.click(svgBox.x + svgBox.w * 0.45, svgBox.y + svgBox.h * 0.55);
    await page.waitForTimeout(150);
    const undo = await page.evaluate(() =>
      [...document.querySelectorAll(".toolbar button")].some(b => b.textContent.includes("UNDO")),
    );
    check("painting a cell records an undo step", undo);

    const contest = await page.evaluate(async () => {
      const swatches = [...document.querySelectorAll(".toolbar .swatch")];
      swatches[swatches.length - 2].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      return document.querySelector(".armed-note")?.textContent ?? "";
    });
    check("contest brush arms", /CONTESTED/i.test(contest), contest.slice(0, 34));

    const uncontest = await page.evaluate(async () => {
      const swatches = [...document.querySelectorAll(".toolbar .swatch")];
      swatches[swatches.length - 1].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      return {
        note: document.querySelector(".armed-note")?.textContent ?? "",
        // The two dispute brushes must be distinguishable, or arming one while
        // the other is held silently re-arms the wrong behaviour.
        onlyOneArmed: document.querySelectorAll(".toolbar .swatch.on").length,
      };
    });
    check(
      "uncontest brush arms and is distinct from contest",
      /CONTESTED/i.test(uncontest.note) && uncontest.onlyOneArmed === 1,
      `${uncontest.note.slice(0, 30)}, ${uncontest.onlyOneArmed} armed`,
    );

    // Contesting then un-contesting the same cell must be a round trip.
    const roundTrip = await page.evaluate(async () => {
      const layer = () =>
        (document.querySelector(".contested-layer path")?.getAttribute("d") ?? "").split("M")
          .length - 1;
      const swatches = [...document.querySelectorAll(".toolbar .swatch")];
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      const at = { x: r.x + r.width * 0.42, y: r.y + r.height * 0.5 };
      const click = () =>
        svg.dispatchEvent(
          new MouseEvent("click", { bubbles: true, clientX: at.x, clientY: at.y }),
        );
      const wait = () => new Promise(ok => setTimeout(ok, 200));

      swatches[swatches.length - 2].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await wait();
      const before = layer();
      click();
      await wait();
      const afterMark = layer();
      swatches[swatches.length - 1].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await wait();
      click();
      await wait();
      const afterClear = layer();
      return { before, afterMark, afterClear, note: (document.querySelector(".armed-note")?.textContent ?? "(none)").slice(0, 24) };
    });
    check(
      "contesting a cell adds it and un-contesting removes it",
      roundTrip.afterMark === roundTrip.before + 1 && roundTrip.afterClear === roundTrip.before,
      `${roundTrip.before} -> ${roundTrip.afterMark} -> ${roundTrip.afterClear} [${roundTrip.note}]`,
    );

    // --- drag to paint ------------------------------------------------------
    const undoCount = () =>
      page.evaluate(() => {
        const b = [...document.querySelectorAll(".toolbar button")].find(x =>
          x.textContent.includes("UNDO"),
        );
        return b ? Number(b.textContent.replace(/\D+/g, "")) || 0 : 0;
      });

    // Put the brush down first, then pick the nation one. Clicking an already
    // armed swatch disarms it — that is what a toggle is for — so arming
    // "only if empty" would leave whatever the previous step happened to be
    // holding, and a drag with the wrong brush is correctly a no-op.
    const armedNation = await page.evaluate(async () => {
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      const sw = [...document.querySelectorAll(".toolbar .swatch")];
      sw[0].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      return document.querySelector(".armed-note")?.textContent ?? "";
    });
    check("a nation brush is armed before dragging", /PAINTING/i.test(armedNation), armedNation.slice(0, 30));

    // One drag: press, a few moves, release. Then assert the pending preview
    // covered MORE cells than there were move events — that is the observable
    // signature of line interpolation, and its absence is what makes a fast
    // drag come out dashed.
    const MOVES = 4;
    const editsBefore = await undoCount();
    const drag = await page.evaluate(async (moves) => {
      const wait = (ms = 200) => new Promise(ok => setTimeout(ok, ms));
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      const fire = (type, fx, fy, button) =>
        svg.dispatchEvent(
          new MouseEvent(type, {
            bubbles: true,
            button: button ?? 0,
            clientX: r.x + r.width * fx,
            clientY: r.y + r.height * fy,
          }),
        );
      const pendingCells = () => {
        const d = document.querySelector(".stroke-pending path")?.getAttribute("d") ?? "";
        return (d.match(/M/g) ?? []).length;
      };

      fire("mousedown", 0.30, 0.30, 0);
      await wait(30);
      const afterPress = pendingCells();
      for (let i = 1; i <= moves; i++) {
        fire("mousemove", 0.30 + 0.03 * i, 0.30 + 0.014 * i);
        await wait(30);
      }
      const beforeRelease = pendingCells();
      fire("mouseup", 0.30 + 0.03 * moves, 0.30 + 0.014 * moves);
      await wait(200);
      return { afterPress, beforeRelease, afterRelease: pendingCells() };
    }, MOVES);

    check("pressing starts a stroke on the cell under the pointer", drag.afterPress === 1, `${drag.afterPress} cells`);
    check(
      "a drag covers more cells than it has move events — no gaps",
      drag.beforeRelease > MOVES,
      `${drag.beforeRelease} cells from ${MOVES} moves`,
    );
    check("the preview is cleared on release", drag.afterRelease === 0, `${drag.afterRelease} cells`);

    const editsAfter = await undoCount();
    check(
      "a whole drag is ONE undo step, not one per cell",
      editsAfter === editsBefore + 1,
      `UNDO ${editsBefore} -> ${editsAfter}`,
    );

    // Escape mid-drag must abandon the stroke rather than commit half of it.
    const cancelled = await page.evaluate(async () => {
      const wait = (ms = 220) => new Promise(ok => setTimeout(ok, ms));
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      const undo = () => {
        const b = [...document.querySelectorAll(".toolbar button")].find(x =>
          x.textContent.includes("UNDO"),
        );
        return b ? Number(b.textContent.replace(/\D+/g, "")) || 0 : 0;
      };
      const fire = (type, fx, fy) =>
        svg.dispatchEvent(
          new MouseEvent(type, {
            bubbles: true,
            button: 0,
            clientX: r.x + r.width * fx,
            clientY: r.y + r.height * fy,
          }),
        );
      const before = undo();
      fire("mousedown", 0.6, 0.3, 0);
      await wait(30);
      for (let i = 1; i <= 3; i++) {
        fire("mousemove", 0.6 - 0.02 * i, 0.3 + 0.01 * i);
        await wait(30);
      }
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
      await wait(250);
      return { before, after: undo(), preview: (document.querySelector(".stroke-pending path")?.getAttribute("d") ?? "").length };
    });
    check(
      "escape abandons an in-progress stroke",
      cancelled.after === cancelled.before && cancelled.preview === 0,
      `UNDO ${cancelled.before} -> ${cancelled.after}`,
    );

    // --- admin vs player ----------------------------------------------------
    check("an admin still sees the brushes", await page.evaluate(() => !!document.querySelector(".paintpick")));

    // A player gets the same chart with the tools removed. Driven by flipping
    // the source's permission, because that is the one input the real build
    // varies: the shipped binary is identical for everyone and only the payload
    // differs.
    const player = await page.evaluate(async () => {
      const wait = (ms = 200) => new Promise(ok => setTimeout(ok, ms));
      window.__galaxySetPermission?.(false);
      await wait();
      const out = {
        supported: typeof window.__galaxySetPermission === "function",
        paint: !!document.querySelector(".paintpick"),
        undo: [...document.querySelectorAll(".toolbar button")].some(b => b.textContent.includes("UNDO")),
        // The chart itself must survive: a player still reads the map.
        stars: document.querySelectorAll("circle.system-label, text.system-label").length,
        labels: document.querySelectorAll(".terr-name").length,
        grid: !!document.querySelector(".toolbar button"),
      };
      return out;
    });
    check("the page can be told it is read-only", player.supported);
    check("a player gets no paint tools", !player.paint);
    check("a player gets no undo", !player.undo);
    check("a player still sees the place names", player.stars > 0, `${player.stars} labels`);
    check("a player still sees the territory names", player.labels > 0, `${player.labels} names`);
    check("a player keeps the grid toggle", player.grid);

    // And a click on a cell must still select, not silently do nothing.
    const playerClick = await page.evaluate(async () => {
      const wait = (ms = 200) => new Promise(ok => setTimeout(ok, ms));
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      svg.dispatchEvent(
        new MouseEvent("click", {
          bubbles: true,
          clientX: r.x + r.width * 0.45,
          clientY: r.y + r.height * 0.55,
        }),
      );
      await wait();
      return document.querySelector(".panel")?.textContent?.slice(0, 24) ?? null;
    });
    check("a player can still select a territory", playerClick !== null, playerClick ?? "no panel");

    await page.evaluate(async () => {
      window.__galaxySetPermission?.(true);
      await new Promise(r => setTimeout(r, 150));
    });

    // The locale toggle must actually translate the chrome and the content.
    const uk = await page.evaluate(async () => {
      const pick = [...document.querySelectorAll(".locpick button")].find(
        b => b.textContent.trim() === "УКР",
      );
      if (!pick) return { found: false };
      pick.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 150));
      return {
        found: true,
        grid: document.querySelector(".toolbar button")?.textContent?.trim() ?? "",
        paint: document.querySelector(".paintlabel")?.textContent?.trim() ?? "",
        legend: document.querySelector(".legend b")?.textContent?.trim() ?? "",
        // A territory title, which is content rather than chrome.
        territory: document.querySelector(".terr-name")?.textContent?.trim() ?? "",
        legendText: document.querySelector(".legend")?.textContent ?? "",
      };
    });
    check("the locale picker offers Ukrainian", uk.found);
    check("chrome is translated", uk.grid === "СІТКА" && uk.paint === "ФАРБА", `${uk.grid} / ${uk.paint}`);
    check("the chart title is translated", uk.legend === "РУКАВ ОРІОНА", uk.legend);
    check(
      "content names are translated",
      /[А-ЯІЇЄҐ]/.test(uk.territory),
      uk.territory,
    );
    check(
      "counts use Ukrainian units and plurals",
      /св\.р\./.test(uk.legendText) && !/ LY /.test(uk.legendText),
      uk.legendText.replace(/\s+/g, " ").slice(0, 64),
    );
    // Back to English, so the brushes below start from a known state.
    await page.evaluate(async () => {
      const pick = [...document.querySelectorAll(".locpick button")].find(
        b => b.textContent.trim() === "EN",
      );
      pick?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 120));
    });

    const overlay = await page.evaluate(() => {
      const layer = document.querySelector(".contested-layer path");
      return (layer?.getAttribute("d") ?? "").length;
    });
    check("contested ground is drawn on the chart", overlay > 0, `${overlay} chars of path`);

    check("no page errors", errors.length === 0, errors.join(" | "));
    await ctx.close();
  }
} finally {
  await browser.close();
  await vite.close();
}

console.log(`\n${failures === 0 ? "all dom checks passed" : `${failures} DOM CHECK(S) FAILED`}\n`);
process.exit(failures === 0 ? 0 : 1);
