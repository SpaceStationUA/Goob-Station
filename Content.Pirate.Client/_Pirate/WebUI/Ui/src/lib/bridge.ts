/**
 * Pirate TUI bridge client.
 *
 * Transport: hidden-iframe navigation (Nova-style deferred iframe).
 *   The page requests an action by navigating an invisible iframe to
 *     res://<pack>/.../tui_bridge/<tx>?action=X[&data=Y]
 *   The engine intercepts the navigation (before-browse), cancels it, and
 *   later delivers the reply by executing
 *     window.__tuiDispatch("<tx>", "<payloadJson>")
 *   which resolves the pending promise via the `tui-dispatch` CustomEvent.
 *
 *   No fetch. Plain stock engine 277 works as long as `res` stays a standard
 *   scheme; replies use the CEF main-frame ExecuteJavaScript channel. Works
 *   equally over the Vite dev origin (the browser context is the game
 *   client in both cases — HMR only changes the HTML origin).
 *
 * Replies use the envelope: {"status":"ok","data":...} | {"status":"error","error":"..."}.
 */

export type BridgeResult<T> =
  | { ok: true; data: T }
  | { ok: false, error: string };

let txCounter = 0;

interface Pending {
  resolve: (r: BridgeResult<unknown>) => void;
  timer: number;
}

const pending = new Map<string, Pending>();

// Install engine->page receiver (idempotent; also re-installed by the engine).
declare global {
  interface Window {
    __tuiDispatch?: (tx: string, payloadJson: string) => void;
    __tuiPush?: (name: string, payloadJson: string) => void;
    __tuiFetchProbe?: (action: string, data?: string) => Promise<BridgeResult<unknown>>;
  }
}

export function bridgeUrl(): string {
  return location.href;
}

function spawnIframe(url: string): void {
  const f = document.createElement("iframe");
  f.style.display = "none";
  f.style.width = "1px";
  f.style.height = "1px";
  f.addEventListener("load", () => { try { f.remove(); } catch { /* noop */ } });
  f.addEventListener("error", () => { try { f.remove(); } catch { /* noop */ } });
  f.src = url;
  document.body.appendChild(f);
  // Safety: never leak iframes if the navigation is swallowed entirely.
  setTimeout(() => { try { f.remove(); } catch { /* noop */ } }, 5000);
}

function tuiUrl(tx: string, action: string, data?: string): string {
  const baseDir = location.pathname.replace(/(\/index\.html)$|\/$/, "");
  // e.g. res://_Pirate/WebUI/Uplink -> res://_Pirate/WebUI/Uplink/tui_bridge/<tx>
  return (
    location.protocol +
    "//" +
    location.host +
    baseDir +
    "/tui_bridge/" +
    encodeURIComponent(tx) +
    "?action=" +
    encodeURIComponent(action) +
    (data ? `&data=${encodeURIComponent(data)}` : "")
  );
}

window.addEventListener("tui-dispatch", (ev) => {
  const detail = (ev as CustomEvent).detail ?? {};
  const tx: string | undefined = detail.tx;
  if (!tx || !pending.has(tx)) return;
  const p = pending.get(tx)!;
  pending.delete(tx);
  try { window.clearTimeout(p.timer); } catch { /* noop */ }
  const payload = detail.payload ?? {};
  if (payload.status !== "ok") {
    p.resolve({ ok: false, error: String(payload.error ?? "unknown error") });
    return;
  }
  p.resolve({ ok: true, data: payload.data });
});

/** Pushed (unsolicited) events from the engine, e.g. state updates. */
export function onPush(handler: (name: string, payload: unknown) => void): void {
  window.addEventListener("tui-push", (ev) => {
    const detail = (ev as CustomEvent).detail ?? {};
    handler(String(detail.name ?? ""), detail.payload);
  });
}

export async function postAction<T = unknown>(
  action: string,
  data?: unknown,
): Promise<BridgeResult<T>> {
  const payload =
    data === undefined ? undefined : JSON.stringify(data);
  const tx = `t${Date.now()}_${txCounter++}`;

  return new Promise((resolve) => {
    const p: Pending = {
      resolve: resolve as (r: BridgeResult<unknown>) => void,
      timer: window.setTimeout(() => {
        if (pending.delete(tx)) {
          resolve({ ok: false, error: "bridge-timeout" });
        }
      }, 10000),
    };
    pending.set(tx, p);
    spawnIframe(tuiUrl(tx, action, payload));
  });
}

// DevTools/CDP probe hook, also handy for integration diagnostics.
window.__tuiFetchProbe = (action: string, data?: string) => postAction(action, data);
