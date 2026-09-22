import { createSignal, For, Show, onCleanup } from "solid-js";
import { postAction } from "../lib/bridge";
import { applyThemeId, ThemeProvider } from "../lib/theme";
import { GameWindow, Icon } from "../lib/kit";
import { IoSunny, IoMoon } from "solid-icons/io";
import "./picker.css";

/** Theme name/id prettifier; the description strings live here so server
 * stays terse. Keep in sync with Resources/Prototypes/_Pirate/webui.yml. */
const LABELS: Record<string, string> = {
  PirateNtWeb: "NanoTrasen blue",
  PirateNtAmber: "NT solar (amber)",
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

  window.__themeSetState = (json: string | ThemeState) => {
    try {
      onState(typeof json === "string" ? JSON.parse(json) : json);
    } catch { /* ignore */ }
  };

  // Pull-based state: pushes to a hidden page can be swallowed, so the
  // page keeps polling "list" until state lands and pulls again whenever
  // the state goes away (fresh mounts race the first server reply).
  const pull = async () => {
    try {
      const res = await postAction<ThemeState>("list", {});
      if (res.ok && res.data && (res.data as ThemeState).current)
        onState(res.data as ThemeState);
    } catch { /* retry next tick */ }
  };
  const syncTimer = window.setInterval(() => {
    try { if (!state()) void pull(); } catch { /* page teardown */ }
  }, 800);
  onCleanup(() => window.clearInterval(syncTimer));
  void pull();
  window.addEventListener("tui-push", (ev) => {
    const detail = (ev as CustomEvent).detail ?? {};
    void postAction("dbg", "tui-push name=" + (detail.name ?? "?"));
    if ((detail.name ?? "") !== "theme-state") return;
    onState(detail.payload as ThemeState);
  });

  function onState(s: ThemeState): void {
    setState(s);
    // Re-skin the picker itself to the device's current theme.
    applyThemeId(s.current);
    setBusy(false);
    void postAction("dbg", "theme-state n=" + s.allowed.length + " cur=" + s.current);
  }

  // Page is up: engine re-pull state (mirrors the radio page's handshake).

  postAction("ready", {});

  return (
    <ThemeProvider>
      <GameWindow>
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
          </Show>
        </div>
      </GameWindow>
    </ThemeProvider>
  );
}
