declare global {
  interface Window {
    __evidenceSetState?: (snapshotJson: string) => void;
    __evidenceSetLocale?: (map: Record<string, string>) => void;
  }
}

export {};
