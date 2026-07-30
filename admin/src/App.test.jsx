import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import App from "@/App.jsx";

describe("App", () => {
  it("mounts the providers and sends a tab without a session to login", async () => {
    window.history.replaceState({}, "", "/");

    render(<App />);

    expect(await screen.findByRole("heading", { level: 1, name: "管理员登录" })).toBeVisible();
  });
});
