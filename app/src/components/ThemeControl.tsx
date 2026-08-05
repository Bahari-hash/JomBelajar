import { Monitor, Moon, Sun } from "lucide-react";
import { useTheme } from "@/hooks/useTheme";
import type { ThemePreference } from "@/lib/theme";

const themeOptions: Array<{
  value: ThemePreference;
  label: string;
}> = [
  { value: "light", label: "浅色" },
  { value: "dark", label: "深色" },
  { value: "system", label: "跟随系统" },
];

/** Accessible theme selector shared by desktop and mobile navigation. */
export default function ThemeControl() {
  const { preference, resolvedTheme, setPreference } = useTheme();
  const ThemeIcon =
    preference === "system" ? Monitor : resolvedTheme === "dark" ? Moon : Sun;

  return (
    <label className="flex items-center gap-2">
      <ThemeIcon aria-hidden="true" className="size-4 shrink-0" />
      <span className="sr-only">主题</span>
      <select
        aria-label="主题"
        className="select select-sm w-28"
        value={preference}
        onChange={(event) =>
          setPreference(event.target.value as ThemePreference)
        }
      >
        {themeOptions.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}
