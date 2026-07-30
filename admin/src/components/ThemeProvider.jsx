import { useEffect, useState } from "react";
import { ThemeContext } from "@/lib/themeContext.js";
import {
  applyResolvedTheme,
  isThemeMode,
  persistTheme,
  readStoredTheme,
  resolveTheme,
  THEME_MODES,
} from "@/lib/theme.js";

/** Synchronizes the selected theme with storage, the document, and system preferences. */
export function ThemeProvider({ children }) {
  const [theme, setThemeState] = useState(readStoredTheme);

  useEffect(() => {
    const colorScheme = window.matchMedia("(prefers-color-scheme: dark)");
    const applyTheme = () => {
      applyResolvedTheme(resolveTheme(theme, colorScheme.matches));
    };

    applyTheme();

    if (theme !== THEME_MODES.SYSTEM) {
      return undefined;
    }

    colorScheme.addEventListener("change", applyTheme);
    return () => colorScheme.removeEventListener("change", applyTheme);
  }, [theme]);

  const setTheme = (nextTheme) => {
    if (!isThemeMode(nextTheme)) {
      return;
    }

    persistTheme(nextTheme);
    setThemeState(nextTheme);
  };

  return (
    <ThemeContext.Provider value={{ theme, setTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}
