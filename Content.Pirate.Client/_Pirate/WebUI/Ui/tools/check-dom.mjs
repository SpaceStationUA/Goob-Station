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

    // --- planet animation ---------------------------------------------------
    // Two properties have to hold, and the second is the one that cannot be seen
    // in a still: consecutive frames must differ, and a full turn must land
    // exactly back on the start.
    //
    // The obvious refinement — scaling the shift by cos(latitude), so the poles
    // hold still and the equator sweeps fastest — is NOT a rigid rotation. A
    // mid-latitude pixel advances by cos(lat) of a texture period instead of a
    // whole one, so the sequence drifts and never returns. An earlier version of
    // this check compared the last frame against the first, which passed with the
    // bug in place: those two are one step apart in the sequence either way, so
    // the metric could not see the drift. The direct property is that a full turn
    // reproduces the start pixel for pixel.
    const spin = await page.evaluate(async () => {
      document.querySelector(".spikebar button").click();
      await new Promise((ok) => setTimeout(ok, 250));
      const N = 12;
      const sheet = window.__galaxySheet({ seed: 0x5eed1, type: "terran", px: 48, dpr: 1 }, N);

      const img = new Image();
      img.src = sheet.uri;
      await img.decode();
      const cv = document.createElement("canvas");
      cv.width = img.width;
      cv.height = img.height;
      const cx = cv.getContext("2d");
      cx.drawImage(img, 0, 0);
      const d = sheet.framePx;
      const frame = (n) => cx.getImageData(n * d, 0, d, d).data;

      const decode = async (uri) => {
        const im = new Image();
        im.src = uri;
        await im.decode();
        const c2 = document.createElement("canvas");
        c2.width = im.width;
        c2.height = im.height;
        c2.getContext("2d").drawImage(im, 0, 0);
        return c2.getContext("2d").getImageData(0, 0, im.width, im.height).data;
      };
      const opts = { seed: 0x5eed1, type: "terran", px: 48, dpr: 1 };
      const start = await decode(window.__galaxyStill({ ...opts, spin: 0 }));
      const full = await decode(window.__galaxyStill({ ...opts, spin: 1 }));
      const half = await decode(window.__galaxyStill({ ...opts, spin: 0.5 }));

      const maxDiff = (a, b) => {
        let m = 0;
        for (let i = 0; i < a.length; i += 4) {
          m = Math.max(m, Math.abs(a[i] - b[i]), Math.abs(a[i + 1] - b[i + 1]), Math.abs(a[i + 2] - b[i + 2]));
        }
        return m;
      };
      const f0 = frame(0);
      const f1 = frame(1);
      let changed = 0;
      for (let i = 0; i < f0.length; i += 4) {
        if (Math.abs(f0[i] - f1[i]) > 8) changed++;
      }
      return {
        frames: N,
        size: [img.width, img.height],
        step: changed / (d * d),
        closure: maxDiff(start, full),
        halfTurn: maxDiff(start, half),
      };
    });

    check(
      "the sheet is a horizontal strip of frames",
      spin.size[0] === spin.size[1] * spin.frames,
      `${spin.size[0]}x${spin.size[1]} for ${spin.frames} frames`,
    );
    check(
      "consecutive frames differ, so it actually rotates",
      spin.step > 0.005,
      `${(spin.step * 100).toFixed(1)}% of pixels change per frame`,
    );
    check(
      "half a turn is visibly different from the start",
      spin.halfTurn > 24,
      `max channel delta ${spin.halfTurn}`,
    );
    check(
      "a full turn lands exactly back on the start, so the loop closes",
      spin.closure === 0,
      `max channel delta ${spin.closure} after 360 degrees`,
    );

    // --- the system overlay ------------------------------------------------
    // Clicking a star is the whole reason the overlay exists, so the things that
    // could quietly break it are pinned down here: which system opens, how big the
    // world is drawn, and that a marker click does not leak into the territory
    // panel underneath.
    //
    // The state arriving from the brushes block has a brush armed, which matters
    // twice over: an armed brush suppresses the marker hit targets entirely (so
    // the element these checks look for would not exist), and it takes priority
    // over a marker click. Both are asserted below rather than worked around.
    await page.keyboard.press("Escape");
    await page.waitForTimeout(120);
    check(
      "escape clears the brush, so markers are clickable again",
      (await page.evaluate(() => document.querySelector(".armed-note")?.textContent ?? "")) === "",
    );

    // Which marker, and where. A capital, so the ring and the widest hit radius
    // are both in play.
    const star = await page.evaluate(() => {
      for (const h of document.querySelectorAll(".sys-hit")) {
        if (document.querySelector(`text.system-label.capital[data-sys="${h.dataset.sys}"]`)) {
          const r = h.getBoundingClientRect();
          const own = document.querySelector(`text.system-label[data-sys="${h.dataset.sys}"]`);
          return {
            id: h.dataset.sys,
            name: own.textContent.trim(),
            x: r.x + r.width / 2,
            y: r.y + r.height / 2,
            r: +h.getAttribute("r"),
          };
        }
      }
      return null;
    });
    check("capitals expose a click target", star !== null, star?.name ?? "none found");

    await page.mouse.click(star.x, star.y);
    await page.waitForTimeout(200);
    const opened = await page.evaluate(() => ({
      overlay: document.querySelector(".overlay") !== null,
      title: document.querySelector(".overlay h2")?.textContent ?? "",
      territoryPanel: document.querySelector(".panel:not(.overlay)") !== null,
    }));
    check("clicking a star opens the overlay for that system", opened.overlay && opened.title === star.name,
      `${opened.title} (wanted ${star.name})`);
    check(
      "the territory panel is not also open — one panel at a time",
      !opened.territoryPanel,
    );

    // The point of the overlay. A map marker is 16-40px; anything near that here
    // would mean the overlay had quietly become the thing it replaced.
    const art = await page.evaluate(() => {
      const w = document.querySelector(".overlay .world");
      const img = document.querySelector(".overlay .world-still");
      if (!w) return null;
      const r = w.getBoundingClientRect();
      return {
        css: r.width,
        natural: img ? img.naturalWidth : 0,
        dpr: window.devicePixelRatio || 1,
        panel: document.querySelector(".overlay").getBoundingClientRect().width,
      };
    });
    check(
      "the overlay draws its world far larger than a map marker",
      art && art.css >= 150,
      art ? `${art.css}px css, ${art.natural}px generated, panel ${Math.round(art.panel)}px` : "no art",
    );
    check(
      "the world fits inside the panel",
      art && art.css <= art.panel,
      art ? `${art.css} vs ${Math.round(art.panel)}` : "",
    );
    // Against css * dpr, not against css. `natural >= css` passes trivially at
    // dpr 1, which is exactly where this suite runs — it would have gone green
    // against a sprite generated at half the display resolution, which is the
    // bug it exists to catch.
    check(
      "the still is generated at the display ratio, not upscaled",
      art && art.natural === Math.round(art.css * art.dpr),
      art ? `${art.natural}px generated for ${art.css} css @ dpr ${art.dpr}` : "",
    );

    // The close button, before anything else needs a closed panel.
    await page.click(".overlay-close");
    await page.waitForTimeout(150);
    check(
      "the close button dismisses the overlay",
      (await page.evaluate(() => document.querySelector(".overlay") === null)),
    );

    // The deferral. Generating 24 frames costs about half a second, so the panel
    // shows a finished still first and upgrades. If someone makes the strip
    // synchronous this goes red, which is the point: the regression is a frozen
    // page on click, and nothing else would notice it.
    //
    // Measured on a system this suite has NOT already opened. That is not a
    // detail: `planetSheet` caches, so re-opening the star above is a cache hit
    // and both the still and the strip land in the same frame. The property
    // being asserted is about a cold generate, so it has to use a cold system —
    // otherwise the check passes for the wrong reason and would not notice the
    // strip being made synchronous.
    const cold = await page.evaluate((used) => {
      for (const h of document.querySelectorAll(".sys-hit")) {
        if (h.dataset.sys !== used) return h.dataset.sys;
      }
      return null;
    }, star.id);
    check("found an unopened system to time", cold !== null, cold ?? "");
    // The close-button check above already left the panel shut, which is the
    // state this needs: the overlay toggles, so timing an open panel would time
    // the close. page.evaluate cannot close over `cold`, so it is passed in.
    const defer = await page.evaluate(async (sysId) => {
      const marks = [];
      const t0 = performance.now();
      const obs = new MutationObserver(() => {
        if (document.querySelector(".world-still") && !marks.some(m => m.k === "still"))
          marks.push({ k: "still", t: performance.now() - t0 });
        if (document.querySelector(".world.turning") && !marks.some(m => m.k === "strip"))
          marks.push({ k: "strip", t: performance.now() - t0 });
      });
      obs.observe(document.body, { childList: true, subtree: true, attributes: true });
      const h = document.querySelector('.sys-hit[data-sys="' + sysId + '"]');
      const r = h.getBoundingClientRect();
      // A real click on the svg, so the timed path is the one a player takes and
      // not a synthetic call into the handler.
      document.querySelector("svg.chart").dispatchEvent(
        new MouseEvent("click", {
          bubbles: true,
          clientX: r.x + r.width / 2,
          clientY: r.y + r.height / 2,
        }),
      );
      await new Promise(res => setTimeout(res, 3500));
      obs.disconnect();
      return marks;
    }, cold);
    const still = defer.find(m => m.k === "still");
    const strip = defer.find(m => m.k === "strip");
    check(
      "a world is on screen immediately, before any rotation is generated",
      still !== undefined,
      still ? `${Math.round(still.t)}ms` : "no still",
    );
    check(
      "the rotation is deferred rather than blocking the click",
      still !== undefined && strip !== undefined && strip.t > still.t + 50,
      still && strip ? `still ${Math.round(still.t)}ms, strip ${Math.round(strip.t)}ms` : "strip never arrived",
    );

    // Toggle-to-close, on whichever system is actually open. Clicking the
    // original capital here would be a different test: that one switches systems,
    // because the overlay moved to `cold` for the timing above.
    const openId = await page.evaluate((sysId) => {
      const h = document.querySelector('.sys-hit[data-sys="' + sysId + '"]');
      const t = document.querySelector(".overlay h2")?.textContent ?? "";
      if (!h) return null;
      const r = h.getBoundingClientRect();
      return { x: r.x + r.width / 2, y: r.y + r.height / 2, name: t };
    }, cold);
    check("the overlay is open on the system that was timed", openId !== null, openId?.name ?? "overlay not open on the timed system");
    if (openId) {
      await page.mouse.click(openId.x, openId.y);
      await page.waitForTimeout(150);
      check(
        "clicking the open system again closes the overlay",
        (await page.evaluate(() => document.querySelector(".overlay") === null)),
      );
    }

    // Open space must still select the territory underneath. This is the
    // regression a marker hit target invites: widen the radius far enough and
    // clicks that used to select a country start opening a planet instead.
    const openSpace = await page.evaluate(() => {
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      const hits = [...document.querySelectorAll(".sys-hit")].map(h => {
        const b = h.getBoundingClientRect();
        return { x: b.x + b.width / 2, y: b.y + b.height / 2, r: +h.getAttribute("r") };
      });
      // A grid of candidates; take the first that is clear of every hit radius.
      for (let fy = 0.2; fy <= 0.8; fy += 0.05) {
        for (let fx = 0.2; fx <= 0.8; fx += 0.05) {
          const x = r.x + r.width * fx;
          const y = r.y + r.height * fy;
          if (hits.every(h => Math.hypot(h.x - x, h.y - y) > h.r + 12))
            return { x, y, fx, fy };
        }
      }
      return null;
    });
    check("found open space well clear of every marker", openSpace !== null);
    if (openSpace) {
      await page.mouse.click(openSpace.x, openSpace.y);
      await page.waitForTimeout(150);
      const terr = await page.evaluate(() => ({
        panel: document.querySelector(".panel:not(.overlay)") !== null,
        overlay: document.querySelector(".overlay") !== null,
      }));
      check(
        "clicking open space still selects the territory, and opens no overlay",
        terr.panel && !terr.overlay,
      );
    }

    // An armed brush wins over a marker. An admin dragging a stroke across a
    // capital must not have that capital swallow the click and open a panel
    // mid-drag.
    await page.evaluate(() => {
      document.querySelector(".toolbar .swatch").dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });
    await page.waitForTimeout(120);
    await page.mouse.click(star.x, star.y);
    await page.waitForTimeout(180);
    const brushWins = await page.evaluate(() => ({
      overlay: document.querySelector(".overlay") !== null,
      undo: [...document.querySelectorAll(".toolbar button")].some(b => b.textContent.includes("UNDO")),
    }));
    check(
      "with a brush armed, a marker click paints instead of opening the overlay",
      !brushWins.overlay && brushWins.undo,
      `overlay ${brushWins.overlay}, undo ${brushWins.undo}`,
    );
    await page.keyboard.press("Escape");
    await page.waitForTimeout(120);

    // --- how a world is drawn ---------------------------------------------
    // The banding is the look. Both of these guard properties that a change
    // could quietly remove while every screenshot still looked plausible.
    const look = await page.evaluate(async () => {
      const decode = async (uri) => {
        const im = new Image();
        im.src = uri;
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        return { data: cx.getImageData(0, 0, im.width, im.height), w: im.width, h: im.height };
      };
      const opts = { seed: 0x5eed1, px: 128, dpr: 1 };
      const world = await decode(window.__galaxyStill({ ...opts, type: "terran" }));
      const star = await decode(window.__galaxyStill({ ...opts, type: "star" }));

      // Distinct opaque colours. A smooth-shaded sphere produces tens of
      // thousands of them; a dithered banded one produces a small palette plus
      // one dither pixel per blend.
      const palette = (img) => {
        const set = new Set();
        for (let i = 0; i < img.data.data.length; i += 4) {
          if (img.data.data[i + 3] > 0)
            set.add((img.data.data[i] << 16) | (img.data.data[i + 1] << 8) | img.data.data[i + 2]);
        }
        return set.size;
      };
      // Corner transparency: a planet is exactly its disc, a star bleeds.
      // Two different probes, because the two claims are about different radii.
      //
      // A planet's disc has radius d/2 and a star's has radius d/3 with its
      // corona reaching d/2, so the only place that is outside one and inside the
      // other is the annulus between d/3 and d/2 — 6% in from the top edge. The
      // box CORNER is at d/sqrt(2), outside everything including the corona, so
      // it is transparent for every type and proves nothing about either claim.
      const cornerAlpha = (img) => img.data.data[3];
      const annulusAlpha = (img) => {
        const i = (Math.round(img.h * 0.06) * img.w + Math.round(img.w / 2)) * 4;
        return img.data.data[i + 3];
      };
      const centreAlpha = (img) => {
        const i = ((img.h / 2) * img.w + img.w / 2) * 4;
        return img.data.data[i + 3];
      };
      return {
        px: world.w * world.h,
        worldColours: palette(world),
        starColours: palette(star),
        worldCorner: cornerAlpha(world),
        starEdge: annulusAlpha(star),
        worldEdge: annulusAlpha(world),
        worldCentre: centreAlpha(world),
        starCentre: centreAlpha(star),
      };
    });

    /**
     * Craters.
     *
     * The obvious check — count dark blobs on the lit side — does not work, and
     * two attempts are worth recording. Counting pixels darker than a smoothed
     * copy of themselves gave barren 208 cratered versus 208 uncratered, because
     * at the time a 128px sprite drew no craters at all; the frequency curve
     * clamped to two cells across the whole sphere and the product field never
     * crossed the bowl threshold. After fixing that, the ratio only reached 1.1,
     * and on lava it went BELOW 1.0 — a crater darkens a dark patch less than it
     * darkens a bright one, so "how many dark pixels" is not a function of "how
     * many craters".
     *
     * So this measures the difference between the two renders directly, and the
     * control is exact rather than statistical: a world with an atmosphere has no
     * craters to suppress, so suppressing them must change NOTHING, byte for
     * byte. If the flag were ignored, or the cache key missed it, or the renderer
     * were nondeterministic, that assertion fails — and it is the assertion that
     * makes the other two mean anything.
     */
    const crat = await page.evaluate(async () => {
      const px = async (opts) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed: 0x5eed1, px: 128, dpr: 1, ...opts });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        return cx.getImageData(0, 0, im.width, im.height).data;
      };
      const lum = (d, i) => 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
      const compare = async (type) => {
        const on = await px({ type });
        const off = await px({ type, suppressCraters: true });
        let changed = 0;
        let darker = 0;
        let opaque = 0;
        for (let i = 0; i < on.length; i += 4) {
          if (on[i + 3] > 0) opaque++;
          if (on[i] === off[i] && on[i + 1] === off[i + 1] && on[i + 2] === off[i + 2]) continue;
          changed++;
          if (lum(on, i) < lum(off, i) - 4) darker++;
        }
        return { changed, darker, opaque, total: on.length / 4 };
      };
      return {
        barren: await compare("barren"),
        lava: await compare("lava"),
        terran: await compare("terran"),
      };
    });

    /**
     * Ice world melt lakes, and specifically that they FRAGMENT the water.
     *
     * This asserts on water AREA, and the choice was made by measurement rather
     * than by taste. Counting connected bodies was the obvious thing to check and
     * it is not reliable: across five seeds the body count went 9/7, 7/8, 17/11,
     * 38/17, 76/6 — and on one seed it went DOWN, because where the main field
     * already has water the ponds merge into it instead of adding to the count.
     * Water area is unambiguous in every case: 20x, 200x, 1.3x, 2.7x and 27x, so
     * the threshold is set at 1.2x, which the weakest seed still clears.
     *
     * So the fragmentation claim — which is the visually interesting one — is NOT
     * asserted. It is real and visible; it is just not a property that holds
     * across seeds, and a check that fails on a legitimate seed is worse than no
     * check.
     *
     * The water test is a luminance threshold, which only works on the LIT side,
     * so the region is restricted to the upper left where the light is. The
     * terminator drags shaded ice down below the water's own luminance, and a
     * threshold applied to the whole disc would therefore count the night half as
     * a lake.
     */
    const lake = await page.evaluate(async () => {
      const bodies = async (opts) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed: 0x5eed1, type: "ice", px: 128, dpr: 1, ...opts });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        const d = cx.getImageData(0, 0, im.width, im.height).data;
        const w = im.width;
        const h = im.height;
        // Ice tones are all above 190 in luminance; water is below 175. Nothing
        // sits between, which is what makes a threshold usable at all here.
        const wet = new Uint8Array(w * h);
        let wetPx = 0;
        for (let y = 0; y < Math.round(h * 0.5); y++) {
          for (let x = 0; x < Math.round(w * 0.6); x++) {
            const i = (y * w + x) * 4;
            if (d[i + 3] === 0) continue;
            const l = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
            if (l < 185) {
              wet[y * w + x] = 1;
              wetPx++;
            }
          }
        }
        // Flood fill, 4-connected.
        let n = 0;
        const stack = [];
        for (let k = 0; k < wet.length; k++) {
          if (!wet[k]) continue;
          n++;
          stack.push(k);
          wet[k] = 0;
          while (stack.length) {
            const p = stack.pop();
            const x = p % w;
            const y = (p - x) / w;
            if (x > 0 && wet[p - 1]) (wet[p - 1] = 0), stack.push(p - 1);
            if (x < w - 1 && wet[p + 1]) (wet[p + 1] = 0), stack.push(p + 1);
            if (y > 0 && wet[p - w]) (wet[p - w] = 0), stack.push(p - w);
            if (y < h - 1 && wet[p + w]) (wet[p + w] = 0), stack.push(p + w);
          }
        }
        return { bodies: n, wetPx };
      };
      const px = async (opts) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed: 0x5eed1, type: "terran", px: 128, dpr: 1, ...opts });
        await im.decode();
        return im.src;
      };
      const on = await bodies({});
      const off = await bodies({ suppressLakes: true });
      return {
        on: on.bodies,
        off: off.bodies,
        wetOn: on.wetPx,
        wetOff: off.wetPx,
        // The control: a world with no lakes to suppress must be untouched.
        terranSame: (await px({})) === (await px({ suppressLakes: true })),
      };
    });

    /**
     * Ring systems.
     *
     * Three things have to hold at once and none of them is implied by the other
     * two. There is a far half and a near half — one ring drawn as a single path
     * still looks like a ring, which is why a ring that lost its far side went
     * unnoticed until a path count gave it away. The far half comes BEFORE the
     * image and the near half AFTER, because the sprite does the occluding: it is
     * opaque across the disc and transparent outside it, so the far half is hidden
     * exactly where the planet is. And the whole thing has to FIT the overlay
     * panel — the first ratio put the ring at 330px and it was clipped at both
     * edges, which reads as a rendering fault rather than as a crop.
     *
     * The control is a world with no rings, which must contribute no paths at all.
     */
    const ring = await page.evaluate(async () => {
      await new Promise((r) => setTimeout(r, 50));
      const btn = [...document.querySelectorAll("button")].find((b) =>
        /ON MAP/.test(b.textContent ?? ""),
      );
      if (btn && !/ON$/.test(btn.textContent.trim())) btn.click();
      await new Promise((r) => setTimeout(r, 900));
      const far = [...document.querySelectorAll(".saturn-far")];
      const near = [...document.querySelectorAll(".saturn-near")];
      let ordered = far.length > 0;
      let nonEmpty = far.length > 0;
      // Sibling navigation, and NOT parentElement.querySelector. Every system
      // marker in the chart is a sibling in ONE <g> — Solid's <For> does not wrap
      // a per-item fragment — so a parent-scoped query returns the first image on
      // the chart rather than this marker's, and the check reports that the ring is
      // behind its planet when the planet is sixty systems away.
      for (const f of far) {
        if (!(f.getAttribute("d") ?? "")) nonEmpty = false;
        const next = f.nextElementSibling;
        if (!next || next.tagName !== "image" || !next.classList.contains("planet-mark")) {
          ordered = false;
        }
      }
      for (const n of near) {
        if (!(n.getAttribute("d") ?? "")) nonEmpty = false;
        const prev = n.previousElementSibling;
        if (!prev || prev.tagName !== "image" || !prev.classList.contains("planet-mark")) {
          ordered = false;
        }
      }
      // Open the ringed system's overlay and measure the ring against the panel.
      const id = far[0]?.getAttribute("data-saturn");
      let fits = null;
      let panel = null;
      if (id) {
        const hit = document.querySelector(`.sys-hit[data-sys="${id}"]`);
        if (hit) {
          const r = hit.getBoundingClientRect();
          hit.dispatchEvent(
            new MouseEvent("click", {
              bubbles: true,
              clientX: r.x + r.width / 2,
              clientY: r.y + r.height / 2,
            }),
          );
          await new Promise((r2) => setTimeout(r2, 1400));
          const svgs = [...document.querySelectorAll(".overlay .world-ring")];
          const box = document.querySelector(".overlay");
          if (svgs.length === 2 && box) {
            const bw = box.getBoundingClientRect().width;
            const widest = Math.max(...svgs.map((s) => s.getBoundingClientRect().width));
            fits = widest;
            panel = bw;
          }
          document.querySelector(".overlay-close")?.dispatchEvent(
            new MouseEvent("click", { bubbles: true }),
          );
        }
      }
      return {
        far: far.length,
        near: near.length,
        ordered,
        nonEmpty,
        fits,
        panel,
      };
    });

    /**
     * A black hole is a landmark, and a landmark that is not drawn is the worst
     * failure this chart has.
     *
     * The first version was a `<div>` with `<div>` children, mounted inside the
     * chart's `<g>`. HTML does not render inside SVG, so every box measured 0x0,
     * no error was raised, and the marker was simply absent — at the one size where
     * it most needed to be seen. `drawn` below is the check for exactly that, and it
     * is worth having precisely because the failure was silent: a screenshot of the
     * area would have shown empty space and looked like a placement problem.
     *
     * The two legibility assertions are the reason the horizon is not `#000`. The
     * page behind the chart is near black, so a true black disc is a hole in the
     * chart rather than an object in it, and the only thing that makes a black hole
     * readable is the light around it. Both are measured on the actual attributes
     * rather than on a screenshot.
     */
    const bh = await page.evaluate(async () => {
      const g = document.querySelector("[data-bh]");
      if (!g) return null;
      const r = g.getBoundingClientRect();
      const horizon = g.querySelector(".bh-horizon");
      const photon = g.querySelector(".bh-photon");
      return {
        drawn: r.width > 8 && r.height > 4,
        w: Math.round(r.width),
        h: Math.round(r.height),
        horizonFill: horizon?.getAttribute("fill") ?? "",
        photonStroke: photon?.getAttribute("stroke") ?? "",
        horizonR: Number(horizon?.getAttribute("r") ?? 0),
        photonR: Number(photon?.getAttribute("r") ?? 0),
        discPaths: g.querySelectorAll("path").length,
        // A black hole has no surface, so it must not be given a generated sprite.
        sprite: g.querySelectorAll("image").length,
        labelClearance: (() => {
          const t = document.querySelector('.system-label[data-sys="the-crow"]');
          if (!t) return null;
          const tb = t.getBoundingClientRect();
          return Math.round(tb.x - (r.x + r.width / 2));
        })(),
      };
    });

    check("a black hole is actually drawn on the chart", bh !== null && bh.drawn,
      bh === null ? "no [data-bh] marker in the DOM" : `box ${bh.w}x${bh.h}px`);
    check("and it has a horizon, a photon ring and both disc halves",
      bh !== null && bh.horizonR > 0 && bh.photonR > bh.horizonR && bh.discPaths === 2,
      bh === null ? "no marker" : `horizon r=${bh.horizonR}, photon r=${bh.photonR}, ${bh.discPaths} disc paths`);
    check("the horizon is not pure black, or it is a hole in the chart rather than an object",
      bh !== null && bh.horizonFill.toLowerCase() !== "#000" && bh.horizonFill.toLowerCase() !== "#000000",
      bh === null ? "no marker" : `fill ${bh.horizonFill}`);
    // Parsed out here rather than in the page: a regular expression with escaped
    // brackets inside a serialised evaluate callback is a syntax error waiting to
    // happen, and the failure mode is the check silently not existing.
    const lumOf = (css) => {
      const hex = /^#([0-9a-f]{6})$/i.exec(css ?? "");
      if (hex) {
        const n = parseInt(hex[1], 16);
        return 0.299 * ((n >> 16) & 255) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255);
      }
      const rgb = /rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)/.exec(css ?? "");
      return rgb ? 0.299 * +rgb[1] + 0.587 * +rgb[2] + 0.114 * +rgb[3] : null;
    };
    check(
      "and the photon ring is brighter than the horizon it outlines",
      bh !== null && (lumOf(bh.photonStroke) ?? 0) > (lumOf(bh.horizonFill) ?? 0),
      bh === null
        ? "no marker"
        : `photon ${bh.photonStroke} (lum ${Math.round(lumOf(bh.photonStroke) ?? 0)}) vs ` +
          `horizon ${bh.horizonFill} (lum ${Math.round(lumOf(bh.horizonFill) ?? 0)})`,
    );
    check("a black hole gets no generated sprite — it has no surface",
      bh !== null && bh.sprite === 0,
      bh === null ? "no marker" : `${bh.sprite} images inside the marker`);
    check("and its label clears the accretion disc, not just the marker box",
      bh !== null && bh.labelClearance !== null && bh.labelClearance > bh.w / 2,
      bh === null || bh.labelClearance === null
        ? "no label"
        : `label ${bh.labelClearance}px from centre, disc reaches ${Math.round(bh.w / 2)}px`);

    check(
      "a ringed system draws a far half and a near half",
      ring.far === 1 && ring.near === 1,
      `${ring.far} far, ${ring.near} near`,
    );
    check(
      "the sprite occludes: far half before the image, near half after",
      ring.ordered && ring.nonEmpty,
      `ordering ${ring.ordered}, both halves have geometry ${ring.nonEmpty}`,
    );
    check(
      "and the ring fits inside the overlay panel",
      ring.fits !== null && ring.fits <= ring.panel,
      ring.fits === null ? "no ring measured" : `ring ${Math.round(ring.fits)}px in a ${Math.round(ring.panel)}px panel`,
    );
    check(
      "a world with no rings contributes no ring geometry",
      await page.evaluate(() => {
        // Every marker that is not ringed must have no ring path of its own.
        return document.querySelectorAll(".saturn-far").length === document.querySelectorAll(
          ".saturn-near",
        ).length;
      }),
      "far and near counts agree",
    );

    check(
      "an ice world has melt water on its sheet, not just one continent-sized ocean",
      lake.wetOn > lake.wetOff * 1.2,
      `wet pixels ${lake.wetOn} with the second field, ${lake.wetOff} without ` +
        `(${lake.on} vs ${lake.off} separate bodies, reported but not asserted)`,
    );
    check(
      "and a world with no lakes is untouched by the flag — the control",
      lake.terranSame,
      lake.terranSame ? "byte-identical" : "terran changed, so the flag is not inert",
    );

    check(
      "an airless world draws craters",
      crat.barren.changed > crat.barren.total * 0.01 && crat.lava.changed > crat.lava.total * 0.01,
      `barren ${crat.barren.changed}/${crat.barren.total}px, lava ${crat.lava.changed}/${crat.lava.total}px`,
    );
    // Barren only, and the exclusion is the point rather than an oversight. A
    // crater legitimately contains BOTH a shadowed floor and a lit rim, so the
    // sign of the change is a property of the type's palette and not of craters:
    // on grey rock the floor dominates and 99% of changed pixels go darker, while
    // on a lava world the rim is molten rock and is the brightest thing on the
    // planet, which puts this at 46%. Asserting "darker" for both was asserting a
    // fact about grey.
    check(
      "and on grey rock a crater is a shadow, not a highlight",
      crat.barren.darker / crat.barren.changed > 0.9,
      `${Math.round((100 * crat.barren.darker) / crat.barren.changed)}% of changed pixels went darker ` +
        `(lava ${Math.round((100 * crat.lava.darker) / crat.lava.changed)}%, and that is correct: its rim is molten)`,
    );
    check(
      "a world with an atmosphere is untouched by the flag — the control, without which the two above mean nothing",
      crat.terran.changed === 0,
      `${crat.terran.changed} pixels changed on a world with no craters to suppress`,
    );

    /**
     * An asteroid is not a disc.
     *
     * Measured as the spread of the outline's radius with angle. A circle gives
     * a spread of zero by definition, so this cannot pass for a round sprite
     * however it is shaded, and it does not care what the rock is made of — the
     * first version of this idea counted dark patches, which a crater also
     * produces and which therefore could not tell a lumpy outline from a smooth
     * one with holes in it.
     *
     * `terran` is the control, and it is a fair one: same renderer, same lighting
     * path, same dither, and a perfectly circular outline. If the measurement were
     * picking up the dither pattern or the banding, it would fire on terran too.
     */
    const outline = await page.evaluate(async () => {
      const spread = async (type) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed: 0x5eed1, type, px: 160, dpr: 1 });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        const d = cx.getImageData(0, 0, im.width, im.height).data;
        const w = im.width;
        const h = im.height;
        const N = 180;
        const radii = [];
        for (let k = 0; k < N; k++) {
          const a = (k / N) * Math.PI * 2;
          const ca = Math.cos(a);
          const sa = Math.sin(a);
          let last = 0;
          // Walk out to the box edge. An earlier version stopped at 0.9, which
          // silently clipped the measurement: a rock wider than that would have
          // reported the same radius as one exactly at the limit, and the
          // standard deviation — the only number this check reads — would have
          // been computed from truncated data without saying so.
          for (let t = 1; t <= 100; t++) {
            const r = t / 100;
            const x = Math.round(w / 2 + ca * r * w * 0.5);
            const y = Math.round(h / 2 + sa * r * h * 0.5);
            if (x < 0 || y < 0 || x >= w || y >= h) break;
            if (d[(y * w + x) * 4 + 3] > 0) last = r;
          }
          radii.push(last);
        }
        const mean = radii.reduce((a, b) => a + b, 0) / radii.length;
        const sd = Math.sqrt(radii.reduce((a, b) => a + (b - mean) ** 2, 0) / radii.length);
        return { mean, sd, min: Math.min(...radii), max: Math.max(...radii) };
      };
      return { asteroid: await spread("asteroid"), terran: await spread("terran") };
    });

    check(
      "an asteroid's outline is lumpy, not a circle",
      outline.asteroid.sd > 0.05,
      `radius ${outline.asteroid.mean.toFixed(2)} ± ${outline.asteroid.sd.toFixed(3)} ` +
        `(range ${outline.asteroid.min.toFixed(2)}–${outline.asteroid.max.toFixed(2)})`,
    );
    check(
      "and a world's outline still is — the control",
      outline.terran.sd < 0.02,
      `radius ${outline.terran.mean.toFixed(2)} ± ${outline.terran.sd.toFixed(3)}`,
    );

    /**
     * Gas giant bands run east-west.
     *
     * Measured as the ratio of vertical to horizontal colour change. Bands of
     * constant latitude change fast as you move up the disc and slowly as you move
     * across it, so the ratio is well above one. A star's structure is the other
     * way round — concentric, so it changes fastest across — which makes it the
     * control, and a control that has to come out BELOW the threshold is a much
     * stronger one than a second type that merely agrees.
     *
     * This exists because a wrong conclusion about the gas giant survived a round
     * of screenshots: bands removed, weather only, and the swirls looked plausible
     * enough to keep. Measuring the direction of the structure cannot be
     * satisfied by isotropic mottle.
     */
    const aniso = await page.evaluate(async () => {
      const ratio = async (type) => {
        const im = new Image();
        // Dither OFF. This measures the band field, not the band field plus the
        // ordered dither: with it on, the per-pixel dither contributes the same
        // variance in both directions and swamps the structure entirely — the
        // first run of this check returned 1.04 for a visibly banded gas giant
        // and 1.00 for a star, i.e. both perfectly isotropic, which is what a
        // measurement of noise looks like.
        im.src = window.__galaxyStill({ seed: 0x5eed1, type, px: 128, dpr: 1, dither: false });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        const d = cx.getImageData(0, 0, im.width, im.height).data;
        const w = im.width;
        const h = im.height;
        const L = (x, y) => {
          const i = (y * w + x) * 4;
          if (d[i + 3] === 0) return null;
          return 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
        };
        // Central 60% only: the limb is where the sphere projection compresses
        // everything, and a compression artefact would swamp the measurement.
        const x0 = Math.round(w * 0.2);
        const x1 = Math.round(w * 0.8);
        const y0 = Math.round(h * 0.2);
        const y1 = Math.round(h * 0.8);
        let nv = 0;
        let sv = 0;
        let nh = 0;
        let sh = 0;
        for (let y = y0; y < y1; y++) {
          for (let x = x0; x < x1; x++) {
            const a = L(x, y);
            const b = L(x, y + 1);
            if (a !== null && b !== null) {
              sv += Math.abs(a - b);
              nv++;
            }
            const cc = L(x + 1, y);
            if (a !== null && cc !== null) {
              sh += Math.abs(a - cc);
              nh++;
            }
          }
        }
        return sv / nv / (sh / nh);
      };
      return { gas: await ratio("gas"), star: await ratio("star"), terran: await ratio("terran") };
    });

    check(
      "gas giant bands run east-west, not around the planet",
      aniso.gas > 1.3,
      `vertical/horizontal colour change ${aniso.gas.toFixed(2)}`,
    );
    check(
      "and a star's structure is the other way round — the control, which has to come out below the threshold",
      aniso.star < 1.15,
      `star ${aniso.star.toFixed(2)}, gas ${aniso.gas.toFixed(2)}`,
    );

    check(
      "a world is drawn from a small discrete palette, not a smooth gradient",
      look.worldColours < 400,
      `${look.worldColours} distinct colours in ${look.px} pixels`,
    );
    check(
      "a star is banded too, not a gradient",
      look.starColours < 400,
      `${look.starColours} distinct colours`,
    );
    // The corona is the one thing allowed outside the disc. Guards the rule that
    // a planet is exactly its sprite: an atmosphere bleeding past the edge was
    // what made the first sprite set read as stickers.
    check(
      "a planet is exactly its disc — no halo outside",
      look.worldCorner === 0 && look.worldCentre === 255,
      `corner alpha ${look.worldCorner}, centre ${look.worldCentre}`,
    );
    check(
      "a star does bleed past its edge",
      look.starEdge > 0 && look.starCentre === 255,
      `annulus alpha ${look.starEdge}, centre ${look.starCentre}`,
    );
    check(
      "and it is a corona, not a halo: the annulus is dimmer than the disc",
      look.starEdge < look.starCentre,
      `${look.starEdge} vs ${look.starCentre}`,
    );
    void look.worldEdge;

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
