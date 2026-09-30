import { cellsInExtent, key } from "./hex";
import { assignCells, type AssignResult } from "./geometry";
import type { GalaxyModel, Ownership, Route, StarSystem, Territory, TerritoryClaim } from "./model";

/**
 * Where the page gets its data.
 *
 * The page body never calls this directly — it receives a model and renders
 * it. That is the whole reason the identical code runs in a browser against a
 * fixture and in the game against the bridge, and it is why the two cannot
 * drift apart in their rendering.
 */
export interface GalaxySource {
  /** Resolve once the model is available. */
  load(): Promise<GalaxyModel>;
  /** Fires whenever ownership changes (round-time painting). */
  onChange(cb: (model: GalaxyModel) => void): void;
  /**
   * Reassign one cell. Present only where painting is permitted; the game
   * validates it server-side, a browser fixture just mutates its own copy.
   *
   * Returns whether anything actually changed, so a caller can skip work when
   * the cell was already the requested territory.
   */
  paint?(cellQ: number, cellR: number, territory: string): Promise<boolean>;
}

/** Map extents and grid resolution, shared by the bake and the runtime. */
export interface MapSpec {
  extentLy: { w: number; h: number };
  hexSizeLy: number;
  /** Id given to every cell no claim covered. */
  unclaimedId: string;
}

export const DEFAULT_MAP: MapSpec = {
  extentLy: { w: 132, h: 74 },
  // 2 LY cells: ~38 columns by ~24 rows, so a shade under 1000 cells for the
  // whole Spur. Small enough to place a border to roughly one system spacing,
  // big enough that nothing is fiddly to click.
  hexSizeLy: 2.0,
  unclaimedId: "unclaimed",
};

/**
 * Resolve hand-drawn claims into a concrete partition.
 *
 * This is the runtime equivalent of the bake command, kept here so the browser
 * harness and the committed data are produced by the same code path. If the
 * two ever disagree it is a bug, not a configuration difference.
 */
export function buildOwnership(
  spec: MapSpec,
  claims: TerritoryClaim[],
): AssignResult {
  return assignCells(
    claims,
    cellsInExtent(spec.extentLy.w, spec.extentLy.h, spec.hexSizeLy),
    spec.hexSizeLy,
    spec.unclaimedId,
  );
}

export function buildModel(
  spec: MapSpec,
  territories: Territory[],
  claims: TerritoryClaim[],
  systems: StarSystem[] = [],
  routes: Route[] = [],
): GalaxyModel {
  const { ownership, contested } = buildOwnership(spec, claims);
  if (contested.length > 0) {
    const first = contested[0];
    console.info(
      `[galaxy] ${contested.length} contested cell(s) resolved by depth, ` +
        `e.g. ${first.cell.q},${first.cell.r}: ${first.claimants.join(" vs ")}`,
    );
  }
  return {
    extentLy: spec.extentLy,
    hexSizeLy: spec.hexSizeLy,
    territories,
    systems,
    routes,
    ownership,
    revision: 0,
  };
}

/* ------------------------------------------------------------------ *
 * Browser fixture
 * ------------------------------------------------------------------ */

/**
 * Dev-only source backed by an inline spec. This is what
 * `TUI_IFACE=GalaxyMap npm run dev` renders — no game, no bridge, no CEF.
 *
 * Ownership is a frozen baseline plus a sparse overlay, which is the same
 * shape the game uses: the baseline comes from the baked data and never
 * changes during a round, painting layers deltas on top, and the overlay is
 * discarded at round restart.
 */
export class FixtureSource implements GalaxySource {
  private listeners: ((m: GalaxyModel) => void)[] = [];
  private overlay = new Map<string, string>();
  private undoStack: { cell: string; prev: string | undefined }[] = [];
  private current: GalaxyModel | null = null;

  constructor(
    private spec: MapSpec,
    private territories: Territory[],
    private claims: TerritoryClaim[],
    private systems: StarSystem[] = [],
    private routes: Route[] = [],
  ) {}

  /**
   * Build a fresh model from the pristine baseline plus the overlay.
   *
   * Both the model and its ownership map are new objects every time. That is
   * not incidental: the view is Solid, so handing it the same object it already
   * holds — even after mutating that object's contents — changes nothing on
   * screen. Painting appeared to do nothing at all for exactly this reason.
   */
  private snapshot(): GalaxyModel {
    const base = buildModel(this.spec, this.territories, this.claims, this.systems, this.routes);
    if (this.overlay.size === 0) return base;
    const ownership: Ownership = new Map(base.ownership);
    for (const [cell, id] of this.overlay) ownership.set(cell, id);
    return { ...base, ownership, revision: base.revision + 1 };
  }

  async load(): Promise<GalaxyModel> {
    if (!this.current) this.current = this.snapshot();
    return this.current;
  }

  onChange(cb: (model: GalaxyModel) => void): void {
    this.listeners.push(cb);
  }

  private emit(): void {
    this.current = this.snapshot();
    for (const cb of this.listeners) cb(this.current);
  }

  /** Number of local edits not in the baked baseline. */
  get edits(): number {
    return this.undoStack.length;
  }

  /** Returns whether anything actually changed, so callers can skip a redraw. */
  async paint(q: number, r: number, territory: string): Promise<boolean> {
    const model = await this.load();
    const k = key(q, r);
    if (!model.ownership.has(k)) return false;
    const current = this.overlay.get(k) ?? model.ownership.get(k);
    if (current === territory) return false;
    this.undoStack.push({ cell: k, prev: this.overlay.get(k) });
    this.overlay.set(k, territory);
    this.emit();
    return true;
  }

  /** Dev affordance: step back through local paint operations. */
  async undo(): Promise<void> {
    const last = this.undoStack.pop();
    if (!last) return;
    if (last.prev === undefined) this.overlay.delete(last.cell);
    else this.overlay.set(last.cell, last.prev);
    this.emit();
  }
}
