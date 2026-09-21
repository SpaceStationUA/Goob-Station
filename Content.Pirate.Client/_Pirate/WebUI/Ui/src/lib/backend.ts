import { createSignal } from "solid-js";
import type { BridgeResult } from "./bridge";
import { postAction } from "./bridge";

/** Global state store holding the latest backend snapshot. */
export type BackendState = unknown;

const [state, setState] = createSignal<BackendState>({});

export { state };

export function setBackendState(s: BackendState) {
  setState(s);
}

/** Hook: dispatch an action; on success refresh local state from response. */
export async function act<T = unknown>(
  action: string,
  data?: unknown,
  refresh = true,
): Promise<BridgeResult<T>> {
  const result = await postAction<T>(action, data);
  if (result.ok && refresh) setState((s) => Object.assign({}, s, result.data as Partial<T>));
  return result;
}
