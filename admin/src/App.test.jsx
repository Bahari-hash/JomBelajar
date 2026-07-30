import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import App from "@/App.jsx";

describe("App", () => {
  it("mounts the application providers and browser router", async () => {
    window.history.replaceState({}, "", "/");

    render(<App />);

    expect(await screen.findByRole("heading", { level: 1, name: "工作台" })).toBeVisible();
  });
});
