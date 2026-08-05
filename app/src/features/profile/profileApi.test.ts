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
    const confirm = vi.spyOn(authApi, "confirmUpload").mockResolvedValue({
      data: { url: "https://storage.example.test/avatars/avatar.png" },
    } as never);
    const file = new File(["avatar"], "avatar.png", { type: "image/png" });
    const progress = vi.fn();

    await expect(uploadAvatar(file, progress)).resolves.toBe(
      "https://storage.example.test/avatars/avatar.png",
    );
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
});
