import dayjs from "dayjs";

export const AUDIO_RESOURCE_STATUSES = Object.freeze([
  "Uploading",
  "Queued",
  "Processing",
  "Ready",
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

function httpUrl(value, name, nullable = false) {
  if (nullable && value === null) return null;
  const result = string(value, name);
  try {
    if (!HTTP_PROTOCOLS.has(new URL(result).protocol)) invalid(name);
  } catch {
    invalid(name);
  }
  return result;
}

export function normalizeAudioResource(value) {
  const source = object(value, "audio resource");
  return {
    id: uuid(source.id, "audio resource id"),
    name: string(source.name, "audio resource name"),
    status: enumeration(
      source.status,
      AUDIO_RESOURCE_STATUSES,
      "audio resource status",
    ),
    durationSeconds: number(source.durationSeconds, "audio resource duration", {
      nullable: true,
    }),
    lastFailureCode: string(
      source.lastFailureCode,
      "audio resource failure code",
      true,
    ),
    updatedAt: date(source.updatedAt, "audio resource updatedAt"),
  };
}

export function normalizeAudioResourceDetails(value) {
  const source = object(value, "audio resource details");
  return {
    ...normalizeAudioResource(source),
    sampleRate: number(source.sampleRate, "audio resource sample rate", {
      nullable: true,
      integer: true,
    }),
    channels: number(source.channels, "audio resource channels", {
      nullable: true,
      integer: true,
    }),
    containerFormat: string(
      source.containerFormat,
      "audio resource container format",
      true,
    ),
    sourceCodec: string(
      source.sourceCodec,
      "audio resource source codec",
      true,
    ),
    currentOutputVersion:
      source.currentOutputVersion === null
        ? null
        : uuid(source.currentOutputVersion, "audio resource output version"),
    concurrencyStamp: uuid(
      source.concurrencyStamp,
      "audio resource concurrency stamp",
    ),
    createdAt: date(source.createdAt, "audio resource createdAt"),
  };
}

export function normalizeAudioResourcePage(value) {
  const source = object(value, "audio resource pagination");
  if (!Array.isArray(source.items)) invalid("audio resource pagination items");
  return {
    items: source.items.map(normalizeAudioResource),
    page: number(source.page, "audio resource page", { integer: true }),
    pageSize: number(source.pageSize, "audio resource page size", {
      integer: true,
    }),
    totalCount: number(source.totalCount, "audio resource total count", {
      integer: true,
    }),
    totalPages: number(source.totalPages, "audio resource total pages", {
      integer: true,
    }),
  };
}

export function normalizeAudioUploadCapability(value) {
  const source = object(value, "Audio upload capability");
  if (source.module !== "Audio") invalid("Audio upload capability module");
  if (!Array.isArray(source.allowedTypes)) invalid("Audio allowed types");
  return {
    module: source.module,
    maxSizeBytes: number(source.maxSizeBytes, "Audio maximum upload size", {
      integer: true,
      positive: true,
    }),
    allowedTypes: source.allowedTypes.map((value) => {
      const allowed = object(value, "Audio allowed media type");
      if (!Array.isArray(allowed.contentTypes))
        invalid("Audio allowed content types");
      return {
        extension: string(allowed.extension, "Audio allowed extension"),
        contentTypes: allowed.contentTypes.map((contentType) =>
          string(contentType, "Audio allowed content type"),
        ),
      };
    }),
    multipartThresholdBytes: number(
      source.multipartThresholdBytes,
      "Audio multipart threshold",
      { integer: true, positive: true },
    ),
    partSizeBytes: number(source.partSizeBytes, "Audio multipart part size", {
      integer: true,
      positive: true,
    }),
    maxPartCount: number(source.maxPartCount, "Audio maximum part count", {
      integer: true,
      positive: true,
    }),
    partPresignBatchLimit: number(
      source.partPresignBatchLimit,
      "Audio part presign batch limit",
      { integer: true, positive: true },
    ),
  };
}

export function normalizeAudioUploadInitialization(value) {
  const source = object(value, "audio upload initialization");
  const multipartSessionId =
    source.multipartSessionId === null
      ? null
      : uuid(source.multipartSessionId, "audio multipart session id");
  return {
    audioResourceId: uuid(source.audioResourceId, "audio resource id"),
    mediaResourceId: uuid(source.mediaResourceId, "audio media resource id"),
    presignedUrl: httpUrl(source.presignedUrl, "audio presigned URL", true),
    multipartSessionId,
    partSize: number(source.partSize, "audio multipart part size", {
      nullable: true,
      integer: true,
      positive: true,
    }),
    partCount: number(source.partCount, "audio multipart part count", {
      nullable: true,
      integer: true,
      positive: true,
    }),
    expiresAt: date(source.expiresAt, "audio upload expiry", true),
  };
}

export function normalizePartPresigns(value) {
  if (!Array.isArray(value)) invalid("audio multipart presigns");
  return value.map((item) => {
    const source = object(item, "audio multipart presign");
    return {
      partNumber: number(source.partNumber, "audio multipart part number", {
        integer: true,
        positive: true,
      }),
      presignedUrl: httpUrl(
        source.presignedUrl,
        "audio multipart presigned URL",
      ),
      contentLength: number(
        source.contentLength,
        "audio multipart content length",
        { integer: true, positive: true },
      ),
      expiresAt: date(source.expiresAt, "audio multipart presign expiry"),
    };
  });
}

export function normalizeMultipartStatus(value) {
  const source = object(value, "audio multipart status");
  if (!Array.isArray(source.uploadedParts))
    invalid("audio multipart uploaded parts");
  return {
    resourceId: uuid(source.resourceId, "audio media resource id"),
    sessionId: uuid(source.sessionId, "audio multipart session id"),
    status: enumeration(
      source.status,
      MULTIPART_UPLOAD_STATUSES,
      "audio multipart status",
    ),
    partSize: number(source.partSize, "audio multipart part size", {
      integer: true,
      positive: true,
    }),
    partCount: number(source.partCount, "audio multipart part count", {
      integer: true,
      positive: true,
    }),
    expiresAt: date(source.expiresAt, "audio multipart expiry"),
    uploadedParts: source.uploadedParts.map((item) => {
      const part = object(item, "audio multipart uploaded part");
      return {
        partNumber: number(part.partNumber, "audio multipart part number", {
          integer: true,
          positive: true,
        }),
        eTag: string(part.eTag, "audio multipart ETag"),
        size: number(part.size, "audio multipart uploaded size", {
          nullable: true,
          integer: true,
        }),
      };
    }),
  };
}

export function normalizeAudioPlayback(value) {
  const source = object(value, "audio playback");
  return {
    url: httpUrl(source.url, "audio playback URL"),
    expiresAt: date(source.expiresAt, "audio playback expiry", true),
    durationSeconds: number(source.durationSeconds, "audio playback duration"),
  };
}
