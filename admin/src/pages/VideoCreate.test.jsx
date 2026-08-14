import { AxiosHeaders } from "axios";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { objectStorageClient } from "@/services/objectStorageTransport.js";
import {
  adminVideo,
  axiosResponse,
  mockHttpClient,
  videoCategory,
} from "@/test/http.js";
import { renderAppAt } from "@/test/renderApp.jsx";

const RESOURCE_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const COVER_RESOURCE_ID = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";

describe("VideoCreate", () => {
  it("runs capability-driven simple upload before creating video metadata", async () => {
    const user = userEvent.setup();
    vi.spyOn(objectStorageClient, "request").mockResolvedValue({
      status: 200,
      headers: new AxiosHeaders(),
    });
    const saved = adminVideo({ title: "Bonjour" });
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("capabilities?module=VideoCover"))
        return Promise.resolve(
          axiosResponse({
            module: "VideoCover",
            maxSizeBytes: 5_242_880,
            allowedTypes: [{ extension: ".jpg", contentTypes: ["image/jpeg"] }],
            multipartThresholdBytes: 268_435_456,
            partSizeBytes: 16_777_216,
            maxPartCount: 10_000,
            partPresignBatchLimit: 20,
          }),
        );
      if (config.url.includes("capabilities?module=CourseVideo"))
        return Promise.resolve(
          axiosResponse({
            module: "CourseVideo",
            maxSizeBytes: 100,
            allowedTypes: [{ extension: ".mp4", contentTypes: ["video/mp4"] }],
            multipartThresholdBytes: 10,
            partSizeBytes: 5,
            maxPartCount: 20,
            partPresignBatchLimit: 10,
          }),
        );
      if (config.url.startsWith("/admin/video-categories"))
        return Promise.resolve(
          axiosResponse({
            items: [videoCategory()],
            page: 1,
            pageSize: 100,
            totalCount: 1,
            totalPages: 1,
          }),
        );
      if (config.url.endsWith("/presign"))
        return Promise.resolve(
          axiosResponse({
            resourceId:
              config.data.module === "VideoCover"
                ? COVER_RESOURCE_ID
                : RESOURCE_ID,
            presignedUrl:
              config.data.module === "VideoCover"
                ? "https://storage.example.test/cover"
                : "https://storage.example.test/video",
            objectName:
              config.data.module === "VideoCover"
                ? "staging/cover.jpg"
                : "staging/video.mp4",
          }),
        );
      if (config.url.endsWith(`${COVER_RESOURCE_ID}/confirm`))
        return Promise.resolve(
          axiosResponse({
            id: COVER_RESOURCE_ID,
            uploaderId: "11111111-1111-4111-8111-111111111111",
            objectName: "video_covers/cover.jpg",
            originalName: "cover.jpg",
            module: "VideoCover",
            status: "Active",
            size: 3,
            extension: ".jpg",
            contentType: "image/jpeg",
            url: "https://media.example.test/cover.jpg",
            createdAt: "2026-07-31T08:00:00+00:00",
          }),
        );
      if (config.url.endsWith("/confirm"))
        return Promise.resolve(
          axiosResponse({
            id: RESOURCE_ID,
            uploaderId: "11111111-1111-4111-8111-111111111111",
            objectName: "courses/video.mp4",
            originalName: "video.mp4",
            module: "CourseVideo",
            status: "Active",
            size: 3,
            extension: ".mp4",
            contentType: "video/mp4",
            url: "https://media.example.test/video.mp4",
            createdAt: "2026-07-31T08:00:00+00:00",
          }),
        );
      return Promise.resolve(axiosResponse(saved, 201));
    });
    const { router } = renderAppAt("/videos/new");
    await user.type(
      await screen.findByLabelText(/^标题/, {}, { timeout: 3000 }),
      "Bonjour",
    );
    await user.upload(
      screen.getByLabelText("自定义封面"),
      new File(["jpg"], "cover.jpg", { type: "image/jpeg" }),
    );
    expect(await screen.findByAltText("视频封面预览")).toBeVisible();
    await user.upload(
      screen.getByLabelText(/^视频源文件/),
      new File(["mp4"], "video.mp4", { type: "video/mp4" }),
    );
    expect(screen.getByRole("button", { name: "重新选择视频" })).toBeVisible();
    expect(screen.getByText(/video\.mp4/)).toBeVisible();
    await user.click(screen.getByRole("button", { name: "上传并创建视频" }));
    await expect
      .poll(() => router.state.location.pathname)
      .toBe(`/videos/${saved.id}`);
    expect(
      requestMock.mock.calls
        .map(([config]) => config.url)
        .filter((url) =>
          [
            "/uploads/admin/media/presign",
            `/uploads/resources/${COVER_RESOURCE_ID}/confirm`,
            `/uploads/resources/${RESOURCE_ID}/confirm`,
            "/admin/videos",
          ].includes(url),
        ),
    ).toEqual([
      "/uploads/admin/media/presign",
      `/uploads/resources/${COVER_RESOURCE_ID}/confirm`,
      "/uploads/admin/media/presign",
      `/uploads/resources/${RESOURCE_ID}/confirm`,
      "/admin/videos",
    ]);
    const create = requestMock.mock.calls.find(
      ([config]) => config.url === "/admin/videos",
    )[0];
    expect(create.data).toMatchObject({
      sourceMediaResourceId: RESOURCE_ID,
      coverMediaResourceId: COVER_RESOURCE_ID,
      title: "Bonjour",
    });
  });
});
