/** TinyLang 管理后台主题模式、持久化与根元素同步工具。 */

export const THEME_STORAGE_KEY = "tinylang.admin.theme";

export const THEME_MODES = Object.freeze({
  LIGHT: "light",
  DARK: "dark",
  SYSTEM: "system",
});

const VALID_THEME_MODES = new Set(Object.values(THEME_MODES));

export function isThemeMode(value) {
  return VALID_THEME_MODES.has(value);
}

export function readStoredTheme(storage = globalThis.localStorage) {
  try {
    const storedTheme = storage.getItem(THEME_STORAGE_KEY);
    return isThemeMode(storedTheme) ? storedTheme : THEME_MODES.SYSTEM;
  } catch {
    return THEME_MODES.SYSTEM;
  }
}

export function persistTheme(theme, storage = globalThis.localStorage) {
  if (!isThemeMode(theme)) {
    return;
  }

  try {
    storage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // Theme persistence is optional when storage is unavailable.
  }
}

export function resolveTheme(theme, prefersDark) {
  if (theme === THEME_MODES.DARK) {
    return THEME_MODES.DARK;
  }

  if (theme === THEME_MODES.LIGHT) {
    return THEME_MODES.LIGHT;
  }

  return prefersDark ? THEME_MODES.DARK : THEME_MODES.LIGHT;
}

export function applyResolvedTheme(
  theme,
  root = globalThis.document?.documentElement,
) {
  if (!root || !isThemeMode(theme) || theme === THEME_MODES.SYSTEM) {
    return;
  }

  root.classList.toggle("dark", theme === THEME_MODES.DARK);
  root.style.colorScheme = theme;
}
