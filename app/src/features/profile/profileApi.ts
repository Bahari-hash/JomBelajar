import { authApi } from "@/features/auth/authApi";
import { ApiRequestError } from "@/features/auth/authErrors";

const MAX_AVATAR_SIZE = 5 * 1024 * 1024;
const MAX_FILE_NAME_LENGTH = 255;
const AVATAR_TYPE_BY_EXTENSION: Record<string, string> = {
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".gif": "image/gif",
  ".webp": "image/webp",
};

/** Validates and uploads a consumer avatar through the backend presign contract. */
export async function uploadAvatar(
  file: File,
  onProgress?: (progress: number) => void,
) {
  if (!isSafeFileName(file.name)) {
    throwAvatarValidationError("头像文件名无效，请重新选择文件。");
  }
  if (file.name.length > MAX_FILE_NAME_LENGTH) {
    throwAvatarValidationError(
      `头像文件名不能超过 ${MAX_FILE_NAME_LENGTH} 个字符。`,
    );
  }

  const extension = getFileExtension(file.name);
  const expectedContentType = AVATAR_TYPE_BY_EXTENSION[extension];
  if (!expectedContentType) {
    throwAvatarValidationError("头像仅支持 PNG、JPG、JPEG、GIF 或 WebP 图片。");
  }
  if (file.type !== expectedContentType) {
    throwAvatarValidationError("头像文件扩展名与文件类型不匹配。");
  }
  if (file.size <= 0) {
    throwAvatarValidationError("头像文件不能为空。");
  }
  if (file.size > MAX_AVATAR_SIZE) {
    throwAvatarValidationError("头像文件大小不能超过 5 MB。");
  }

  const presignResponse = await authApi.presignAvatar({
    originalName: file.name,
    extension,
    contentType: file.type,
    size: file.size,
  });
  await authApi.uploadToPresignedUrl(
    presignResponse.data.presignedUrl,
    file,
    onProgress,
  );
  const confirmedResponse = await authApi.confirmUpload(
    presignResponse.data.resourceId,
  );
  const url = confirmedResponse.data.url;
  if (!url) {
    throw new ApiRequestError("头像上传已完成，但服务端没有返回头像地址。");
  }
  return url;
}

function getFileExtension(fileName: string) {
  const dotIndex = fileName.lastIndexOf(".");
  if (dotIndex <= 0 || dotIndex === fileName.length - 1) {
    return "";
  }
  return fileName.slice(dotIndex).toLowerCase();
}

function isSafeFileName(fileName: string) {
  return Boolean(fileName.trim()) &&
    fileName !== "." &&
    fileName !== ".." &&
    !fileName.includes("/") &&
    !fileName.includes("\\");
}

function throwAvatarValidationError(message: string): never {
  throw new ApiRequestError(message, {
    fieldErrors: { avatarUrl: message },
  });
}
