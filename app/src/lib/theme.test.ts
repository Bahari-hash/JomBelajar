import { beforeEach, describe, expect, it } from "vitest";
import {
  applyTheme,
  parseThemePreference,
  readThemePreference,
  resolveTheme,
  THEME_STORAGE_KEY,
  writeThemePreference,
} from "@/lib/theme";

describe("theme utilities", () => {
  beforeEach(() => {
    document.head.innerHTML = '<meta name="theme-color" content="#ffffff">';
  });

  it("accepts supported preferences and falls back to system", () => {
    expect(parseThemePreference("light")).toBe("light");
    expect(parseThemePreference("dark")).toBe("dark");
    expect(parseThemePreference("system")).toBe("system");
    expect(parseThemePreference("unknown")).toBe("system");
  });

  it("handles unavailable storage without blocking rendering", () => {
    const unavailableStorage = {
      getItem: () => {
        throw new Error("unavailable");
      },
      setItem: () => {
        throw new Error("unavailable");
      },
    };

    expect(readThemePreference(unavailableStorage)).toBe("system");
    expect(() =>
      writeThemePreference("dark", unavailableStorage),
    ).not.toThrow();
  });

  it("persists and resolves explicit and system preferences", () => {
    writeThemePreference("dark");
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe("dark");
    expect(readThemePreference()).toBe("dark");
    expect(resolveTheme("system", true)).toBe("dark");
    expect(resolveTheme("system", false)).toBe("light");
    expect(resolveTheme("light", true)).toBe("light");
  });

  it("applies theme attributes and browser chrome color", () => {
    applyTheme("dark");
    expect(document.documentElement).toHaveAttribute(
      "data-theme",
      "tinylang-dark",
    );
    expect(document.documentElement.style.colorScheme).toBe("dark");
    expect(document.querySelector('meta[name="theme-color"]')).toHaveAttribute(
      "content",
      "#18181b",
    );
  });
});
