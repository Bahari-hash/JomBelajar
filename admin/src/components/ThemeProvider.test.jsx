import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { ThemeMenu } from "@/components/ThemeMenu.jsx";
import { ThemeProvider } from "@/components/ThemeProvider.jsx";
import { TooltipProvider } from "@/components/ui/tooltip.jsx";
import { useTheme } from "@/hooks/useTheme.js";
import { THEME_STORAGE_KEY } from "@/lib/theme.js";
import { installMatchMedia } from "@/test/matchMedia.js";

function ThemeProbe() {
  const { theme } = useTheme();
  return <output aria-label="当前主题">{theme}</output>;
}

function renderTheme() {
  return render(
    <ThemeProvider>
      <TooltipProvider delayDuration={0}>
        <ThemeProbe />
        <ThemeMenu />
      </TooltipProvider>
    </ThemeProvider>,
  );
}

describe("ThemeProvider", () => {
  it.each([
    ["light", true, false],
    ["dark", false, true],
    ["system", true, true],
    ["invalid", false, false],
  ])(
    "applies stored %s preference against system dark=%s",
    async (storedTheme, systemDark, expectedDark) => {
      installMatchMedia(systemDark);
      window.localStorage.setItem(THEME_STORAGE_KEY, storedTheme);

      renderTheme();

      const expectedMode = storedTheme === "invalid" ? "system" : storedTheme;
      expect(screen.getByLabelText("当前主题")).toHaveTextContent(expectedMode);
      await waitFor(() =>
        expect(document.documentElement).toHaveClass(
          expectedDark ? "dark" : "",
          {
            exact: true,
          },
        ),
      );
    },
  );

  it("responds to system changes and removes its listener on unmount", async () => {
    const colorScheme = installMatchMedia(false);
    const { unmount } = renderTheme();

    expect(colorScheme.listenerCount()).toBe(1);
    act(() => colorScheme.setMatches(true));
    await waitFor(() => expect(document.documentElement).toHaveClass("dark"));

    unmount();
    expect(colorScheme.listenerCount()).toBe(0);
  });

  it("persists a user theme selection through the accessible menu", async () => {
    const user = userEvent.setup();
    renderTheme();

    await user.click(screen.getByRole("button", { name: "切换主题" }));
    await user.click(
      await screen.findByRole("menuitemradio", { name: "深色" }),
    );

    expect(window.localStorage.getItem(THEME_STORAGE_KEY)).toBe("dark");
    expect(screen.getByLabelText("当前主题")).toHaveTextContent("dark");
    await waitFor(() => expect(document.documentElement).toHaveClass("dark"));
  });
});
