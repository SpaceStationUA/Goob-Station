import { For, Show, createMemo, createSignal, onMount } from "solid-js";
import { postAction } from "../lib/bridge";
import { act, setBackendState, state } from "../lib/backend";

// -- Types -------------------------------------------------------------------

interface ItemData {
  ref: string;
  name: string;
  desc: string;
  cost: number;
  category: string;
  icon: string;
  purchase_limit: number;
  purchased: number;
}

interface UplinkData {
  title: string;
  theme: string;
  credits: number;
  vr: boolean;
  can_lock: boolean;
  can_self_destruct: boolean;
  self_destructing: boolean;
  categories: string[];
  items: ItemData[];
}

const readState = createMemo(() => state() as UplinkData);

// -- App ---------------------------------------------------------------------

export const App = () => {
  const [error, setError] = createSignal("");

  const refresh = async () => {
    const res = await postAction<UplinkData>("uplink_state", {});
    if (res.ok) setBackendState(res.data);
    else setError(res.error);
  };

  onMount(refresh);

  const buy = async (item: ItemData) =>
    act<UplinkData>("buy", { ref: item.ref }, true);

  const lock = async () => act("lock", {});

  const selfDestruct = async () => act("self_destruct", {}, false);

  const data = readState;
  const [search, setSearch] = createSignal("");
  const [categoryFilter, setCategoryFilter] = createSignal<string>("");

  const filteredItems = createMemo(() => {
    const items = data()?.items ?? [];
    const query = search().toLowerCase();
    const cat = categoryFilter();
    return items.filter(
      (item) =>
        (cat === "" || item.category === cat) &&
        (query === "" ||
          item.name.toLowerCase().includes(query) ||
          item.desc.toLowerCase().includes(query)),
    );
  });

  return (
    <div class="tui-window" style={{ "--tui-accent": themeColor(data()?.theme ?? "syndicate") }}>
      <div class="tui-body">
        <div class="tui-sidebar">
          <div class="tui-section">
            <div>
              Credits:{" "}
              <span class="tui-credits">{data()?.credits ?? 0}</span> TC
            </div>
          </div>

          <Show when={!!data()?.can_lock || !!data()?.can_self_destruct}>
            <div class="tui-section">
              <Show when={!!data()?.can_lock}>
                <button class="tui-btn good" onClick={lock}>
                  🔒 Lock Uplink
                </button>
              </Show>
              <Show when={!!data()?.can_self_destruct}>
                <button class="tui-btn bad" onClick={selfDestruct}>
                  💣 Self Destruct
                </button>
              </Show>
            </div>
          </Show>

          <div class="tui-section">
            <input
              class="tui-input"
              placeholder="Search..."
              value={search()}
              onInput={(e) => setSearch(e.currentTarget.value)}
            />
          </div>
        </div>

        <div class="tui-main">
          <div class="tui-tabs">
            <For each={data()?.categories ?? []}>
              {(cat) => (
                <div
                  class="tui-tab"
                  classList={{ active: categoryFilter() === cat }}
                  onClick={() => setCategoryFilter(cat)}
                >
                  {cat}
                </div>
              )}
            </For>
          </div>

          <div class="tui-section" style={{ flex: "1", overflow: "auto" }}>
            <For each={filteredItems()}>
              {(item) => (
                <div class="tui-item">
                  <Show when={item.icon}>
                    <img src={item.icon} />
                  </Show>
                  <div style={{ flex: "1", "min-width": "0" }}>
                    <div>{item.name}</div>
                    <span class="tui-desc">
                      {item.purchased}/{item.purchase_limit}
                    </span>
                    <span class="tui-desc" innerHTML={item.desc} />
                  </div>
                  <div class="tui-item-cost">{item.cost} TC</div>
                  <button
                    class="tui-btn good"
                    style={{ "max-width": "90px", flex: "0 0 90px" }}
                    disabled={item.purchased >= item.purchase_limit}
                    onClick={() => buy(item)}
                  >
                    Buy
                  </button>
                </div>
              )}
            </For>
          </div>
        </div>
      </div>

      <Show when={!!data()?.self_destructing}>
        <div class="tui-modal-overlay">
          <div class="tui-modal">
            SELF DESTRUCT
            <br />
            ⚠ ACTIVATED ⚠
          </div>
        </div>
      </Show>

      <Show when={error()}>
        <div class="tui-error">{error()}</div>
      </Show>
    </div>
  );
};

const themeColor = (theme: string): string => {
  switch (theme) {
    case "neutral":
      return "#28a745";
    case "syndicate":
    default:
      return "#dc3545";
  }
};
