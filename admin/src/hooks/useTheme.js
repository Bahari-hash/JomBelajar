import { useContext } from "react";
import { ThemeContext } from "@/lib/themeContext.js";

/** Returns the active TinyLang admin theme preference and update command. */
export function useTheme() {
  const context = useContext(ThemeContext);

  if (!context) {
    throw new Error("useTheme 必须在 ThemeProvider 内使用。");
  }

  return context;
}
