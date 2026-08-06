import dayjs from "dayjs";

export const VIDEO_PROCESSING_STATUSES = Object.freeze([
  "Queued",
  "Processing",
  "Ready",
  "Failed",
]);
export const VIDEO_PUBLICATION_STATUSES = Object.freeze([
  "Draft",
  "Published",
  "Unpublished",
  "Archived",
]);
export const VIDEO_JOB_STATUSES = Object.freeze([
  "Queued",
  "Processing",
  "Completed",
  "Failed",
]);
export const MULTIPART_UPLOAD_STATUSES = Object.freeze([
  "Initiated",
  "Completing",
  "Finalizing",
  "Completed",
  "Aborting",
  "Aborted",
  "Expired",
  "Failed",
]);

const UUID_PATTERN = /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i;
const HTTP_PROTOCOLS = new Set(["http:", "https:"]);

function invalid(name) {
  throw new Error(`API returned invalid ${name}.`);
}

function object(value, name) {
  if (!value || typeof value !== "object" || Array.isArray(value))
    invalid(name);
  return value;
}

function string(value, name, nullable = false) {
  if (nullable && value === null) return null;
  if (typeof value !== "string" || value.length === 0) invalid(name);
  return value;
}

function uuid(value, name) {
  const result = string(value, name);
  if (!UUID_PATTERN.test(result)) invalid(name);
  return result;
}

function boolean(value, name) {
  if (typeof value !== "boolean") invalid(name);
  return value;
}

function number(
  value,
  name,
  { nullable = false, integer = false, positive = false } = {},
) {
  if (nullable && value === null) return null;
  if (
    !Number.isFinite(value) ||
    value < 0 ||
    (integer && !Number.isInteger(value)) ||
    (positive && value <= 0)
  )
    invalid(name);
  return value;
}

function date(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = string(value, name);
  if (!dayjs(result).isValid()) invalid(name);
  return result;
}

function enumeration(value, allowed, name) {
  if (!allowed.includes(value)) invalid(name);
  return value;
}

export function normalizeHttpUrl(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = string(value, name);
  try {
    if (!HTTP_PROTOCOLS.has(new URL(result).protocol)) invalid(name);
  } catch {
    invalid(name);
  }
  return result;
}

function auditUser(value) {
  const source = object(value, "video audit user");
  return {
    id: uuid(source.id, "video audit user id"),
    nickname: string(source.nickname, "video audit user nickname", true),
    avatarUrl: normalizeHttpUrl(
      source.avatarUrl,
      "video audit avatar URL",
      true,
    ),
  };
}

function categorySummary(value) {
  const source = object(value, "video category summary");
  return {
    id: uuid(source.id, "video category id"),
    name: string(source.name, "video category name"),
    slug: string(source.slug, "video category slug"),
    isActive: boolean(source.isActive, "video category state"),
  };
}

function coverSummary(value) {
  if (value === null) return null;
  const source = object(value, "video cover");
  return {
    id: uuid(source.id, "video cover id"),
    originalName: string(source.originalName, "video cover name"),
    url: normalizeHttpUrl(source.url, "video cover URL"),
  };
}

function latestJob(value) {
  if (value === null) return null;
  const source = object(value, "video job summary");
  return {
    id: uuid(source.id, "video job id"),
    status: enumeration(source.status, VIDEO_JOB_STATUSES, "video job status"),
    attemptCount: number(source.attemptCount, "video job attempt count", {
      integer: true,
    }),
    nextAttemptAt: date(source.nextAttemptAt, "video job next attempt", true),
    startedAt: date(source.startedAt, "video job startedAt", true),
    completedAt: date(source.completedAt, "video job completedAt", true),
    failureCode: string(source.failureCode, "video job failure code", true),
  };
}

function commonVideo(value) {
  const source = object(value, "video");
  if (!Array.isArray(source.categories)) invalid("video categories");
  return {
    id: uuid(source.id, "video id"),
    title: string(source.title, "video title"),
    originalLanguage: string(source.originalLanguage, "video language"),
    processingStatus: enumeration(
      source.processingStatus,
      VIDEO_PROCESSING_STATUSES,
      "video processing status",
    ),
    publicationStatus: enumeration(
      source.publicationStatus,
      VIDEO_PUBLICATION_STATUSES,
      "video publication status",
    ),
    durationSeconds: number(source.durationSeconds, "video duration", {
      nullable: true,
    }),
    failureCode: string(source.failureCode, "video failure code", true),
    categories: source.categories.map(categorySummary),
    createdBy: auditUser(source.createdBy),
    lastEditor: auditUser(source.lastEditor),
    concurrencyStamp: uuid(source.concurrencyStamp, "video concurrency stamp"),
    createdAt: date(source.createdAt, "video createdAt"),
    updatedAt: date(source.updatedAt, "video updatedAt"),
    latestJob: latestJob(source.latestJob),
  };
}

export function normalizeVideoListItem(value) {
  return commonVideo(value);
}

function rendition(value) {
  const source = object(value, "video rendition");
  return {
    targetHeight: number(source.targetHeight, "rendition target height", {
      integer: true,
    }),
    width: number(source.width, "rendition width", { integer: true }),
    height: number(source.height, "rendition height", { integer: true }),
    videoBitrateKbps: number(source.videoBitrateKbps, "video bitrate", {
      integer: true,
    }),
    audioBitrateKbps: number(source.audioBitrateKbps, "audio bitrate", {
      integer: true,
    }),
    codecs: string(source.codecs, "rendition codecs"),
  };
}

export function normalizeAdminVideo(value) {
  const source = object(value, "video details");
  if (Object.hasOwn(source, "subtitles")) invalid("video details contract");
  if (!Array.isArray(source.renditions)) invalid("video renditions");
  return {
    ...commonVideo(source),
    sourceMediaResourceId: uuid(
      source.sourceMediaResourceId,
      "video source media resource id",
    ),
    cover: coverSummary(source.cover),
    description: string(source.description, "video description", true),
    displayWidth: number(source.displayWidth, "video display width", {
      nullable: true,
      integer: true,
    }),
    displayHeight: number(source.displayHeight, "video display height", {
      nullable: true,
      integer: true,
    }),
    containerFormat: string(source.containerFormat, "container format", true),
    videoCodec: string(source.videoCodec, "video codec", true),
    audioCodec: string(source.audioCodec, "audio codec", true),
    publishedAt: date(source.publishedAt, "video publishedAt", true),
    archivedAt: date(source.archivedAt, "video archivedAt", true),
    renditions: source.renditions.map(rendition),
  };
}

export function normalizeVideoPage(value) {
  const source = object(value, "video pagination");
  if (!Array.isArray(source.items)) invalid("video pagination items");
  return {
    items: source.items.map(normalizeVideoListItem),
    page: number(source.page, "video page", { integer: true }),
    pageSize: number(source.pageSize, "video page size", { integer: true }),
    totalCount: number(source.totalCount, "video total count", {
      integer: true,
    }),
    totalPages: number(source.totalPages, "video total pages", {
      integer: true,
    }),
  };
}

export function normalizeVideoCategory(value) {
  const source = object(value, "video category");
  return {
    id: uuid(source.id, "video category id"),
    name: string(source.name, "video category name"),
    slug: string(source.slug, "video category slug"),
    description: string(source.description, "video category description", true),
    isActive: boolean(source.isActive, "video category state"),
    videoCount: number(source.videoCount, "video category count", {
      integer: true,
    }),
    createdAt: date(source.createdAt, "video category createdAt"),
  };
}

export function normalizeVideoCategoryPage(value) {
  const source = object(value, "video category pagination");
  if (!Array.isArray(source.items)) invalid("video category pagination items");
  return {
    items: source.items.map(normalizeVideoCategory),
    page: number(source.page, "video category page", { integer: true }),
    pageSize: number(source.pageSize, "video category page size", {
      integer: true,
    }),
    totalCount: number(source.totalCount, "video category total count", {
      integer: true,
    }),
    totalPages: number(source.totalPages, "video category total pages", {
      integer: true,
    }),
  };
}

export function normalizePlayback(value) {
  const source = object(value, "video playback");
  if (Object.hasOwn(source, "subtitles")) invalid("video playback contract");
  return {
    masterPlaylistUrl: normalizeHttpUrl(
      source.masterPlaylistUrl,
      "master playlist URL",
    ),
    posterUrl: normalizeHttpUrl(source.posterUrl, "poster URL", true),
    expiresAt: date(source.expiresAt, "playback expiresAt", true),
    durationSeconds: number(source.durationSeconds, "playback duration"),
    positionSeconds: number(source.positionSeconds, "playback position"),
    isCompleted: boolean(source.isCompleted, "playback completion"),
  };
}

export function normalizeUploadCapability(value, expectedModule = "CourseVideo") {
  const source = object(value, "upload capability");
  if (source.module !== expectedModule || !Array.isArray(source.allowedTypes))
    invalid(`${expectedModule} upload capability`);
  return {
    module: source.module,
    maxSizeBytes: number(source.maxSizeBytes, "maximum upload size", {
      integer: true,
      positive: true,
    }),
    allowedTypes: source.allowedTypes.map((item) => {
      const allowed = object(item, "allowed media type");
      if (!Array.isArray(allowed.contentTypes))
        invalid("allowed content types");
      return {
        extension: string(allowed.extension, "allowed extension"),
        contentTypes: allowed.contentTypes.map((type) =>
          string(type, "allowed content type"),
        ),
      };
    }),
    multipartThresholdBytes: number(
      source.multipartThresholdBytes,
      "multipart threshold",
      { integer: true, positive: true },
    ),
    partSizeBytes: number(source.partSizeBytes, "multipart part size", {
      integer: true,
      positive: true,
    }),
    maxPartCount: number(source.maxPartCount, "maximum part count", {
      integer: true,
      positive: true,
    }),
    partPresignBatchLimit: number(
      source.partPresignBatchLimit,
      "part presign batch limit",
      { integer: true, positive: true },
    ),
  };
}

export function normalizePresign(value) {
  const source = object(value, "video upload presign");
  return {
    resourceId: uuid(source.resourceId, "media resource id"),
    presignedUrl: normalizeHttpUrl(source.presignedUrl, "presigned URL"),
    objectName: string(source.objectName, "media object name"),
  };
}

export function normalizeMultipartCreate(value) {
  const source = object(value, "multipart create");
  return {
    resourceId: uuid(source.resourceId, "media resource id"),
    sessionId: uuid(source.sessionId, "multipart session id"),
    partSize: number(source.partSize, "multipart part size", {
      integer: true,
      positive: true,
    }),
    partCount: number(source.partCount, "multipart part count", {
      integer: true,
      positive: true,
    }),
    expiresAt: date(source.expiresAt, "multipart expiry"),
  };
}

export function normalizePartPresigns(value) {
  if (!Array.isArray(value)) invalid("multipart part presigns");
  return value.map((item) => {
    const source = object(item, "multipart part presign");
    return {
      partNumber: number(source.partNumber, "part number", {
        integer: true,
        positive: true,
      }),
      presignedUrl: normalizeHttpUrl(source.presignedUrl, "part presigned URL"),
      contentLength: number(source.contentLength, "part content length", {
        integer: true,
        positive: true,
      }),
      expiresAt: date(source.expiresAt, "part presign expiry"),
    };
  });
}

export function normalizeMultipartStatus(value) {
  const source = object(value, "multipart status");
  if (!Array.isArray(source.uploadedParts)) invalid("uploaded multipart parts");
  const result = {
    resourceId: uuid(source.resourceId, "media resource id"),
    sessionId: uuid(source.sessionId, "multipart session id"),
    status: enumeration(
      source.status,
      MULTIPART_UPLOAD_STATUSES,
      "multipart status",
    ),
    partSize: number(source.partSize, "multipart part size", {
      integer: true,
      positive: true,
    }),
    partCount: number(source.partCount, "multipart part count", {
      integer: true,
      positive: true,
    }),
    expiresAt: date(source.expiresAt, "multipart expiry"),
    uploadedParts: source.uploadedParts.map((item) => {
      const part = object(item, "uploaded multipart part");
      return {
        partNumber: number(part.partNumber, "uploaded part number", {
          integer: true,
          positive: true,
        }),
        eTag: string(part.eTag, "uploaded part ETag"),
        size: number(part.size, "uploaded part size", {
          nullable: true,
          integer: true,
        }),
      };
    }),
  };
  const partNumbers = result.uploadedParts.map(({ partNumber }) => partNumber);
  if (
    new Set(partNumbers).size !== partNumbers.length ||
    partNumbers.some((partNumber) => partNumber > result.partCount)
  )
    invalid("uploaded multipart parts");
  return result;
}

export function normalizeCourseVideoResource(value) {
  const source = object(value, "CourseVideo media resource");
  if (source.module !== "CourseVideo" || source.status !== "Active")
    invalid("active CourseVideo media resource");
  return {
    id: uuid(source.id, "media resource id"),
    originalName: string(source.originalName, "media original name"),
    contentType: string(source.contentType, "media content type"),
    size: number(source.size, "media size", { integer: true }),
    url: normalizeHttpUrl(source.url, "media URL", true),
  };
}

export function normalizeVideoCoverResource(value) {
  const source = object(value, "VideoCover media resource");
  if (source.module !== "VideoCover" || source.status !== "Active")
    invalid("active VideoCover media resource");
  return {
    id: uuid(source.id, "video cover resource id"),
    originalName: string(source.originalName, "video cover original name"),
    contentType: string(source.contentType, "video cover content type"),
    size: number(source.size, "video cover size", { integer: true }),
    url: normalizeHttpUrl(source.url, "video cover URL"),
  };
}

export function normalizeClearVideoCategory(value) {
  const source = object(value, "video category clear result");
  return {
    categoryId: uuid(source.categoryId, "video category id"),
    removedVideoCount: number(source.removedVideoCount, "removed video count", {
      integer: true,
    }),
  };
}
