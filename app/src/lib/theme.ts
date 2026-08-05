export const THEME_STORAGE_KEY = "tinylang-theme";

export const THEME_NAMES = {
  light: "tinylang-light",
  dark: "tinylang-dark",
} as const;

export type ThemePreference = "light" | "dark" | "system";
export type ResolvedTheme = "light" | "dark";

export function parseThemePreference(value: unknown): ThemePreference {
  return value === "light" || value === "dark" || value === "system"
    ? value
    : "system";
}

export function readThemePreference(
  storage: Pick<Storage, "getItem"> = localStorage,
) {
  try {
    return parseThemePreference(storage.getItem(THEME_STORAGE_KEY));
  } catch {
    return "system";
  }
}

export function writeThemePreference(
  preference: ThemePreference,
  storage: Pick<Storage, "setItem"> = localStorage,
) {
  try {
    storage.setItem(THEME_STORAGE_KEY, preference);
  } catch {
    // Storage can be unavailable in privacy modes; the in-memory preference remains usable.
  }
}

export function resolveTheme(
  preference: ThemePreference,
  systemPrefersDark: boolean,
): ResolvedTheme {
  return preference === "system"
    ? systemPrefersDark
      ? "dark"
      : "light"
    : preference;
}

/** Applies the resolved theme to the document without coupling it to React. */
export function applyTheme(
  theme: ResolvedTheme,
  documentRoot: Document = document,
) {
  const themeName = THEME_NAMES[theme];
  documentRoot.documentElement.dataset.theme = themeName;
  documentRoot.documentElement.style.colorScheme = theme;
  documentRoot
    .querySelector<HTMLMetaElement>('meta[name="theme-color"]')
    ?.setAttribute("content", theme === "dark" ? "#18181b" : "#ffffff");
}
