// Radio playback: bare <audio> for direct streams + MediaSource relay
// for transcoded ones. Faithful port of the hand-built page's playback
// core (see Resources/_Pirate/WebUI/Radio in the phase-A history).

import { createSignal } from "solid-js";
import type { StationEntry } from "../lib/protocol";

export function createPlayer() {
  const [currentId, setCurrentId] = createSignal("");
  const [playing, setPlaying] = createSignal(false);
  const [relayMode, setRelayMode] = createSignal(false);
  const [status, setStatus] = createSignal("idle");
  const [meta, setMeta] = createSignal({ label: "Pirate Radio", genre: "pick a station" });

  const audio = new Audio();
  audio.preload = "none";
  // NOTE: no crossOrigin attribute. It would switch the media fetch into
  // CORS mode, and stream servers without Access-Control-Allow-Origin
  // (most Icecast endpoints) would fail to load - request goes out,
  // playback rejects. Plain no-cors media playback is all we need.

  // ---- server relay (transcoded WebM/Opus over the game connection) ----

  let relayMs: MediaSource | null = null;
  let relayBuffer: SourceBuffer | null = null;
  let relayChunks: Uint8Array[] = [];
  let relayUrl = "";

  function startRelay(onReady: () => void) {
    teardownRelay();
    setRelayMode(true);
    relayMs = new MediaSource();
    relayUrl = URL.createObjectURL(relayMs);
    relayMs.addEventListener("sourceopen", () => {
      if (!relayMode()) return;
      try {
        relayBuffer = relayMs!.addSourceBuffer('audio/webm; codecs="opus"');
      } catch {
        relayBuffer = null;
      }
      if (!relayBuffer) {
        setStatus("relay unsupported");
        return;
      }
      relayBuffer.addEventListener("updateend", feedRelay);
      try { relayMs!.duration = Infinity; } catch { /* live: ok to fail */ }
      // C# may now start pushing transcoded chunks.
      onReady();
      feedRelay();
    });
    audio.src = relayUrl;
    audio.load();
  }

  function feedRelay() {
    if (!relayBuffer || relayBuffer.updating || relayChunks.length === 0)
      return;
    try {
      relayBuffer.appendBuffer(relayChunks.shift()!.buffer as ArrayBuffer);
    } catch {
      // Usually quota (too far behind): drop queued chunks and re-anchor to
      // the live edge rather than dying in a buffer loop.
      relayChunks = [];
    }
  }

  // Keep a relay session glued to the live edge: if playback drifts behind
  // the buffered range (bg pauses, reconnect), re-anchor and resume.
  setInterval(() => {
    if (!playing() || !relayMode() || !relayBuffer || relayBuffer.buffered.length === 0)
      return;
    const start = relayBuffer.buffered.start(0);
    if (audio.paused && audio.currentTime < start - 0.5) {
      audio.currentTime = start;
      audio.play().catch(() => { /* gesture needed */ });
    }
  }, 2000);

  function teardownRelay() {
    setRelayMode(false);
    relayChunks = [];
    if (relayBuffer) { try { relayBuffer.abort(); } catch { /* noop */ } }
    if (relayUrl) { try { URL.revokeObjectURL(relayUrl); } catch { /* noop */ } }
    relayBuffer = null;
    relayMs = null;
    relayUrl = "";
  }

  /** Ingest a base64 relay chunk (order-sensitive, MSE append). */
  function feedChunk(b64: string) {
    if (!relayMode()) return;
    try {
      const bin = atob(b64);
      const u8 = new Uint8Array(bin.length);
      for (let i = 0; i < bin.length; i++) u8[i] = bin.charCodeAt(i);
      relayChunks.push(u8);
      // Bound the queue: if MSE falls hopelessly behind, jump to live tail.
      while (relayChunks.length > 64) relayChunks.shift();
      feedRelay();    } catch { /* ignore malformed chunk */ }
  }

  // ---- play/stop ----

  function play(s: StationEntry) {
    diag("play " + s.id + (s.relay ? " relay" : " direct") + " " + caller());
    setCurrentId(s.id);
    setPlaying(true);
    setRelayMode(s.relay);
    setMeta({ label: s.label, genre: (s.genre || "") + (s.relay ? " \u00b7 server relay" : "") });
    setStatus("tuning...");
    if (s.relay) {
      // The client CEF cannot decode this stream; the server transcodes it
      // and pushes WebM/Opus chunks over the game connection. Prepare the
      // MediaSource first: the optimistic play() below then stays pending
      // until the first chunk arrives, which keeps the click's
      // user-gesture alive for the autoplay policy.
      startRelay(onReady);
    } else {
      audio.src = s.url;
      audio.load();
    }
    audio.play().catch((e: DOMException) => {
      setStatus("cannot play: " + (e && e.name ? e.name : "unknown"));
      setPlaying(false);
      diag("play-reject " + (e && e.name) + ": " + (e && e.message) + " backlog=(dismissed)");
    });
  }

  let onReady = () => { /* replaced by App via setRelayReady */ };

  function setRelayReady(fn: () => void): void { onReady = fn; }

  let diag = (what: string) => { /* replaced by App via setDiag */ };

  function caller(): string {
    const st = new Error().stack ?? "";
    return st.split("\n").slice(1, 5).join(" | ");
  }

  function setDiag(fn: (what: string) => void): void { diag = fn; }
  function stop(): void {
    diag("stop() " + caller());
    setPlaying(false);
    setCurrentId("");
    audio.pause();
    audio.removeAttribute("src");
    audio.load();
    teardownRelay();
    setMeta({ label: "Pirate Radio", genre: "pick a station" });
    setStatus("idle");
  }

  audio.addEventListener("playing", () => { setStatus("on air"); diag("playing " + currentId()); });
  audio.addEventListener("waiting", () => setStatus("buffering"));
  audio.addEventListener("error", () => {
    if (!playing()) return;
    setStatus("stream error");
    diag("media-error " + currentId() + " " + caller());
  });
  // The page marks the station broken via the returned onError callback
  // (the player doesn't know the catalog).

  audio.volume = 0.7;

  return {
    audio,
    currentId, playing, relayMode, status, meta,
    play, stop, feedChunk, setDiag,
    setVolume: (v: number) => { audio.volume = v / 100; },
    setRelayReady,
  };
}
