declare global {
  interface Window {
    __evidenceSetState?: (snapshotJson: string) => void;
  }
}

export {};
