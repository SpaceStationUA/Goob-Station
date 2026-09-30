import { createMemo, createSignal, For, onCleanup, onMount, Show } from "solid-js";
import Chart from "./Chart";
import { cellsInExtent, key, pixelToHex, type Axial } from "./lib/hex";
import { cellsByTerritory } from "./lib/geometry";
import { pick, type GalaxyModel } from "./lib/model";
import { DEFAULT_MAP, FixtureSource } from "./lib/source";
import { CLAIMS, ROUTES, SYSTEMS, TERRITORIES } from "./lib/devmap";

/** Locales the page can render. Matches what the bridge will send. */
const LOCALES = [
  { id: "en", label: "EN" },
  { id: "uk", label: "УКР" },
] as const;

/**
 * What a click does while a brush is armed.
 *
 * Modelled as data rather than as a bag of booleans in the component because
 * the game needs the same three cases, and because "unclaim" is not a special
 * case at all: it is painting the `unclaimed` territory, which already exists in
 * the model. Treating it as one more swatch means there is no second code path
 * to get wrong.
 */
type Brush =
  | { kind: "owner"; territory: string }
  | { kind: "unclaim" }
  | { kind: "contest"; on: boolean };

const brushId = (b: Brush | undefined) =>
  b === undefined ? "" : b.kind === "owner" ? `t:${b.territory}` : b.kind;

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
  const [brush, setBrush] = createSignal<Brush>();
  const [locale, setLocale] = createSignal<string>("en");
  const [edits, setEdits] = createSignal(0);

  let source: FixtureSource | undefined;

  onMount(async () => {
    source = new FixtureSource(DEFAULT_MAP, TERRITORIES, CLAIMS, SYSTEMS, ROUTES);
    // The subscription is the whole reason painting works. The source hands back
    // a NEW model each time; without this the map keeps rendering the one it
    // was given at startup and every stroke is invisible.
    source.onChange(m => {
      setModel(m);
      setEdits(source?.edits ?? 0);
    });
    setModel(await source.load());
  });

  // Escape drops the armed brush. Without it there is no way out once a swatch
  // is picked except hunting for the same swatch again, which reads as being
  // stuck.
  const onKey = (ev: KeyboardEvent) => {
    if (ev.key === "Escape") setBrush(undefined);
  };
  window.addEventListener("keydown", onKey);
  onCleanup(() => window.removeEventListener("keydown", onKey));

  const loc = () => locale();
  const terrById = createMemo(() => {
    const out = new Map<string, (typeof TERRITORIES)[number]>();
    for (const t of model()?.territories ?? []) out.set(t.id, t);
    return out;
  });

  /** The id of the territory that represents "nobody's". */
  const unclaimedId = createMemo(
    () => (model()?.territories ?? []).find(t => t.unclaimed)?.id ?? "",
  );

  const cellCount = createMemo(() => {
    const sel = selected();
    if (!sel) return 0;
    return cellsByTerritory(model()?.ownership ?? new Map()).get(sel)?.length ?? 0;
  });

  const systemsIn = (id: string) => (model()?.systems ?? []).filter(s => s.territory === id);

  /** Brushable nations. Unclaimed is excluded because it has its own brush. */
  const paintable = createMemo(() => (model()?.territories ?? []).filter(t => !t.unclaimed));

  const contestedCount = createMemo(() => model()?.contested.size ?? 0);

  /** Disputed cells belonging to one territory, for the side panel. */
  const contestedIn = (id: string) => {
    const cells = new Set(cellsByTerritory(model()?.ownership ?? new Map()).get(id) ?? []);
    let n = 0;
    for (const c of model()?.contested ?? []) if (cells.has(c)) n++;
    return n;
  };

  /**
   * Full phrase for the armed-brush note, verb included.
   *
   * A bare noun is not enough here. With a brush armed a click means something
   * completely different to what it meant a moment ago, so the toolbar has to
   * say what the next click will DO, not just what it will paint.
   */
  const brushLabel = createMemo(() => {
    const b = brush();
    if (!b) return "";
    if (b.kind === "contest") {
      return b.on ? "MARKING CONTESTED" : "CLEARING CONTESTED";
    }
    if (b.kind === "unclaim") return "PAINTING UNCLAIMED SPACE";
    const name = terrById().get(b.territory);
    return `PAINTING ${name ? pick(name.name, loc()) : b.territory}`;
  });

  /** Clicking the armed brush again puts it down. */
  function arm(b: Brush) {
    setBrush(prev => (brushId(prev) === brushId(b) ? undefined : b));
  }

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
    const b = brush();
    if (b && source) {
      if (b.kind === "contest") await source.setContested?.(cell.q, cell.r, b.on);
      else await source.paint(cell.q, cell.r, b.kind === "unclaim" ? unclaimedId() : b.territory);
      return;
    }
    const owner = m.ownership.get(key(cell.q, cell.r));
    if (owner === undefined) return;
    setSelected(prev => (prev === owner ? undefined : owner));
  }

  async function undo() {
    await source?.undo();
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
            locale={loc()}
            showCells={showCells()}
            selected={selected()}
            hoverCell={hoverCell()}
            painting={!!brush()}
            onHover={onHover}
            onClick={onClick}
            onLeave={() => setHoverCell(undefined)}
          />

          <div class="toolbar">
            <button classList={{ on: showCells() }} onClick={() => setShowCells(v => !v)}>
              GRID
            </button>

            <div class="locpick">
              <For each={LOCALES}>
                {l => (
                  <button
                    classList={{ on: locale() === l.id }}
                    onClick={() => setLocale(l.id)}
                    title={`Show names in ${l.id}`}
                  >
                    {l.label}
                  </button>
                )}
              </For>
            </div>

            <div class="paintpick" classList={{ armed: !!brush() }}>
              <span class="paintlabel">PAINT</span>
              <For each={paintable()}>
                {t => (
                  <div
                    class="swatch"
                    classList={{ on: brushId(brush()) === `t:${t.id}` }}
                    style={{ background: t.color }}
                    title={pick(t.name, loc())}
                    onClick={() => arm({ kind: "owner", territory: t.id })}
                  />
                )}
              </For>
              <div
                class="swatch swatch-unclaim"
                classList={{ on: brushId(brush()) === "unclaim" }}
                title="Return the cell to unclaimed space"
                onClick={() => arm({ kind: "unclaim" })}
              />
              <div
                class="swatch swatch-contest"
                classList={{ on: brushId(brush()) === "contest" }}
                title="Mark the cell as contested — two claims, one owner"
                onClick={() => arm({ kind: "contest", on: true })}
              />
            </div>

            <Show when={brush()}>
              <span class="armed-note">{brushLabel()} — click cells, ESC to stop</span>
            </Show>

            <Show when={edits() > 0}>
              <button onClick={undo}>UNDO {edits()}</button>
            </Show>
          </div>

          <Show when={selected()}>
            {id => {
              const t = terrById().get(id());
              return (
                <Show when={t}>
                  <div class="panel">
                    <h2 style={{ color: t!.color }}>{pick(t!.name, loc())}</h2>
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
                    <div class="stat">
                      <span>CONTESTED</span>
                      <span>{contestedIn(id())}</span>
                    </div>
                    <button class="panel-close" onClick={() => setSelected(undefined)}>
                      CLOSE
                    </button>
                  </div>
                </Show>
              );
            }}
          </Show>

          <div class="legend">
            <b>ORION SPUR</b>
            {m().extentLy.w} × {m().extentLy.h} LY · {totalCells()} cells @ {m().hexSizeLy} LY
            <Show when={contestedCount() > 0}>
              {" "}
              · <span class="legend-contested">{contestedCount()} contested</span>
            </Show>
            <Show when={edits() > 0}> · {edits()} local edit(s)</Show>
          </div>
        </div>
      )}
    </Show>
  );
}
