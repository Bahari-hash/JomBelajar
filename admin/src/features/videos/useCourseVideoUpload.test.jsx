import { AxiosHeaders } from "axios";
import { act, renderHook } from "@testing-library/react";
import { Provider } from "react-redux";
import { describe, expect, it, vi } from "vitest";
import { useCourseVideoUpload } from "@/features/videos/useCourseVideoUpload.js";
import { objectStorageClient } from "@/services/objectStorageTransport.js";
import { tokenVault } from "@/services/tokenVault.js";
import { createAppStore } from "@/store/index.js";
import { axiosResponse, mockHttpClient } from "@/test/http.js";

const RESOURCE_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const SESSION_ID = "cccccccc-cccc-4ccc-8ccc-cccccccccccc";

describe("useCourseVideoUpload", () => {
  it("uploads multipart batches with at most three concurrent provider PUTs", async () => {
    tokenVault.install("access", "refresh");
    let active = 0;
    let maximumActive = 0;
    vi.spyOn(objectStorageClient, "request").mockImplementation(
      async (config) => {
        active += 1;
        maximumActive = Math.max(maximumActive, active);
        await new Promise((resolve) => window.setTimeout(resolve, 5));
        active -= 1;
        return {
          status: 200,
          headers: new AxiosHeaders({
            ETag: `"etag-${config.url.split("-").pop()}"`,
          }),
        };
      },
    );
    const requestMock = mockHttpClient((config) => {
      if (config.url.endsWith("/media/multipart"))
        return Promise.resolve(
          axiosResponse(
            {
              resourceId: RESOURCE_ID,
              sessionId: SESSION_ID,
              partSize: 5,
              partCount: 4,
              expiresAt: "2026-08-01T08:00:00+00:00",
            },
            201,
          ),
        );
      if (config.url.endsWith("/parts/presign"))
        return Promise.resolve(
          axiosResponse(
            config.data.partNumbers.map((partNumber) => ({
              partNumber,
              presignedUrl: `https://storage.example.test/part-${partNumber}`,
              contentLength: partNumber === 4 ? 2 : 5,
              expiresAt: "2026-08-01T08:00:00+00:00",
            })),
          ),
        );
      if (config.url.endsWith("/complete"))
        return Promise.resolve(
          axiosResponse(
            {
              resourceId: RESOURCE_ID,
              sessionId: SESSION_ID,
              status: "Completed",
              partSize: 5,
              partCount: 4,
              expiresAt: "2026-08-01T08:00:00+00:00",
              uploadedParts: [],
            },
            202,
          ),
        );
      return Promise.resolve(
        axiosResponse({
          id: RESOURCE_ID,
          uploaderId: "11111111-1111-4111-8111-111111111111",
          objectName: "courses/video.mp4",
          originalName: "video.mp4",
          module: "CourseVideo",
          status: "Active",
          size: 17,
          extension: ".mp4",
          contentType: "video/mp4",
          url: "https://media.example.test/video.mp4",
          createdAt: "2026-07-31T08:00:00+00:00",
        }),
      );
    });
    const store = createAppStore();
    const wrapper = ({ children }) => (
      <Provider store={store}>{children}</Provider>
    );
    const { result } = renderHook(() => useCourseVideoUpload(), { wrapper });
    const file = new File(["12345678901234567"], "video.mp4", {
      type: "video/mp4",
      lastModified: 123,
    });
    let resource;
    await act(async () => {
      resource = await result.current.upload(file, {
        multipartThresholdBytes: 10,
        partPresignBatchLimit: 4,
      });
    });
    expect(resource.id).toBe(RESOURCE_ID);
    expect(maximumActive).toBe(3);
    const complete = requestMock.mock.calls.find(([config]) =>
      config.url.endsWith("/complete"),
    )[0];
    expect(complete.data.parts).toEqual([
      { partNumber: 1, eTag: '"etag-1"' },
      { partNumber: 2, eTag: '"etag-2"' },
      { partNumber: 3, eTag: '"etag-3"' },
      { partNumber: 4, eTag: '"etag-4"' },
    ]);
    expect(sessionStorage.getItem("tinylang.courseVideoUpload.v1")).toBeNull();
  });
});
