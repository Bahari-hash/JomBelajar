import { AxiosHeaders } from "axios";
import { describe, expect, it, vi } from "vitest";
import {
  objectStorageClient,
  putObject,
} from "@/services/objectStorageTransport.js";
import { videoUploadApi } from "@/services/videoUploadApi.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const RESOURCE_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const SESSION_ID = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";

function statusResponse(status = "Initiated") {
  return {
    resourceId: RESOURCE_ID,
    sessionId: SESSION_ID,
    status,
    partSize: 5,
    partCount: 2,
    expiresAt: "2026-08-01T08:00:00+00:00",
    uploadedParts: [],
  };
}

describe("videoUploadApi", () => {
  it("uses capability, simple, multipart, complete, abort and confirm contracts", async () => {
    tokenVault.install("access", "refresh");
    const requestMock = mockHttpClient((config) => {
      if (config.url.includes("capabilities"))
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
      if (config.url.endsWith("/parts/presign"))
        return Promise.resolve(
          axiosResponse([
            {
              partNumber: 1,
              presignedUrl: "https://storage.example.test/part-1",
              contentLength: 5,
              expiresAt: "2026-08-01T08:00:00+00:00",
            },
          ]),
        );
      if (config.url.endsWith("/presign"))
        return Promise.resolve(
          axiosResponse({
            resourceId: RESOURCE_ID,
            presignedUrl: "https://storage.example.test/upload",
            objectName: "staging/video.mp4",
          }),
        );
      if (config.url.endsWith("/media/multipart"))
        return Promise.resolve(
          axiosResponse(
            {
              resourceId: RESOURCE_ID,
              sessionId: SESSION_ID,
              partSize: 5,
              partCount: 2,
              expiresAt: "2026-08-01T08:00:00+00:00",
            },
            201,
          ),
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
            size: 12,
            extension: ".mp4",
            contentType: "video/mp4",
            url: "https://media.example.test/video.mp4",
            createdAt: "2026-07-31T08:00:00+00:00",
          }),
        );
      if (config.method === "DELETE")
        return Promise.resolve(axiosResponse(undefined, 204));
      return Promise.resolve(
        axiosResponse(
          statusResponse(
            config.url.endsWith("/complete") ? "Finalizing" : "Initiated",
          ),
          config.url.endsWith("/complete") ? 202 : 200,
        ),
      );
    });
    const store = createAppStore();
    const file = new File(["hello world!"], "video.mp4", { type: "video/mp4" });
    await store
      .dispatch(
        videoUploadApi.endpoints.getCourseVideoUploadCapability.initiate(),
      )
      .unwrap();
    await store
      .dispatch(videoUploadApi.endpoints.presignCourseVideo.initiate(file))
      .unwrap();
    await store
      .dispatch(
        videoUploadApi.endpoints.createCourseVideoMultipart.initiate(file),
      )
      .unwrap();
    await store
      .dispatch(
        videoUploadApi.endpoints.presignCourseVideoParts.initiate({
          sessionId: SESSION_ID,
          partNumbers: [1],
        }),
      )
      .unwrap();
    await store
      .dispatch(
        videoUploadApi.endpoints.getCourseVideoMultipartStatus.initiate(
          SESSION_ID,
        ),
      )
      .unwrap();
    await store
      .dispatch(
        videoUploadApi.endpoints.completeCourseVideoMultipart.initiate({
          sessionId: SESSION_ID,
          parts: [{ partNumber: 1, eTag: '"etag"' }],
        }),
      )
      .unwrap();
    await store
      .dispatch(
        videoUploadApi.endpoints.abortCourseVideoMultipart.initiate({
          sessionId: SESSION_ID,
        }),
      )
      .unwrap();
    await store
      .dispatch(
        videoUploadApi.endpoints.confirmCourseVideoResource.initiate(
          RESOURCE_ID,
        ),
      )
      .unwrap();

    expect(requestMock.mock.calls.map(([config]) => config.url)).toEqual([
      "/uploads/admin/media/capabilities?module=CourseVideo",
      "/uploads/admin/media/presign",
      "/uploads/admin/media/multipart",
      `/uploads/admin/multipart/${SESSION_ID}/parts/presign`,
      `/uploads/admin/multipart/${SESSION_ID}`,
      `/uploads/admin/multipart/${SESSION_ID}/complete`,
      `/uploads/admin/multipart/${SESSION_ID}`,
      `/uploads/resources/${RESOURCE_ID}/confirm`,
    ]);
    expect(requestMock.mock.calls[1][0].data).toEqual({
      originalName: "video.mp4",
      extension: ".mp4",
      contentType: "video/mp4",
      size: 12,
      module: "CourseVideo",
    });
    expect(requestMock.mock.calls[5][0].data).toEqual({
      parts: [{ partNumber: 1, eTag: '"etag"' }],
    });
  });

  it("uploads through isolated Axios and returns the provider ETag", async () => {
    const request = vi.spyOn(objectStorageClient, "request").mockResolvedValue({
      status: 200,
      headers: new AxiosHeaders({ ETag: '"abc"' }),
    });
    const body = new Blob(["part"], { type: "video/mp4" });
    await expect(
      putObject({
        url: "https://storage.example.test/part",
        body,
        contentType: "video/mp4",
        signal: new AbortController().signal,
        onProgress: vi.fn(),
      }),
    ).resolves.toBe('"abc"');
    expect(request.mock.calls[0][0]).toMatchObject({
      method: "PUT",
      data: body,
      headers: { "Content-Type": "video/mp4" },
      withCredentials: false,
    });
    expect(request.mock.calls[0][0].headers).not.toHaveProperty(
      "Authorization",
    );
  });
});
