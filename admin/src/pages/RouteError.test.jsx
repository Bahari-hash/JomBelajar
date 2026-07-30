import { render, screen } from "@testing-library/react";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { describe, expect, it } from "vitest";
import RouteError from "@/pages/RouteError.jsx";

describe("RouteError", () => {
  it("provides safe recovery actions without exposing exception details", async () => {
    const router = createMemoryRouter(
      [
        {
          path: "/",
          loader: () => {
            throw new Error("sensitive route details");
          },
          element: <div />,
          errorElement: <RouteError />,
        },
      ],
      { initialEntries: ["/"] },
    );

    render(<RouterProvider router={router} />);

    expect(
      await screen.findByRole("heading", {
        level: 1,
        name: "页面暂时无法显示",
      }),
    ).toBeVisible();
    expect(
      screen.queryByText(/sensitive route details/i),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "重试" })).toBeVisible();
    expect(screen.getByRole("link", { name: "返回工作台" })).toBeVisible();
  });
});
