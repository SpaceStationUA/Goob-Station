import { createMemo, createSignal, For, onMount, Show } from "solid-js";
import Chart from "./Chart";
import { cellsInExtent, key, pixelToHex, type Axial } from "./lib/hex";
import { cellsByTerritory } from "./lib/geometry";
import type { GalaxyModel } from "./lib/model";
import { DEFAULT_MAP, FixtureSource } from "./lib/source";
import { CLAIMS, ROUTES, SYSTEMS, TERRITORIES } from "./lib/devmap";

/**
 * Browser harness entry point.
 *
 * Owns state and chrome only — the chart itself is a pure function of the
 * model. The data comes from a FixtureSource here; in game it will come from
 * a bridge source, and nothing below this line has to change.
 */
export default function App() {
  const [model, setModel] = createSignal<GalaxyModel>();
  const [selected, setSelected] = createSignal<string>();
  const [hoverCell, setHoverCell] = createSignal<Axial>();
  const [showCells, setShowCells] = createSignal(false);
  const [paintWith, setPaintWith] = createSignal<string>();
  const [painted, setPainted] = createSignal(0);

  let source: FixtureSource | undefined;

  onMount(async () => {
    source = new FixtureSource(DEFAULT_MAP, TERRITORIES, CLAIMS, SYSTEMS, ROUTES);
    setModel(await source.load());
  });

  const terrById = createMemo(() => {
    const out = new Map<string, (typeof TERRITORIES)[number]>();
    for (const t of model()?.territories ?? []) out.set(t.id, t);
    return out;
  });

  const cellCount = createMemo(() => {
    const sel = selected();
    if (!sel) return 0;
    return cellsByTerritory(model()?.ownership ?? new Map()).get(sel)?.length ?? 0;
  });

  const systemsIn = (id: string) => (model()?.systems ?? []).filter(s => s.territory === id);
  const paintable = createMemo(() => (model()?.territories ?? []).filter(t => !t.unclaimed));

  /* --------------------------- interaction --------------------------- */
  /* The chart hands back light-year coordinates; all pixel maths is its job. */

  function onHover(ly: { x: number; y: number }) {
    const m = model();
    if (!m) return;
    setHoverCell(pixelToHex(ly, m.hexSizeLy));
  }

  async function onClick(ly: { x: number; y: number }) {
    const m = model();
    if (!m) return;
    const cell = pixelToHex(ly, m.hexSizeLy);
    const brush = paintWith();
    if (brush && source) {
      await source.paint(cell.q, cell.r, brush);
      setPainted(n => n + 1);
      return;
    }
    const owner = m.ownership.get(key(cell.q, cell.r));
    setSelected(prev => (prev === owner ? undefined : owner));
  }

  async function undo() {
    if (!source) return;
    await source.undo();
    setPainted(n => Math.max(0, n - 1));
  }

  const totalCells = createMemo(() => {
    const m = model();
    return m ? cellsInExtent(m.extentLy.w, m.extentLy.h, m.hexSizeLy).length : 0;
  });

  return (
    <Show when={model()} fallback={<div class="chart-shell" />}>
      {m => (
        <div class="chart-shell">
          <Chart
            model={m()}
            showCells={showCells()}
            selected={selected()}
            hoverCell={hoverCell()}
            painting={!!paintWith()}
            onHover={onHover}
            onClick={onClick}
            onLeave={() => setHoverCell(undefined)}
          />

          <div class="toolbar">
            <button classList={{ on: showCells() }} onClick={() => setShowCells(v => !v)}>
              GRID
            </button>
            <Show when={painted() > 0}>
              <button onClick={undo}>UNDO {painted()}</button>
            </Show>
            <div class="paintpick">
              <span class="paintlabel">PAINT</span>
              <For each={paintable()}>
                {t => (
                  <div
                    class="swatch"
                    classList={{ on: paintWith() === t.id }}
                    style={{ background: t.color }}
                    title={t.name}
                    onClick={() => setPaintWith(prev => (prev === t.id ? undefined : t.id))}
                  />
                )}
              </For>
              <Show when={paintWith()}>
                <button onClick={() => setPaintWith(undefined)}>STOP</button>
              </Show>
            </div>
          </div>

          <Show when={selected()}>
            {id => {
              const t = terrById().get(id());
              return (
                <Show when={t}>
                  <div class="panel">
                    <h2 style={{ color: t!.color }}>{t!.name}</h2>
                    <div class="sub">{t!.unclaimed ? "UNCLAIMED SPACE" : "SOVEREIGN TERRITORY"}</div>
                    <div class="blurb">{t!.blurb}</div>
                    <div class="stat">
                      <span>CELLS</span>
                      <span>{cellCount()}</span>
                    </div>
                    <div class="stat">
                      <span>SYSTEMS</span>
                      <span>{systemsIn(id()).length}</span>
                    </div>
                    <div class="stat">
                      <span>CAPITALS</span>
                      <span>{systemsIn(id()).filter(s => s.importance === 3).length}</span>
                    </div>
                  </div>
                </Show>
              );
            }}
          </Show>

          <div class="legend">
            <b>ORION SPUR</b>
            {m().extentLy.w} × {m().extentLy.h} LY · {totalCells()} cells @ {m().hexSizeLy} LY
            <Show when={paintWith()}> · PAINT: {terrById().get(paintWith()!)?.name}</Show>
          </div>
        </div>
      )}
    </Show>
  );
}
