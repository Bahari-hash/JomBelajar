import { AxiosError, AxiosHeaders } from "axios";
import { vi } from "vitest";
import { httpClient } from "@/services/httpTransport.js";

/** Builds Axios-compatible responses and errors for isolated API contract tests. */
export function axiosResponse(data, status = 200) {
  return {
    data,
    status,
    statusText: status === 204 ? "No Content" : "OK",
    headers: new AxiosHeaders({ "Content-Type": "application/json" }),
    config: {},
  };
}

export function axiosHttpError(data, status) {
  return new AxiosError(
    `Request failed with status code ${status}`,
    AxiosError.ERR_BAD_RESPONSE,
    {},
    null,
    axiosResponse(data, status),
  );
}

export function mockHttpClient(implementation) {
  return vi.spyOn(httpClient, "request").mockImplementation(implementation);
}

export function adminTokenResponse(overrides = {}) {
  return {
    token: "access-token",
    refreshToken: "refresh-token",
    expiresIn: 900,
    user: {
      id: "11111111-1111-1111-1111-111111111111",
      email: "admin@example.test",
      role: "Admin",
    },
    ...overrides,
  };
}

export function userListItem(overrides = {}) {
  return {
    id: "22222222-2222-2222-2222-222222222222",
    username: "alice",
    email: "alice@example.test",
    role: "User",
    nickname: "Alice",
    avatarUrl: null,
    isBanned: false,
    bannedAt: null,
    bannedReason: null,
    isDeleted: false,
    deletedAt: null,
    createdAt: "2026-07-29T08:00:00+00:00",
    updatedAt: "2026-07-29T08:00:00+00:00",
    lastLoginAt: null,
    activeSessionCount: 1,
    ...overrides,
  };
}

export function articleCategory(overrides = {}) {
  return {
    id: "44444444-4444-4444-8444-444444444444",
    name: "语法",
    slug: "grammar",
    description: null,
    isActive: true,
    articleCount: 1,
    createdAt: "2026-07-31T08:00:00+00:00",
    ...overrides,
  };
}

export function articleListItem(overrides = {}) {
  return {
    id: "55555555-5555-4555-8555-555555555555",
    title: "Grammar essentials",
    summary: "A concise guide",
    status: "Draft",
    categories: [
      {
        id: "44444444-4444-4444-8444-444444444444",
        name: "语法",
        slug: "grammar",
      },
    ],
    coverUrl: null,
    author: {
      id: "11111111-1111-1111-8111-111111111111",
      nickname: "Admin",
      avatarUrl: null,
    },
    publishedAt: null,
    updatedAt: "2026-07-30T08:00:00+00:00",
    ...overrides,
  };
}

export function adminArticle(overrides = {}) {
  const list = articleListItem();
  const { coverUrl: _coverUrl, ...common } = list;
  return {
    ...common,
    contentMarkdown: "# Grammar",
    contentHtml: "<h1>Grammar</h1>",
    lastEditor: common.author,
    coverMedia: null,
    bodyMedia: [],
    concurrencyStamp: "66666666-6666-4666-8666-666666666666",
    createdAt: "2026-07-30T07:00:00+00:00",
    ...overrides,
  };
}

export function videoCategory(overrides = {}) {
  return {
    id: "77777777-7777-4777-8777-777777777777",
    name: "听力",
    slug: "listening",
    description: null,
    isActive: true,
    videoCount: 1,
    createdAt: "2026-07-31T08:00:00+00:00",
    ...overrides,
  };
}

export function videoListItem(overrides = {}) {
  const auditUser = {
    id: "11111111-1111-4111-8111-111111111111",
    nickname: "Admin",
    avatarUrl: null,
  };
  return {
    id: "88888888-8888-4888-8888-888888888888",
    title: "French greetings",
    originalLanguage: "fr",
    processingStatus: "Ready",
    publicationStatus: "Draft",
    durationSeconds: 42.5,
    failureCode: null,
    categories: [videoCategory()],
    createdBy: auditUser,
    lastEditor: auditUser,
    concurrencyStamp: "99999999-9999-4999-8999-999999999999",
    createdAt: "2026-07-31T08:00:00+00:00",
    updatedAt: "2026-07-31T09:00:00+00:00",
    latestJob: {
      id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
      status: "Completed",
      attemptCount: 1,
      nextAttemptAt: null,
      startedAt: "2026-07-31T08:01:00+00:00",
      completedAt: "2026-07-31T08:03:00+00:00",
      failureCode: null,
    },
    ...overrides,
  };
}

export function adminVideo(overrides = {}) {
  return {
    ...videoListItem(),
    sourceMediaResourceId: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
    description: "Basic phrases",
    displayWidth: 1280,
    displayHeight: 720,
    containerFormat: "mov,mp4,m4a,3gp,3g2,mj2",
    videoCodec: "h264",
    audioCodec: "aac",
    publishedAt: null,
    archivedAt: null,
    renditions: [
      {
        targetHeight: 720,
        width: 1280,
        height: 720,
        videoBitrateKbps: 2800,
        audioBitrateKbps: 128,
        codecs: "avc1.64001f,mp4a.40.2",
      },
    ],
    ...overrides,
  };
}
