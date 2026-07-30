import axios from "axios";
import { baseApi } from "@/services/baseApi.js";
import {
  normalizeConfirmedMedia,
  normalizePresign,
} from "@/services/articleContracts.js";
import { toApiError } from "@/services/problemDetails.js";

/** TinyLang media metadata endpoints for the simple ArticlePicture upload flow. */
export const articleMediaApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    presignArticlePicture: builder.mutation({
      query: (file) => ({
        url: "/uploads/editor/media/presign",
        method: "POST",
        body: {
          originalName: file.name,
          extension: `.${file.name.split(".").pop()?.toLowerCase() ?? ""}`,
          contentType: file.type,
          size: file.size,
          module: "ArticlePicture",
        },
      }),
      transformResponse: normalizePresign,
    }),
    confirmArticlePicture: builder.mutation({
      query: (resourceId) => ({
        url: `/uploads/resources/${resourceId}/confirm`,
        method: "PUT",
      }),
      transformResponse: normalizeConfirmedMedia,
    }),
  }),
});

/** Uploads a File to an absolute presigned URL without TinyLang credentials or defaults. */
export async function putPresignedObject({ url, file, signal, onProgress }) {
  try {
    await axios.put(url, file, {
      headers: { "Content-Type": file.type },
      signal,
      withCredentials: false,
      onUploadProgress: (event) => {
        if (event.total && onProgress)
          onProgress(
            Math.min(100, Math.round((event.loaded / event.total) * 100)),
          );
      },
    });
  } catch (error) {
    throw toApiError(error);
  }
}

export const {
  useConfirmArticlePictureMutation,
  usePresignArticlePictureMutation,
} = articleMediaApi;
