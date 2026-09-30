import { cellsInExtent, key, type Axial } from "./hex";
import { assignCells, type AssignResult } from "./geometry";
import type {
  Contested,
  GalaxyModel,
  Ownership,
  Route,
  StarSystem,
  Territory,
  TerritoryClaim,
} from "./model";

/**
 * Where the page gets its data.
 *
 * The page body never calls this directly — it receives a model and renders
 * it. That is the whole reason the identical code runs in a browser against a
 * fixture and in the game against the bridge, and it is why the two cannot
 * drift apart in their rendering.
 */
/**
 * What the viewer is allowed to do.
 *
 * Painting is an admin tool, so the page and an ordinary player get the same
 * read-only chart and only a flagged admin sees the brushes. Keeping this on the
 * seam rather than a compile-time flag means the shipped build is the same binary
 * for everyone and the difference is data.
 */
export interface GalaxyPermissions {
  paint: boolean;
}

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
  /**
   * Flag or unflag a cell as disputed. Separate from `paint` on purpose:
   * marking a border as contested says nothing about who owns it, and folding
   * the two together would mean painting over a cell to say "both of us want
   * this", which throws away the owner.
   */
  setContested?(cellQ: number, cellR: number, on: boolean): Promise<boolean>;
  /**
   * Apply a whole stroke in one edit.
   *
   * Exists because a drag is one gesture, not N gestures. Rebuilding per cell
   * would re-run the wobble over every territory outline on every mousemove and
   * leave an undo stack with one entry per cell, so a single flick of the mouse
   * would take twenty undos to put back. One call, one rebuild, one undo step —
   * which is also what a person means by "undo that".
   */
  stroke?(cells: readonly Axial[], territory: string): Promise<boolean>;
  /** Batch form of `setContested`, for dragging a flag along a border. */
  contestStroke?(cells: readonly Axial[], on: boolean): Promise<boolean>;
  /**
   * What this client may do. In game the chart is read-only for players and only
   * an admin gets the brushes, so the page asks instead of assuming and hides
   * the tools it cannot use. Absent means "everything", which is the fixture.
   */
  readonly permissions?: GalaxyPermissions;
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
    // Baked-in disputes come straight from the assignment pass. They are data,
    // not a log line: the depth rule settled these cells, but "settled" and
    // "agreed" are different things and the chart should be able to say so.
    contested: new Set(contested.map(c => key(c.cell.q, c.cell.r))),
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
  /**
   * Sparse deltas on the contested set, mirroring `overlay`.
   *
   * `null` means "clear the flag", `undefined` means "not touched by this
   * session". The distinction matters: a cell that the bake marked disputed and
   * an admin then un-marked must not spring back to disputed on the next edit.
   */
  private contestedOverlay = new Map<string, boolean>();
  /**
   * One entry per gesture, not per cell.
   *
   * `deltas` records only what the gesture actually changed, so stepping back
   * over a drag reverts the whole stroke in one move and leaves fields the
   * stroke did not touch — the owner, when the gesture only moved a dispute
   * flag — exactly as they were.
   */
  private undoStack: { deltas: { cell: string; owner?: string; contested?: boolean }[] }[] = [];

  /** True when the fixture lets the viewer edit. It always does. */
  readonly permissions: GalaxyPermissions = { paint: true };
  private current: GalaxyModel | null = null;

  constructor(
    private spec: MapSpec,
    private territories: Territory[],
    private claims: TerritoryClaim[],
    private systems: StarSystem[] = [],
    private routes: Route[] = [],
  ) {}

  /**
   * Build a fresh model from the pristine baseline plus the overlays.
   *
   * The model, its ownership map and its contested set are all new objects every
   * time. That is not incidental: the view is Solid, so handing it the same
   * object it already holds — even after mutating that object's contents —
   * changes nothing on screen. Painting appeared to do nothing at all for
   * exactly this reason.
   */
  private snapshot(): GalaxyModel {
    const base = buildModel(this.spec, this.territories, this.claims, this.systems, this.routes);
    if (this.overlay.size === 0 && this.contestedOverlay.size === 0) return base;
    const ownership: Ownership = new Map(base.ownership);
    for (const [cell, id] of this.overlay) ownership.set(cell, id);
    const contested: Contested = new Set(base.contested);
    for (const [cell, on] of this.contestedOverlay) {
      if (on) contested.add(cell);
      else contested.delete(cell);
    }
    return { ...base, ownership, contested, revision: base.revision + 1 };
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
    return this.stroke([{ q, r }], territory);
  }

  async setContested(q: number, r: number, on: boolean): Promise<boolean> {
    return this.contestStroke([{ q, r }], on);
  }

  /**
   * Apply a whole drag in one edit.
   *
   * Cells already in the requested state are skipped rather than recorded, so
   * dragging back and forth over a border does not fill the undo stack with
   * no-ops, and a stroke that ends up changing nothing leaves no trace at all.
   */
  async stroke(cells: readonly Axial[], territory: string): Promise<boolean> {
    const model = await this.load();
    const deltas: { cell: string; owner?: string }[] = [];
    for (const c of cells) {
      const k = key(c.q, c.r);
      if (!model.ownership.has(k)) continue;
      if ((this.overlay.get(k) ?? model.ownership.get(k)) === territory) continue;
      deltas.push({ cell: k, owner: territory });
    }
    if (deltas.length === 0) return false;
    for (const d of deltas) this.overlay.set(d.cell, d.owner!);
    this.undoStack.push({ deltas });
    this.emit();
    return true;
  }

  async contestStroke(cells: readonly Axial[], on: boolean): Promise<boolean> {
    const model = await this.load();
    const deltas: { cell: string; contested: boolean }[] = [];
    for (const c of cells) {
      const k = key(c.q, c.r);
      if (!model.ownership.has(k)) continue;
      if ((this.contestedOverlay.get(k) ?? model.contested.has(k)) === on) continue;
      deltas.push({ cell: k, contested: on });
    }
    if (deltas.length === 0) return false;
    for (const d of deltas) this.contestedOverlay.set(d.cell, d.contested);
    this.undoStack.push({ deltas });
    this.emit();
    return true;
  }

  /**
   * Dev affordance: step back through local edits, one gesture at a time.
   *
   * An absent field means "revert to the baked baseline".
   */
  async undo(): Promise<void> {
    const last = this.undoStack.pop();
    if (!last) return;
    for (const d of last.deltas) {
      if (d.owner !== undefined) this.overlay.delete(d.cell);
      if (d.contested !== undefined) this.contestedOverlay.delete(d.cell);
    }
    this.emit();
  }
}
