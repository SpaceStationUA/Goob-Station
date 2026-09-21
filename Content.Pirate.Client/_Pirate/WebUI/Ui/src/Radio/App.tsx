import { createSignal, createMemo, onMount, For, Show } from "solid-js";
import { createPlayer } from "./player";
import {
  onRadioCatalog, onRadioState, onRelayChunk, playerAction, dbg,
  type RadioCatalog, type RadioState, type StationEntry,
} from "../lib/protocol";
import { ThemeProvider, applyThemeId } from "../lib/theme";
import { GameWindow, Button, Icon } from "../lib/kit";
import { IoPlay, IoStop, IoClose } from "solid-icons/io";
import "./radio.css";

// Remote entries carry comma-separated tag lists; pinned ones a short
// genre line. Split on commas; group case-insensitively, display as-is.
function genresOf(s: StationEntry): string[] {
  const out: string[] = [];
  String(s.genre || "").split(",").forEach((g) => {
    const t = g.trim();
    if (t.length > 0 && !out.some((x) => x.toLowerCase() === t.toLowerCase()))
      out.push(t);
  });
  return out;
}

export default function App() {
  const player = createPlayer();

  const [stations, setStations] = createSignal<StationEntry[]>([], { equals: false });
  const [tab, setTab] = createSignal<"stations" | "genres">("stations");
  const [genre, setGenre] = createSignal<string | null>(null);
  const [starred, setStarred] = createSignal<Set<string>>(new Set());
  const [broken, setBroken] = createSignal<Set<string>>(new Set());
  const [vol, setVol] = createSignal(70);
  const [themeIds, setThemeIds] = createSignal<string[]>([]);
  const [appliedId, setAppliedId] = createSignal<string>("");

  function markBroken(id: string): void {
    setBroken((prev) => new Set(prev).add(id));
  }

  // ---- bridge: catalog/state/chunk + relay-ready action ----
  // Dual-channel intakes (tui-push events and the direct window globals);
  // whichever arrives first wins.
  onRadioCatalog((c: RadioCatalog) => setStations(c.stations));
  onRadioState((s: RadioState) => onState(s));
  onRelayChunk((b64) => player.feedChunk(b64));

  function onState(s: RadioState): void {
    if (!s.playing) {
      // A stale-echo guard: the ready handshake re-pushes the driver's
      // cached state, which can momentarily be empty/false while local
      // playback already runs; do not stop real audio for it.
      if (player.playing() && !s.stationId) return;
      player.stop();
      return;
    }
    // Already playing this station locally: the echo is our own report
    // coming back - do not restart the stream.
    if (player.playing() && s.stationId === player.currentId()) return;
    const st = stations().find((x) => x.id === s.stationId);
    if (st && !broken().has(st.id)) player.play(st);
  }

  function onCatalog(c: RadioCatalog): void {
    if (c.theme) { setAppliedId(c.theme); applyThemeId(c.theme); }
    setThemeIds(c.themes ?? []);
    setStations(c.stations);
  }

  window.__radioSetCatalog = (json: string | RadioCatalog) => {
    try {
      onCatalog(typeof json === "string" ? JSON.parse(json) : json);
    } catch { /* ignore */ }
  };
  window.__radioSetState = (json: string | RadioState) => {
    try {
      onState(typeof json === "string" ? JSON.parse(json) : json);
    } catch { /* ignore */ }
  };
  window.__radioRelayChunk = (b64: string) => { player.feedChunk(b64); };
  // Same contract as the hand page: C# calls this before disposing the
  // playback control (e.g. the PDA was destroyed) or the CEF browser would
  // keep the audio going as an orphan.
  window.__radioHardStop = () => { player.stop(); };

  player.setRelayReady(() => { playerAction("relayready"); });
  player.setDiag((m) => { dbg(m); });

  // The page has its listeners up; ask the engine to re-send anything that
  // was pushed during the load window (catalog arrives once and is deduped
  // server-side, so a dropped push would otherwise never be seen).
  onMount(() => {
    playerAction("ready");
  });

  // The player sets the status; the catalog side remembers the failure and
  // greys the station out for the session.
  player.audio.addEventListener("error", () => {
    if (player.playing() && player.currentId()) markBroken(player.currentId());
  });

  // ---- actions ----
  function select(id: string): void {
    if (broken().has(id)) return;
    if (player.playing() && id === player.currentId()) { doStop(); return; }
    // Play locally right away: this runs inside the click's user-gesture
    // window, which Chromium's autoplay policy requires. The server report
    // is a notification, not a round trip (state echoes are deduped).
    const st = stations().find((x) => x.id === id);
    if (!st) return;
    player.play(st);
    playerAction("play", id);
  }

  function doStop(): void {
    player.stop();
    playerAction("stop");
  }

  function toggleStar(id: string): void {
    setStarred((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  const defaultOn = () => {
    if (broken().has(player.currentId())) return;
    const next = stations().filter((s) => !broken().has(s.id));
    if (next.length > 0) select(next[0].id);
  };

  function onPlayButton(): void {
    if (player.playing()) { doStop(); return; }
    const cur = player.currentId();
    if (cur && !broken().has(cur)) { select(cur); return; }
    defaultOn();
  }

  // ---- render ----
  const sorted = createMemo(() =>
    stations().slice().sort((a, b) =>
      ((starred().has(b.id) ? 1 : 0) - (starred().has(a.id) ? 1 : 0)) ||
      ((b.featured ? 1 : 0) - (a.featured ? 1 : 0)),
    ),
  );

  const genreGroups = createMemo(() => {
    const groups = new Map<string, { name: string; count: number }>();
    stations().forEach((s) => {
      if (broken().has(s.id)) return;
      genresOf(s).forEach((g) => {
        const k = g.toLowerCase();
        const cur = groups.get(k);
        if (cur) cur.count++;
        else groups.set(k, { name: g, count: 1 });
      });
    });
    return [...groups.values()].sort((a, b) =>
      b.count - a.count || a.name.localeCompare(b.name));
  });

  const inGenre = createMemo(() =>
    stations().filter((s) =>
      !broken().has(s.id) &&
      genresOf(s).some((x) => x.toLowerCase() === genre()),
    ).sort((a, b) =>
      ((starred().has(b.id) ? 1 : 0) - (starred().has(a.id) ? 1 : 0)),
    ),
  );

  return (
    <ThemeProvider>
      <GameWindow title="Pirate Radio" titlebar={
        <Show when={themeIds().length > 1}>
          <span class="theme-switch">
            <For each={themeIds()}>{(t) =>
              <button
                class={"theme-chip" + (appliedId() === t ? " active" : "")}
                title={t}
                onClick={() => { playerAction("theme", undefined, undefined, t); }}
              >
                {t === "PirateSyndiWeb" ? "\u25cf" : "\u25cb"}
              </button>
            }</For>
          </span>
        </Show>
      }>
        <div class="app">
          <div class="now">
            <div class={"eq" + (player.playing() ? "" : " paused")}><i /><i /><i /><i /></div>
            <div class="meta">
              <div class="station-label">{player.meta().label}</div>
              <div class="genre-line">{player.meta().genre}</div>
            </div>
            <div class="status-line">{player.status()}</div>
          </div>

          <div class="controls">
            <Button variant={player.playing() ? undefined : "accent"} onClick={onPlayButton}>
              <Icon p={player.playing() ? <IoStop /> : <IoPlay />} />
              {player.playing() ? "Stop" : "Play"}
            </Button>

            <Button onClick={doStop}>
              <Icon p={<IoClose />} /> Stop
            </Button>
            <input class="vol" type="range" min={0} max={100}
              value={vol()}
              onInput={(e) => {
                const v = Number(e.currentTarget.value);
                setVol(v);
                player.setVolume(v);
                playerAction("volume", undefined, v);
              }} />
            <span class="volpct">{vol()}%</span>
          </div>

          <div class="tabs">
            <button class={"tab" + (tab() === "stations" ? " active" : "")} onClick={() => { setTab("stations"); setGenre(null); }}>Stations</button>
            <button class={"tab" + (tab() === "genres" ? " active" : "")} onClick={() => { setTab("genres"); setGenre(null); }}>Genres</button>
          </div>

          <div class="list">
            <Show when={tab() === "genres"} fallback={
              <For each={sorted()}>{(s) =>
                <StationRow s={s} active={s.id === player.currentId() && player.playing()}
                  dead={broken().has(s.id)} starred={starred().has(s.id)}
                  onSelect={select} onStar={toggleStar} />
              }</For>
            }>
              <Show when={genre() == null} fallback={
                <>
                  <div class="back" onClick={() => setGenre(null)}>{"\u2190 all genres"}</div>
                  <For each={inGenre()}>{(s) =>
                    <StationRow s={s} active={s.id === player.currentId() && player.playing()}
                      dead={broken().has(s.id)} starred={starred().has(s.id)}
                      onSelect={select} onStar={toggleStar} />
                  }</For>
                </>
              }>
                <For each={genreGroups()}>{(g) =>
                  <div class="genre-row" onClick={() => setGenre(g.name.toLowerCase())}>
                    <span class="gname">{g.name}</span>
                    <span class="cnt">{g.count}</span>
                  </div>
                }</For>
              </Show>
            </Show>
          </div>
        </div>
      </GameWindow>
    </ThemeProvider>
  );
}

function StationRow(props: {
  s: StationEntry;
  active: boolean;
  dead: boolean;
  starred?: boolean;
  onSelect: (id: string) => void;
  onStar: (id: string) => void;
}) {
  const cls = () =>
    "station-row" + (props.active ? " active" : "") + (props.dead ? " dead" : "");
  return (
    <div
      class={cls()}
      title={props.s.label}
      onClick={() => !props.dead && props.onSelect(props.s.id)}
    >
      <span
        class={"star" + (props.starred ? "" : " off")}
        onClick={(e) => { e.stopPropagation(); props.onStar(props.s.id); }}
      >
        {props.starred ? "\u2605" : "\u2606"}
      </span>
      <span class="name">{props.s.label}</span>
      <span class="g">{props.s.genre || ""}</span>
      <Show when={props.dead}><span class="g">unavailable</span></Show>
    </div>
  );
}
