import { useEffect, useState, type ReactNode } from "react";
import {
  applyTheme,
  readThemePreference,
  resolveTheme,
  writeThemePreference,
  type ThemePreference,
} from "@/lib/theme";
import { ThemeContext } from "@/providers/themeContext";

interface ThemeProviderProps {
  children: ReactNode;
}

/** Owns the persisted theme preference and tracks OS changes only in system mode. */
export default function ThemeProvider({ children }: ThemeProviderProps) {
  const [preference, setPreferenceState] = useState(readThemePreference);
  const [systemPrefersDark, setSystemPrefersDark] = useState(
    () => window.matchMedia("(prefers-color-scheme: dark)").matches,
  );
  const resolvedTheme = resolveTheme(preference, systemPrefersDark);

  useEffect(() => {
    applyTheme(resolvedTheme);
  }, [resolvedTheme]);

  useEffect(() => {
    if (preference !== "system") {
      return;
    }

    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
    const handleChange = (event: MediaQueryListEvent) => {
      setSystemPrefersDark(event.matches);
    };

    setSystemPrefersDark(mediaQuery.matches);
    mediaQuery.addEventListener("change", handleChange);
    return () => mediaQuery.removeEventListener("change", handleChange);
  }, [preference]);

  const setPreference = (nextPreference: ThemePreference) => {
    setPreferenceState(nextPreference);
    writeThemePreference(nextPreference);
  };

  return (
    <ThemeContext value={{ preference, resolvedTheme, setPreference }}>
      {children}
    </ThemeContext>
  );
}
