import { vi } from "vitest";

/** Installs a controllable matchMedia implementation for theme behavior tests. */
export function installMatchMedia(initialMatches = false) {
  let matches = initialMatches;
  const listeners = new Set();

  const matchMedia = vi.fn((query) => ({
    media: query,
    get matches() {
      return matches;
    },
    onchange: null,
    addEventListener(type, listener) {
      if (type === "change") {
        listeners.add(listener);
      }
    },
    removeEventListener(type, listener) {
      if (type === "change") {
        listeners.delete(listener);
      }
    },
    addListener(listener) {
      listeners.add(listener);
    },
    removeListener(listener) {
      listeners.delete(listener);
    },
    dispatchEvent() {
      return true;
    },
  }));

  Object.defineProperty(window, "matchMedia", {
    configurable: true,
    writable: true,
    value: matchMedia,
  });

  return {
    listenerCount() {
      return listeners.size;
    },
    setMatches(nextMatches) {
      matches = nextMatches;
      const event = { matches, media: "(prefers-color-scheme: dark)" };
      listeners.forEach((listener) => listener(event));
    },
  };
}
