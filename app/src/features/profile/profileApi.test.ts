import { describe, expect, it, vi } from "vitest";
import { authApi } from "@/features/auth/authApi";
import { uploadAvatar } from "@/features/profile/profileApi";

describe("profileApi", () => {
  it("runs presign, direct upload, and confirm in order", async () => {
    const presign = vi.spyOn(authApi, "presignAvatar").mockResolvedValue({
      data: {
        resourceId: "resource-1",
        presignedUrl: "https://storage.example.test/avatar-upload",
        objectName: "avatars/avatar.png",
      },
    } as never);
    const directUpload = vi
      .spyOn(authApi, "uploadToPresignedUrl")
      .mockResolvedValue({} as never);
    const file = new File(["avatar"], "avatar.png", { type: "image/png" });
    const confirm = vi.spyOn(authApi, "confirmUpload").mockResolvedValue({
      data: {
        id: "resource-1",
        uploaderId: "user-1",
        objectName: "avatars/avatar.png",
        originalName: "avatar.png",
        module: "Avatar",
        status: "Active",
        size: file.size,
        extension: ".png",
        contentType: "image/png",
        url: "https://storage.example.test/avatars/avatar.png",
        createdAt: "2026-08-01T00:00:00Z",
      },
    } as never);
    const progress = vi.fn();

    await expect(uploadAvatar(file, progress)).resolves.toMatchObject({
      id: "resource-1",
      url: "https://storage.example.test/avatars/avatar.png",
    });
    expect(presign).toHaveBeenCalledWith({
      originalName: "avatar.png",
      extension: ".png",
      contentType: "image/png",
      size: file.size,
    });
    expect(directUpload).toHaveBeenCalledWith(
      "https://storage.example.test/avatar-upload",
      file,
      progress,
    );
    expect(confirm).toHaveBeenCalledWith("resource-1");
  });

  it("rejects unsupported files before requesting a presign", async () => {
    const presign = vi.spyOn(authApi, "presignAvatar");
    const file = new File(["text"], "avatar.txt", { type: "text/plain" });

    await expect(uploadAvatar(file)).rejects.toThrow("头像仅支持");
    expect(presign).not.toHaveBeenCalled();
  });

  it.each([
    [
      "unsafe file name",
      () => new File(["avatar"], "../avatar.png", { type: "image/png" }),
    ],
    [
      "long file name",
      () =>
        new File(["avatar"], `${"a".repeat(252)}.png`, { type: "image/png" }),
    ],
    [
      "unsupported extension",
      () => new File(["avatar"], "avatar.bmp", { type: "image/png" }),
    ],
    [
      "mismatched content type",
      () => new File(["avatar"], "avatar.jpg", { type: "image/png" }),
    ],
    ["empty file", () => new File([], "avatar.png", { type: "image/png" })],
    [
      "oversized file",
      () =>
        new File([new Uint8Array(5 * 1024 * 1024 + 1)], "avatar.png", {
          type: "image/png",
        }),
    ],
  ])(
    "rejects %s before requesting a presign",
    async (_scenario, createFile) => {
      const presign = vi.spyOn(authApi, "presignAvatar");

      await expect(uploadAvatar(createFile())).rejects.toThrow();
      expect(presign).not.toHaveBeenCalled();
    },
  );
});
