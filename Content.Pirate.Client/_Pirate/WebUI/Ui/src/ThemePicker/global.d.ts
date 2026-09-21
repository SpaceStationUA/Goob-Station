interface Window {
  __themeSetState?: (json: string | { current: string; allowed: string[] }) => void;
}
