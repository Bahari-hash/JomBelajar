import { authApi } from "@/features/auth/authApi";

const MAX_AVATAR_SIZE = 5 * 1024 * 1024;
const AVATAR_TYPES = new Set([
  "image/png",
  "image/jpeg",
  "image/gif",
  "image/webp",
]);

/** Validates and uploads a consumer avatar through the backend presign contract. */
export async function uploadAvatar(
  file: File,
  onProgress?: (progress: number) => void,
) {
  if (!AVATAR_TYPES.has(file.type)) {
    throw new Error("头像仅支持 PNG、JPG、GIF 或 WebP 图片。");
  }
  if (file.size <= 0 || file.size > MAX_AVATAR_SIZE) {
    throw new Error("头像文件大小不能超过 5 MB。");
  }

  const extension = getFileExtension(file.name);
  if (!extension) {
    throw new Error("头像文件缺少有效扩展名。");
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
    throw new Error("头像上传已完成，但服务端没有返回头像地址。");
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
