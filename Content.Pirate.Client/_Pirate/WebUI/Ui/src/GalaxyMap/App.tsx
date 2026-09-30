import { createMemo, createSignal, For, onCleanup, onMount, Show } from "solid-js";
import Chart from "./Chart";
import { cellsInExtent, key, pixelToHex, type Axial } from "./lib/hex";
import { cellsByTerritory } from "./lib/geometry";
import { pick, type GalaxyModel } from "./lib/model";
import {
  currentLocales,
  installStrings,
  setLocales,
  t,
  tp,
} from "./lib/i18n";
import { DEFAULT_MAP, FixtureSource } from "./lib/source";
import { CLAIMS, ROUTES, SYSTEMS, TERRITORIES } from "./lib/devmap";

/**
 * What a click does while a brush is armed.
 *
 * Modelled as data rather than as a bag of booleans in the component because
 * the game needs the same cases, and because "unclaim" is not a special case at
 * all: it is painting the `unclaimed` territory, which already exists in the
 * model. Treating it as one more swatch means there is no second code path to
 * get wrong. Un-contesting is the mirror of contesting and sits beside it for
 * the same reason — an admin settling a row should not have to reach for a
 * modifier key to undo the mark they just made.
 */
type Brush =
  | { kind: "owner"; territory: string }
  | { kind: "unclaim" }
  | { kind: "contest"; on: boolean };

/** Identity for the "is this brush already armed" comparison. */
const brushId = (b: Brush | undefined): string => {
  if (b === undefined) return "";
  if (b.kind === "owner") return `t:${b.territory}`;
  // `on` is part of the identity: contest and uncontest are separate brushes and
  // must not read as the same one.
  return `${b.kind}${b.kind === "contest" ? (b.on ? "" : ":off") : ""}`;
};

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
  const [locale, setLocale] = createSignal<string>("en-US");
  const [edits, setEdits] = createSignal(0);
  /** Bumped when the bridge installs a new string table or locale list. */
  const [stringsVersion, setStringsVersion] = createSignal(0);
  const [locales, setLocalesSignal] = createSignal(currentLocales());

  // The browser harness uses the built-in table. In game this is where the
  // bridge's push lands instead; both paths go through the same signal so the
  // UI reacts identically.
  function adoptStrings(
    next: Parameters<typeof installStrings>[0],
    nextLocales?: Parameters<typeof setLocales>[0],
  ) {
    installStrings(next);
    if (nextLocales) setLocales(nextLocales);
    setStringsVersion(v => v + 1);
  }
  // Exposed for the bridge push and for the DOM check. Without this the
  // installed table is unreachable from outside the component and the seam is
  // theoretical.
  (window as unknown as Record<string, unknown>).__galaxyAdoptStrings = adoptStrings;

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

  /**
   * The active locale, and a stand-in for the string table's freshness.
   *
   * The table in lib/i18n is module state, which Solid cannot see. Reading this
   * version bump inside `loc()` makes every `t()` call in the tree re-evaluate
   * when the bridge pushes new strings, without threading the table through the
   * component tree as a prop.
   */
  const loc = createMemo(() => {
    stringsVersion();
    return locale();
  });
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
      return b.on ? t("brushContesting", loc()) : t("brushClearingContest", loc());
    }
    if (b.kind === "unclaim") return t("brushUnclaimed", loc());
    const name = terrById().get(b.territory);
    return t("brushPainting", loc(), {
      name: name ? pick(name.name, loc()) : b.territory,
    });
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
              {t("grid", loc())}
            </button>

            <div class="locpick">
              <For each={locales()}>
                {l => (
                  <button
                    classList={{ on: locale() === l.id }}
                    onClick={() => setLocale(l.id)}
                    title={t("showNamesIn", loc(), { locale: l.label })}
                  >
                    {l.label}
                  </button>
                )}
              </For>
            </div>

            <div class="paintpick" classList={{ armed: !!brush() }}>
              <span class="paintlabel">{t("paint", loc())}</span>
              <For each={paintable()}>
                {terr => (
                  <div
                    class="swatch"
                    classList={{ on: brushId(brush()) === `t:${terr.id}` }}
                    style={{ background: terr.color }}
                    title={pick(terr.name, loc())}
                    onClick={() => arm({ kind: "owner", territory: terr.id })}
                  />
                )}
              </For>
              <div
                class="swatch swatch-unclaim"
                classList={{ on: brushId(brush()) === "unclaim" }}
                title={t("tipUnclaim", loc())}
                onClick={() => arm({ kind: "unclaim" })}
              />
              <div
                class="swatch swatch-contest"
                classList={{ on: brushId(brush()) === "contest" }}
                title={t("tipContest", loc())}
                onClick={() => arm({ kind: "contest", on: true })}
              />
              <div
                class="swatch swatch-uncontest"
                classList={{ on: brushId(brush()) === "contest:off" }}
                title={t("tipUncontest", loc())}
                onClick={() => arm({ kind: "contest", on: false })}
              />
            </div>

            <Show when={brush()}>
              <span class="armed-note">
                {brushLabel()}
                {t("brushHint", loc())}
              </span>
            </Show>

            <Show when={edits() > 0}>
              <button onClick={undo}>
                {t("undo", loc())} {edits()}
              </button>
            </Show>
          </div>

          <Show when={selected()}>
            {id => {
              const terr = terrById().get(id());
              return (
                <Show when={terr}>
                  <div class="panel">
                    <h2 style={{ color: terr!.color }}>{pick(terr!.name, loc())}</h2>
                    <div class="sub">
                      {terr!.unclaimed
                        ? t("unclaimedSpace", loc())
                        : t("sovereignTerritory", loc())}
                    </div>
                    <Show when={terr!.blurb}>
                      <div class="blurb">{pick(terr!.blurb, loc())}</div>
                    </Show>
                    <div class="stat">
                      <span>{t("labelCells", loc())}</span>
                      <span>{cellCount()}</span>
                    </div>
                    <div class="stat">
                      <span>{t("labelSystems", loc())}</span>
                      <span>{systemsIn(id()).length}</span>
                    </div>
                    <div class="stat">
                      <span>{t("labelCapitals", loc())}</span>
                      <span>{systemsIn(id()).filter(s => s.importance === 3).length}</span>
                    </div>
                    <div class="stat">
                      <span>{t("labelContested", loc())}</span>
                      <span>{contestedIn(id())}</span>
                    </div>
                    <button class="panel-close" onClick={() => setSelected(undefined)}>
                      {t("close", loc())}
                    </button>
                  </div>
                </Show>
              );
            }}
          </Show>

          <div class="legend">
            <b>{t("orionSpur", loc())}</b>
            {t("extent", loc(), {
              w: m().extentLy.w,
              h: m().extentLy.h,
              cells: totalCells(),
              size: m().hexSizeLy,
            })}
            <Show when={contestedCount() > 0}>
              {" · "}
              <span class="legend-contested">{tp("legendContested", contestedCount(), loc())}</span>
            </Show>
            <Show when={edits() > 0}>
              {" · "}
              {tp("legendEdits", edits(), loc())}
            </Show>
          </div>
        </div>
      )}
    </Show>
  );
}
