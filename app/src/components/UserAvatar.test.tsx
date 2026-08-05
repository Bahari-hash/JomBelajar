import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import UserAvatar from "@/components/UserAvatar";

describe("UserAvatar", () => {
  it("rejects unsafe URLs and falls back when an image fails", () => {
    const { rerender } = render(
      <UserAvatar name="学习者" url="javascript:alert(1)" />,
    );
    expect(screen.getByRole("img", { name: "默认头像" })).toBeInTheDocument();

    rerender(
      <UserAvatar name="学习者" url="https://example.test/avatar.png" />,
    );
    fireEvent.error(screen.getByRole("img", { name: "学习者的头像" }));
    expect(screen.getByRole("img", { name: "默认头像" })).toBeInTheDocument();
  });
});
