import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useEffect } from "react";
import { describe, expect, it, vi } from "vitest";
import ThemeControl from "@/components/ThemeControl";
import { useTheme } from "@/hooks/useTheme";
import ThemeProvider from "@/providers/ThemeProvider";

function ThemeStatus() {
  const { resolvedTheme } = useTheme();
  useEffect(() => undefined, [resolvedTheme]);
  return <output>{resolvedTheme}</output>;
}

describe("ThemeProvider", () => {
  it("changes and persists an explicit theme through the control", async () => {
    const user = userEvent.setup();
    render(
      <ThemeProvider>
        <ThemeControl />
        <ThemeStatus />
      </ThemeProvider>,
    );

    await user.selectOptions(
      screen.getByRole("combobox", { name: "主题" }),
      "dark",
    );

    expect(screen.getByText("dark")).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute(
      "data-theme",
      "tinylang-dark",
    );
    expect(localStorage.getItem("tinylang-theme")).toBe("dark");
  });

  it("tracks and cleans up system theme changes", async () => {
    let changeHandler: ((event: MediaQueryListEvent) => void) | undefined;
    const removeEventListener = vi.fn();
    vi.spyOn(window, "matchMedia").mockReturnValue({
      matches: false,
      media: "(prefers-color-scheme: dark)",
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn((_type, handler) => {
        changeHandler = handler as (event: MediaQueryListEvent) => void;
      }),
      removeEventListener,
      dispatchEvent: vi.fn(),
    });

    const view = render(
      <ThemeProvider>
        <ThemeStatus />
      </ThemeProvider>,
    );
    expect(screen.getByText("light")).toBeInTheDocument();

    changeHandler?.({ matches: true } as MediaQueryListEvent);
    expect(await screen.findByText("dark")).toBeInTheDocument();

    view.unmount();
    expect(removeEventListener).toHaveBeenCalledWith(
      "change",
      expect.any(Function),
    );
  });
});
