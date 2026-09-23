import { createSignal, onMount, onCleanup, For, Show, createMemo } from "solid-js";
import { createStore, produce } from "solid-js/store";
import { postAction } from "../lib/bridge";
import "./board.css";

const CELL = 52;
const CARD_W_CELLS = 5;
const CARD_H_CELLS = 4;
const GRID_W_FALLBACK = 24;
const GRID_H_FALLBACK = 14;

interface Card {
  id: number;
  kind: string;
  x: number;
  y: number;
  text: string;
  img?: string;
}

interface LinkData {
  id: number;
  a: number;
  b: number;
  label?: string;
}

interface CaseData {
  id: number;
  name: string;
  cards: Card[];
  links: LinkData[];
}

interface Board {
  v: number;
  w?: number;
  h?: number;
  theme?: string;
  current: number;
  cases: CaseData[];
}

function reconcileShaped(data: Board): Board {
  return {
    v: data.v,
    w: data.w,
    h: data.h,
    theme: data.theme,
    current: data.current,
    cases: data.cases,
  };
}

function themeClass(id: string | undefined): string {
  const slug = (id ?? "").replace(/^Pirate/, "").replace(/Web$/, "").toLowerCase() || "nt";
  return `theme-${slug}`;
}

/// Card body dispatch — each kind renders its own shape (the old
/// "char body as universal fallback" bug showed filler under chips).
function CardBody(props: { card: Card; onEdit?: () => void }) {
  return (
    <Show when={props.card.kind === "char"} fallback={<PlainBody card={props.card} onEdit={props.onEdit} />}>
      <CharBody card={props.card} />
    </Show>
  );
}

function PlainBody(props: { card: Card; onEdit?: () => void }) {
  return (
    <div class="note-text" onDblClick={(e) => { e.stopPropagation(); if (props.onEdit) props.onEdit(); }}>
      <Show
        when={props.card.kind === "photo"}
        fallback={<span>{props.card.text}</span>}
      >
        <div class="photo-frame">
          <Show when={props.card.img} fallback={<div class="photo-none" />}>
            <img class="photo-img" src={props.card.img} alt="photo" />
          </Show>
        </div>
        <div class="photo-caption">{(props.card.text.split("\n").slice(1).join("\n") || "").trim() || "photo"}</div>
      </Show>
    </div>
  );
}

/// Character card body: fields are stored \u001f-separated
/// (name|job|age|species|gender|prints|dna). DNA/prints are truncated.
function CharBody(props: { card: Card }) {
  const f = () => props.card.text.split("\u001f");
  const toss = (s: string | undefined, max = 10) => {
    const v = (s ?? "").trim();
    if (!v) return "—";
    return v.length > max ? v.slice(0, max) + "…" : v;
  };
  return (
    <div class="note-text char-text">
      <Show when={props.card.img} fallback={null}>
        <div class="portrait-frame">
          <img class="portrait-img" src={props.card.img} alt="portrait" />
        </div>
      </Show>
      <Show when={(f()[5] ?? "").trim() || (f()[6] ?? "").trim()} fallback={<span />}>
        <span class="char-dna">ID card data</span>
      </Show>
      <div class="char-name">{f()[0] || "unknown"}</div>
      <div>{f()[1] || "unknown job"}</div>
      <div>age {f()[2] || "—"}</div>
      <div>{f()[3] || "unknown species"} · {f()[4] || ""}</div>
      <div class="char-hashes">prints: {toss(f()[5])}</div>
      <div class="char-hashes">dna: {toss(f()[6])}</div>
    </div>
  );
}

export default function App() {
  const [board, setBoard] = createStore<Board>({ v: 1, current: 0, cases: [] });
  const [live, setLive] = createSignal(false);
  const [linkMode, setLinkMode] = createSignal(false);
  const [linkA, setLinkA] = createSignal<number | null>(null);
  const [noteText, setNoteText] = createSignal("");
  const [caseName, setCaseName] = createSignal("");
  const [linkLabel, setLinkLabel] = createSignal("");
  const [renaming, setRenaming] = createSignal<number | null>(null);
  const [editing, setEditing] = createSignal<number | null>(null);
  const [zoom, setZoom] = createSignal(1);

  let lastBoardKey = "";
  const applyState = (data: unknown) => {
    if (data && typeof data === "object" && "cases" in (data as Board)) {
      // Identical snapshots (the 2s sync poll when nobody moved) must not
      // rebuild the DOM: rebuilding resets card scroll positions.
      const key = JSON.stringify(data);
      if (key === lastBoardKey) return;
      lastBoardKey = key;
      setBoard(reconcileShaped(data as Board));
      setLive(true);
      // string mode / selection are NOT reset here: the 2s sync poll
      // re-applies state and would kill the link gesture mid-pick
    }
  };

  const zoomStep = (delta: number) =>
    setZoom(Math.min(1.6, Math.max(0.6, Math.round((zoom() + delta) * 10) / 10)));

  onMount(() => {
    window.__evidenceSetState = (json: string) => {
      try {
        applyState(JSON.parse(json));
      } catch {
        /* bad snapshot */
      }
    };
    const onPush = (ev: Event) => {
      const detail = (ev as CustomEvent).detail ?? {};
      if (String(detail.name ?? "") !== "board-state") return;
      const payload = detail.payload;
      try {
        applyState(typeof payload === "string" ? JSON.parse(payload) : payload);
      } catch {
        /* bad payload */
      }
    };
    window.addEventListener("tui-push", onPush);
    postAction("ready").then(() => postAction("sync"));
    const poll = setInterval(() => void postAction("sync"), 2000);
    onCleanup(() => {
      clearInterval(poll);
      window.removeEventListener("tui-push", onPush);
    });
  });

  const current = createMemo(() => {
    const b = live() ? board : null;
    if (!b) return null;
    for (const c of b.cases) {
      if (c.id === b.current) return c;
    }
    return b.cases[0] ?? null;
  });

  const boardW = createMemo(() => (live() ? board.w ?? GRID_W_FALLBACK : GRID_W_FALLBACK));
  const boardH = createMemo(() => (live() ? board.h ?? GRID_H_FALLBACK : GRID_H_FALLBACK));

  const nextFreeSpot = (): [number, number] => {
    const c = current();
    if (!c) return [0, 0];
    const taken = new Set<string>();
    for (const card of c.cards) taken.add(`${card.x},${card.y}`);
    const W = boardW();
    const H = boardH();
    for (let y = 0; y <= H - CARD_H_CELLS; y++) {
      for (let x = 0; x <= W - CARD_W_CELLS; x++) {
        let free = true;
        outer: for (let dy = 0; dy < CARD_H_CELLS; dy++) {
          for (let dx = 0; dx < CARD_W_CELLS; dx++) {
            if (taken.has(`${x + dx},${y + dy}`)) {
              free = false;
              break outer;
            }
          }
        }
        if (free) return [x, y];
      }
    }
    return [0, 0];
  };

  const addNote = () => {
    const text = noteText();
    const [cx, cy] = nextFreeSpot();
    postAction("addnote", `${cx}|${cy}|${text}`);
    setNoteText("");
  };

  const newCase = () => {
    postAction("newcase", caseName());
    setCaseName("");
  };

  // --- drag plumbing ---
  const [drag, setDrag] = createStore({ id: 0, gx: 0, gy: 0, startX: 0, startY: 0, cardX: 0, cardY: 0 });

  const onPointerDown = (ev: PointerEvent, card: Card) => {
    if (linkMode()) {
      if (linkA() === null) {
        setLinkA(card.id);
      } else {
        const a = linkA()!;
        setLinkA(null);
        if (a !== card.id) postAction("link", `${a}|${card.id}`);
      }
      return;
    }
    setDrag({
      id: card.id,
      gx: card.x,
      gy: card.y,
      startX: ev.clientX,
      startY: ev.clientY,
      cardX: card.x,
      cardY: card.y,
    });
  };

  const onPointerMove = (ev: PointerEvent) => {
    if (!drag.id) return;
    const nx = Math.min(
      Math.max(0, drag.cardX + Math.round((ev.clientX - drag.startX) / CELL)),
      boardW() - CARD_W_CELLS,
    );
    const ny = Math.min(
      Math.max(0, drag.cardY + Math.round((ev.clientY - drag.startY) / CELL)),
      boardH() - CARD_H_CELLS,
    );
    setDrag({ gx: nx, gy: ny });
  };

  const onPointerUp = () => {
    if (!drag.id) return;
    const { id, gx, gy } = drag;
    setDrag("id", 0);
    setBoard(
      produce((b) => {
        if (!b) return;
        const c = b.cases.find((x) => x.id === b.current) ?? b.cases[0];
        const card = c?.cards.find((x) => x.id === id);
        if (card) {
          card.x = gx;
          card.y = gy;
        }
      }),
    );
    postAction("move", `${id}|${gx}|${gy}`);
  };

  onMount(() => {
    window.addEventListener("pointermove", onPointerMove);
    window.addEventListener("pointerup", onPointerUp);
    onCleanup(() => {
      window.removeEventListener("pointermove", onPointerMove);
      window.removeEventListener("pointerup", onPointerUp);
    });
  });

  const cardCenter = (card: Card | undefined): [number, number] | null => {
    if (!card) return null;
    return [card.x * CELL + (CARD_W_CELLS * CELL) / 2, card.y * CELL + (CARD_H_CELLS * CELL) / 2];
  };

  return (
    <div class={`board-root ${themeClass(live() ? board.theme : undefined)}`}>
      <div class="board-header">
        <For each={live() ? board.cases : []}>
          {(c) => (
            <Show when={renaming() === c.id} fallback={
              <button
                class={"case-tab" + (c.id === board?.current ? " current" : "")}
                onClick={() => postAction("switchcase", String(c.id))}
                onDblClick={(e) => { e.stopPropagation(); setRenaming(c.id); }}
              >
                {c.name}
              </button>
            }>
              <input
                class="case-tab current"
                value={c.name}
                ref={(el) => { setTimeout(() => { el.focus(); el.select(); }, 10); }}
                onKeyDown={(e) => { if (e.key === "Enter") e.currentTarget.blur(); if (e.key === "Escape") { setRenaming(null); } }}
                onBlur={(e) => {
                  setRenaming(null);
                  postAction("renamecase", `${c.id}|${e.currentTarget.value}`);
                }}
              />
            </Show>
          )}
        </For>
        <Show when={live()}>
          <button class="hd-btn danger" onClick={() => postAction("delcase", String(board.current))}>
            ✕ case
          </button>
        </Show>
        <input
          placeholder="new case…"
          value={caseName()}
          onInput={(e) => setCaseName(e.currentTarget.value)}
          onKeyDown={(e) => e.key === "Enter" && newCase()}
        />
        <button class="hd-btn" onClick={newCase}>
          + case
        </button>
        <div style={{ flex: "1" }} />
        <button class={"hd-btn" + (linkMode() ? " linkmode" : "")} onClick={() => setLinkMode(!linkMode())}>
          {linkMode() ? (linkA() === null ? "string: pick 1st pin" : "string: pick 2nd pin") : "string mode"}
        </button>
        <input
          placeholder="string label…"
          style={{ width: "110px", "font-size": "11px" }}
          value={linkLabel()}
          onInput={(e) => setLinkLabel(e.currentTarget.value)}
        />
        <input
          placeholder="sticky note…"
          value={noteText()}
          onInput={(e) => setNoteText(e.currentTarget.value)}
          onKeyDown={(e) => e.key === "Enter" && addNote()}
        />
        <button class="hd-btn" onClick={addNote}>
          + note
        </button>
        <button class="hd-btn" onClick={() => postAction("printcase", String(current()?.id))}>
          print case
        </button>
        <button class="hd-btn" onClick={() => zoomStep(-0.2)}>
          −
        </button>
        <button class="hd-btn" onClick={() => setZoom(1)}>
          {Math.round(zoom() * 100)}%
        </button>
        <button class="hd-btn" onClick={() => zoomStep(0.2)}>
          +
        </button>
      </div>
      <div
        class="board-scroll"
        onWheel={(e) => {
          if (e.ctrlKey) {
            e.preventDefault();
            zoomStep(e.deltaY < 0 ? 0.1 : -0.1);
          }
        }}
      >
        <div
          class="board-zoom"
          style={{ width: `${boardW() * CELL * zoom()}px`, height: `${boardH() * CELL * zoom()}px` }}
        >
        <div
          class="board-canvas"
          style={{
            width: `${boardW() * CELL}px`,
            height: `${boardH() * CELL}px`,
            transform: `scale(${zoom()})`,
          }}
        >
          <svg class="board-strings">
            <For each={current()?.links ?? []}>
              {(l) => {
                const a = cardCenter(current()?.cards.find((x) => x.id === l.a));
                const b = cardCenter(current()?.cards.find((x) => x.id === l.b));
                if (!a || !b) return null;
                return (
                  <g>
                    <line
                      class="string-hit"
                      x1={a[0]}
                      y1={a[1]}
                      x2={b[0]}
                      y2={b[1]}
                      onClick={() => postAction("unlink", String(l.id))}
                    />
                    <line class="string" x1={a[0]} y1={a[1]} x2={b[0]} y2={b[1]} style={{ "pointer-events": "none" }} />
                  </g>
                );
              }}
            </For>
          </svg>
          <Show when={current()} fallback={<div class="board-empty">create a case to start pinning</div>}>
            <For each={current()!.cards}>
              {(card) => {
                const isDrag = () => drag.id === card.id;
                const x = () => (isDrag() ? drag.gx : card.x);
                const y = () => (isDrag() ? drag.gy : card.y);
                const isChar = () => card.kind === "char";
                return (
                  <div
                    class={
                      "sticky-note" +
                      (isChar() ? " char" : "") +
                      (card.kind === "chip" ? " chip" : "") +
                      (card.kind === "photo" ? " photo" : "") +
                      (isDrag() ? " dragging" : "") +
                      (linkA() === card.id ? " link-endA" : "")
                    }
                    style={{ left: `${x() * CELL + 4}px`, top: `${y() * CELL + 4}px` }}
                    onPointerDown={(ev) => {
                      if (editing() === card.id) return;
                      onPointerDown(ev, card);
                    }}
                  >
                    <button
                      class="note-del"
                      onPointerDown={(e) => e.stopPropagation()}
                      onClick={() => postAction("del", String(card.id))}
                    >
                      ✕
                    </button>
                    <Show
                      when={editing() === card.id}
                      fallback={<CardBody card={card} onEdit={card.kind === "note" ? () => setEditing(card.id) : undefined} />}
                    >
                      <textarea
                        class="note-editor"
                        ref={(el) => setTimeout(() => el.focus(), 10)}
                        value={card.text}
                        onPointerDown={(e) => e.stopPropagation()}
                        onPointerUp={(e) => e.stopPropagation()}
                        onClick={(e) => e.stopPropagation()}
                        onDblClick={(e) => e.stopPropagation()}
                        onKeyDown={(e) => {
                          if (e.key === "Escape") setEditing(null);
                          e.stopPropagation();
                        }}
                        onBlur={(e) => {
                          const t = e.currentTarget.value;
                          setEditing(null);
                          postAction("settext", `${card.id}|${t}`);
                        }}
                      />
                    </Show>
                  </div>
                );
              }}
            </For>
          </Show>
        </div>
        </div>
      </div>
    </div>
  );
}
