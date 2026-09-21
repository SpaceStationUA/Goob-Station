import { createSignal, For, Show } from "solid-js";
import { postAction } from "../lib/bridge";
import { applyThemeId, ThemeProvider } from "../lib/theme";
import { GameWindow, Icon } from "../lib/kit";
import { IoSunny, IoMoon } from "solid-icons/io";
import "./picker.css";

/** Theme name/id prettifier; the description strings live here so server
 * stays terse. Keep in sync with Resources/Prototypes/_Pirate/webui.yml. */
const LABELS: Record<string, string> = {
  PirateNtWeb: "NanoTrasen blue",
  PirateSyndiWeb: "Syndicate red",
};

// The page self-themes to the device's current skin via the initial state.
interface ThemeState {
  current: string;
  allowed: string[];
}

export default function App() {
  const [state, setState] = createSignal<ThemeState | null>(null);
  const [busy, setBusy] = createSignal(false);
  const [probe, setProbe] = createSignal("probe: init");
  window.setInterval(() => setProbe(perf()), 500);

  function perf(): string {
    try {
      return [
        "hasThemeSetState=" + (String(typeof (window as any).__themeSetState)),
        "state=" + (state() ? JSON.stringify(state()) : "null"),
      ].join("\n");
    } catch (e) {
      return "probe err " + String(e);
    }
  }

  window.__themeSetState = (json: string | ThemeState) => {
    try {
      onState(typeof json === "string" ? JSON.parse(json) : json);
    } catch { /* ignore */ }
  };

  // Pull-based state: poll postAction("list") until the engine answers
  // with {current, allowed}; the reply path (postAction -> Respond) is the
  // proven connection, engine->page pushes are best-effort only.
  const syncTimer = window.setInterval(async () => {
    try {
      const res = await postAction<ThemeState>("list", {});
      if (res.ok && res.data && res.data.current)
        onState(res.data);
    } catch { /* retry next tick */ }
  }, 700);
  window.addEventListener("tui-push", (ev) => {
    const detail = (ev as CustomEvent).detail ?? {};
    void postAction("dbg", "tui-push name=" + (detail.name ?? "?"));
    if ((detail.name ?? "") !== "theme-state") return;
    onState(detail.payload as ThemeState);
  });

  function onState(s: ThemeState): void {
    setState(s);
    window.clearInterval(syncTimer);
    // Re-skin the picker itself to the device's current theme.
    applyThemeId(s.current);
    setBusy(false);
    void postAction("dbg", "theme-state n=" + s.allowed.length + " cur=" + s.current);
  }

  // Page is up: engine re-pull state (mirrors the radio page's handshake).
  void postAction("dbg", "picker mounted");
  postAction("ready", {});

  return (
    <ThemeProvider>
      <GameWindow title="PDA theme">
        <div class="picker">
          <p class="hint">Applies to this PDA's windows (radio and future apps).</p>
          <div class="list">
            <For each={state()?.allowed ?? []}>{(t) =>
              <button
                class={"theme-card" + (state()?.current === t ? " active" : "")}
                disabled={busy() || state()?.current === t}
                onClick={async () => {
                  setBusy(true);
                  await postAction("set", { theme: t });
                }}
              >
                <Icon p={t === "PirateSyndiWeb" ? <IoMoon /> : <IoSunny />} />
                <span class="name">{LABELS[t] ?? t}</span>
                <Show when={state()?.current === t}><span class="cur">current</span></Show>
              </button>
            }</For>
          </div>
          <Show when={!state()}>
            <p class="hint">waiting for device state…</p>
            <p class="probe" style={{ "font-family": "monospace", "font-size": "10px", "white-space": "pre-wrap" }}>
              {probe()}
            </p>
          </Show>
        </div>
      </GameWindow>
    </ThemeProvider>
  );
}
