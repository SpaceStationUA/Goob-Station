import { onRadioCatalog, onRadioState, playerAction, type RadioCatalog, type RadioState } from "../lib/protocol";
import { createSignal } from "solid-js";

export default function App() {
  const [catalog, setCatalog] = createSignal<RadioCatalog | null>(null);
  const [state, setState] = createSignal<RadioState | null>(null);

  onRadioCatalog((c) => setCatalog(c));
  onRadioState((s) => setState(s));

  return (
    <div class="radio-phase-b-placeholder">
      <h1>Pirate Radio</h1>
      <p>Phase B will port the real page. This stub validates the bridge + build pipeline.</p>
      <p>
        catalog: {catalog() ? catalog()!.stations.length : "-"} stations · state:{" "}
        {state() ? `${state()!.stationId} (${state()!.playing})` : "-"}
      </p>
      <button onClick={() => playerAction("play", "test")}>test play action</button>
    </div>
  );
}
