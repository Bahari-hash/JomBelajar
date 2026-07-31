import axios from "axios";
import { toApiError } from "@/services/problemDetails.js";

/** Axios client isolated from TinyLang API authentication and default headers. */
export const objectStorageClient = axios.create({ withCredentials: false });

/** Uploads one File or Blob to a presigned object-storage URL and returns its ETag. */
export async function putObject({
  url,
  body,
  contentType,
  signal,
  onProgress,
}) {
  try {
    const response = await objectStorageClient.request({
      url,
      method: "PUT",
      data: body,
      headers: { "Content-Type": contentType },
      signal,
      withCredentials: false,
      onUploadProgress: (event) => {
        if (event.total && onProgress) {
          onProgress(
            Math.min(100, Math.round((event.loaded / event.total) * 100)),
          );
        }
      },
    });
    const eTag = response.headers?.get?.("etag") ?? response.headers?.etag;
    return typeof eTag === "string" && eTag.trim() ? eTag.trim() : null;
  } catch (error) {
    throw toApiError(error);
  }
}
