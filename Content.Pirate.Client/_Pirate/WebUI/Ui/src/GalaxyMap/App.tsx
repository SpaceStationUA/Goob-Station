import { createMemo, createSignal, For, onCleanup, onMount, Show } from "solid-js";
import Chart from "./Chart";
import { cellsInExtent, hexLine, key, pixelToHex, type Axial } from "./lib/hex";
import { cellsByTerritory } from "./lib/geometry";
import { pick, type GalaxyModel, type StarSystem } from "./lib/model";
import { hasRings, planetTypeFor, seedFromId } from "./lib/planet";
import { readableOnDark } from "./Chart";
import WorldSprite from "./WorldSprite";
import BlackHole from "./BlackHole";
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
  /** A star marker, selected. Distinct from `selected`, which is a territory. */
  const [focused, setFocused] = createSignal<string>();
  const [hoverSystem, setHoverSystem] = createSignal<string>();
  const [hoverCell, setHoverCell] = createSignal<Axial>();
  const [showCells, setShowCells] = createSignal(false);
  const [brush, setBrush] = createSignal<Brush>();
  const [locale, setLocale] = createSignal<string>("en-US");
  const [edits, setEdits] = createSignal(0);
  /** Bumped when the bridge installs a new string table or locale list. */
  const [stringsVersion, setStringsVersion] = createSignal(0);

  /**
   * Client-side kill switch for the brushes, layered over whatever the source
   * says. The DOM check flips this to exercise the read-only player view against
   * the real component; in game the source's permission is the only input.
   */
  const [paintAllowed, setPaintAllowed] = createSignal(true);

  /** Draw systems as their generated world rather than as a dot. */
  const [planets, setPlanets] = createSignal(false);

  /**
   * In-progress drag: the cells covered so far, and the cell the pointer was on
   * last. The latter is what makes the stroke continuous — each move fills the
   * line from the previous cell, so a fast flick across four cells marks all
   * four instead of leaving three gaps.
   */
  /**
   * Declared before the memos that read it.
   *
   * `canPaint` is a memo, and a memo body runs immediately — so a `let` below
   * this point is still in its temporal dead zone when the memo first evaluates
   * and the whole component throws before it renders. TypeScript cannot see
   * that, which is why the failure was a blank page rather than a build error.
   */
  let source: FixtureSource | undefined;

  const [stroking, setStroking] = createSignal(false);
  const [pendingCells, setPendingCells] = createSignal<ReadonlySet<string>>();
  const [lastCell, setLastCell] = createSignal<Axial>();

  /**
   * Painting is an admin tool; a player gets the same chart without the brushes.
   *
   * Read from the source rather than a build flag, so the shipped binary is the
   * same for everyone and only the payload differs.
   */
  const canPaint = createMemo(() => paintAllowed() && source?.permissions?.paint !== false);

  /**
   * What the in-progress stroke will do, for its preview colour.
   *
   * A dispute stroke previews in the dispute amber rather than in the owner's
   * colour: the thing being drawn is the flag, not a change of owner, and
   * previewing it in the owner's colour would suggest otherwise.
   */
  const pendingStyle = createMemo<{ colour: string } | undefined>(() => {
    const b = brush();
    if (!b) return undefined;
    if (b.kind === "contest") return { colour: b.on ? "#ffb454" : "#8a7a52" };
    if (b.kind === "unclaim") return { colour: terrById().get(unclaimedId())?.color ?? "#8a96a8" };
    return { colour: terrById().get(b.territory)?.color ?? "#ffd479" };
  });
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
  // Lets the DOM check exercise the read-only player view against the real
  // component, rather than trusting that the gate works because nothing renders
  // the brushes anyway.
  (window as unknown as Record<string, unknown>).__galaxySetPermission = (allowed: boolean) =>
    setPaintAllowed(allowed);

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
    if (ev.key !== "Escape") return;
    // Mid-drag, Escape throws the stroke away rather than committing half of it.
    if (stroking()) cancelStroke();
    setBrush(undefined);
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

  const sysById = createMemo(() => new Map((model()?.systems ?? []).map(s => [s.id, s])));

  /** The type of world to generate for a system: explicit data, else hashed. */
  const worldType = (s: StarSystem) => s.planetType ?? planetTypeFor(s.kind, s.id);

  /**
   * Clicking a star.
   *
   * Clicking the same star twice closes it, matching the territory panel's
   * toggle, so "click it again to dismiss" works the same everywhere on the
   * chart. Only one of the two panels is ever open: they describe different
   * things at different scales, and stacking them would put two sets of close
   * buttons on screen at once.
   */
  function onSystemClick(id: string) {
    setSelected(undefined);
    setFocused(prev => (prev === id ? undefined : id));
  }

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

  /* --------------------------- drag to paint --------------------------- */
  /*
   * A stroke is accumulated locally and committed once on release.
   *
   * Committing per cell would rebuild the model on every mousemove, and the
   * rebuild re-runs the wobble across every territory outline — far too much
   * work for something that has to keep up with a pointer. It would also leave
   * one undo step per cell, so a single flick would take twenty undos to put
   * back. Both problems go away by treating the drag as one gesture.
   */

  function addToStroke(from: Axial | undefined, to: Axial): ReadonlySet<string> {
    const next = new Set(pendingCells() ?? []);
    for (const c of from ? hexLine(from, to) : [to]) next.add(key(c.q, c.r));
    return next;
  }

  function onStrokeStart(ly: { x: number; y: number }) {
    const m = model();
    if (!m || !canPaint() || !brush()) return;
    const cell = pixelToHex(ly, m.hexSizeLy);
    setStroking(true);
    setLastCell(cell);
    setPendingCells(addToStroke(undefined, cell));
  }

  function onStrokeMove(ly: { x: number; y: number }) {
    const m = model();
    if (!m || !stroking()) return;
    const cell = pixelToHex(ly, m.hexSizeLy);
    const prev = lastCell();
    if (prev && prev.q === cell.q && prev.r === cell.r) return;
    setLastCell(cell);
    setPendingCells(addToStroke(prev, cell));
  }

  async function onStrokeEnd() {
    const cells = pendingCells();
    const b = brush();
    if (!stroking()) return;
    setStroking(false);
    setLastCell(undefined);
    setPendingCells(undefined);
    if (!cells || cells.size === 0 || !b || !source) return;
    const list = [...cells].map(k => {
      const [q, r] = k.split(",");
      return { q: +q, r: +r };
    });
    if (b.kind === "contest") await source.contestStroke?.(list, b.on);
    else await source.stroke?.(list, b.kind === "unclaim" ? unclaimedId() : b.territory);
  }

  /** Abandon the in-progress stroke without committing it. */
  function cancelStroke() {
    setStroking(false);
    setLastCell(undefined);
    setPendingCells(undefined);
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
    // Selecting a territory dismisses any open system: one panel at a time.
    setFocused(undefined);
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
            canPaint={canPaint()}
            brushArmed={!!brush()}
            stroking={stroking()}
            pendingCells={pendingCells()}
            pendingColour={pendingStyle()?.colour}
            planets={planets()}
            onHover={onHover}
            onClick={onClick}
            onSystemClick={onSystemClick}
            hoverSystem={hoverSystem()}
            onSystemHover={setHoverSystem}
            onStrokeStart={onStrokeStart}
            onStrokeMove={onStrokeMove}
            onStrokeEnd={onStrokeEnd}
            onLeave={() => setHoverCell(undefined)}
          />

          <div class="toolbar">
            <button classList={{ on: showCells() }} onClick={() => setShowCells(v => !v)}>
              {t("grid", loc())}
            </button>

            {/* Was a checkbox in `Spike`. A system drawn as its own world reads as a
                place; a dot reads as a pin. The panel it started in is gone. */}
            <button classList={{ on: planets() }} onClick={() => setPlanets(v => !v)}>
              {t("planets", loc())}
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

            {/* The brushes exist only for an admin. A player gets the same chart
                with the tools taken out, rather than a toolbar full of things
                that silently do nothing. */}
            <Show when={canPaint()}>
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
            </Show>

            <Show when={brush()}>
              <span class="armed-note">
                {brushLabel()}
                {t("brushHint", loc())}
              </span>
            </Show>

            {/* Undo is part of the painting tool, so it goes when painting does.
                Gating it on the edit count alone left a read-only viewer with a
                live UNDO button that reverted an admin's work out from under
                them. */}
            <Show when={canPaint() && edits() > 0}>
              <button onClick={undo}>
                {t("undo", loc())} {edits()}
              </button>
            </Show>
          </div>

          <Show when={focused()}>
            {id => {
              const sys = sysById().get(id());
              return (
                <Show when={sys}>
                  {s => {
                    const terr = () => terrById().get(s().territory);
                    const kindKey = () =>
                      s().kind === "star"
                        ? "kindStar"
                        : s().kind === "planet"
                          ? "kindPlanet"
                          : s().kind === "blackhole"
                            ? "kindBlackHole"
                            : s().kind === "station"
                            ? "kindStation"
                            : s().kind === "gate"
                              ? "kindGate"
                              : "kindOutpost";
                    return (
                      // A ringed system gets a wider panel, so the ring can be the
                      // size a ring is instead of a collar. See `.overlay.ringed`.
                      <div
                        class="panel overlay"
                        classList={{
                          ringed:
                            s().kind !== "blackhole" &&
                            hasRings(seedFromId(s().id), worldType(s()), s().rings),
                        }}
                      >
                        <button
                          class="panel-close overlay-close"
                          onClick={() => setFocused(undefined)}
                        >
                          {t("close", loc())}
                        </button>

                        {/* The one place a generated world is drawn big enough
                            to be worth generating. At 16-40px on the chart a
                            rotation is invisible and the sprite is mostly a
                            coloured dot; at 180px it is the reason to click. */}
                        <div class="overlay-art">
                          {/* A singularity has no world to draw. The panel still
                              opens, and still says what the system is — which is
                              the point of making it its own kind. */}
                          <Show
                            when={s().kind === "blackhole"}
                            fallback={
                              <WorldSprite
                                seed={seedFromId(s().id)}
                                type={worldType(s())}
                                px={200}
                                frames={96}
                                period={12}
                                ring={hasRings(seedFromId(s().id), worldType(s()), s().rings)}
                                title={pick(s().name, loc())}
                              />
                            }
                          >
                            <BlackHole
                              px={190}
                              seed={seedFromId(s().id)}
                              frames={48}
                              period={6}
                              title={pick(s().name, loc())}
                            />
                          </Show>
                        </div>

                        <h2 style={{ color: readableOnDark(terr()?.color ?? "#94a3b8") }}>
                          {pick(s().name, loc())}
                        </h2>
                        <div class="sub">{t(kindKey(), loc())}</div>

                        <Show when={s().importance === 3}>
                          <div class="capital-tag">
                            {t("capitalOf", loc(), {
                              name: terr()
                                ? pick(terr()!.name, loc())
                                : t("unclaimedOwner", loc()),
                            })}
                          </div>
                        </Show>

                        <div class="stat">
                          <span>{t("labelSovereign", loc())}</span>
                          <span>
                            <Show
                              when={terr() && !terr()!.unclaimed}
                              fallback={<em>{t("unclaimedOwner", loc())}</em>}
                            >
                              <button
                                class="link"
                                style={{ color: readableOnDark(terr()!.color) }}
                                onClick={() => setSelected(s().territory)}
                              >
                                {pick(terr()!.name, loc())}
                              </button>
                            </Show>
                          </span>
                        </div>
                        <div class="stat">
                          <span>{t("labelPosition", loc())}</span>
                          <span class="mono">
                            {s().xLy.toFixed(0)} / {s().yLy.toFixed(0)} LY
                          </span>
                        </div>
                      </div>
                    );
                  }}
                </Show>
              );
            }}
          </Show>

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
