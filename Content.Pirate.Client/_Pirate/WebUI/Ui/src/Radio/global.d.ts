// Direct (pre-event-channel) intake globals the C# driver also mirrors
// pushes onto. Both channels deliver identical payloads.
interface Window {
  __radioSetCatalog?: (json: string | { stations: import("../lib/protocol").StationEntry[] }) => void;
  __radioSetState?: (json: string | import("../lib/protocol").RadioState) => void;
  __radioRelayChunk?: (b64: string) => void;
  __radioHardStop?: () => void;
}
