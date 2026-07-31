import axios from "axios";
import { describe, expect, it, vi } from "vitest";
import {
  articleMediaApi,
  putPresignedObject,
} from "@/services/articleMediaApi.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

describe("article media upload", () => {
  it("uses exact TinyLang presign and confirm contracts", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) =>
      Promise.resolve(
        axiosResponse(
          config.url.includes("presign")
            ? {
                resourceId: "88888888-8888-4888-8888-888888888888",
                presignedUrl: "https://storage.example.test/upload",
                objectName: "staging/a.png",
              }
            : {
                id: "88888888-8888-4888-8888-888888888888",
                uploaderId: "11111111-1111-1111-8111-111111111111",
                objectName: "article_pictures/a.png",
                originalName: "a.png",
                module: "ArticlePicture",
                status: "Active",
                size: 3,
                extension: ".png",
                contentType: "image/png",
                url: "https://media.example.test/a.png",
                createdAt: "2026-07-30T08:00:00Z",
              },
        ),
      ),
    );
    const store = createAppStore();
    const file = new File(["png"], "a.png", { type: "image/png" });
    await store
      .dispatch(articleMediaApi.endpoints.presignArticlePicture.initiate(file))
      .unwrap();
    await store
      .dispatch(
        articleMediaApi.endpoints.confirmArticlePicture.initiate(
          "88888888-8888-4888-8888-888888888888",
        ),
      )
      .unwrap();
    expect(
      requestMock.mock.calls.map(([config]) => ({
        url: config.url,
        method: config.method,
        data: config.data,
      })),
    ).toEqual([
      {
        url: "/uploads/admin/media/presign",
        method: "POST",
        data: {
          originalName: "a.png",
          extension: ".png",
          contentType: "image/png",
          size: 3,
          module: "ArticlePicture",
        },
      },
      {
        url: "/uploads/resources/88888888-8888-4888-8888-888888888888/confirm",
        method: "PUT",
        data: undefined,
      },
    ]);
  });

  it("uploads with isolated Axios config and no TinyLang authorization", async () => {
    const put = vi.spyOn(axios, "put").mockResolvedValue({ status: 200 });
    const file = new File(["png"], "a.png", { type: "image/png" });
    const controller = new AbortController();
    await putPresignedObject({
      url: "https://storage.example.test/upload",
      file,
      signal: controller.signal,
      onProgress: vi.fn(),
    });
    expect(put).toHaveBeenCalledWith(
      "https://storage.example.test/upload",
      file,
      expect.objectContaining({
        headers: { "Content-Type": "image/png" },
        signal: controller.signal,
        withCredentials: false,
      }),
    );
    expect(put.mock.calls[0][2].headers).not.toHaveProperty("Authorization");
  });
});
