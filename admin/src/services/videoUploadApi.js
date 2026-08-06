import { baseApi } from "@/services/baseApi.js";
import {
  normalizeCourseVideoResource,
  normalizeMultipartCreate,
  normalizeMultipartStatus,
  normalizePartPresigns,
  normalizePresign,
  normalizeUploadCapability,
  normalizeVideoCoverResource,
} from "@/services/videoContracts.js";

function fileMetadata(file) {
  const dot = file.name.lastIndexOf(".");
  return {
    originalName: file.name,
    extension: dot >= 0 ? file.name.slice(dot).toLowerCase() : "",
    contentType: file.type,
    size: file.size,
    module: "CourseVideo",
  };
}

function coverFileMetadata(file) {
  return { ...fileMetadata(file), module: "VideoCover" };
}

/** CourseVideo capability, simple upload and multipart session API contract. */
export const videoUploadApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getCourseVideoUploadCapability: builder.query({
      query: () => ({
        url: "/uploads/admin/media/capabilities?module=CourseVideo",
      }),
      transformResponse: normalizeUploadCapability,
    }),
    getVideoCoverUploadCapability: builder.query({
      query: () => ({
        url: "/uploads/admin/media/capabilities?module=VideoCover",
      }),
      transformResponse: (value) => normalizeUploadCapability(value, "VideoCover"),
    }),
    presignVideoCover: builder.mutation({
      query: (file) => ({
        url: "/uploads/admin/media/presign",
        method: "POST",
        body: coverFileMetadata(file),
      }),
      transformResponse: normalizePresign,
    }),
    createVideoCoverMultipart: builder.mutation({
      query: (file) => ({
        url: "/uploads/admin/media/multipart",
        method: "POST",
        body: coverFileMetadata(file),
      }),
      transformResponse: normalizeMultipartCreate,
    }),
    confirmVideoCoverResource: builder.mutation({
      query: (resourceId) => ({
        url: `/uploads/resources/${resourceId}/confirm`,
        method: "PUT",
      }),
      transformResponse: normalizeVideoCoverResource,
    }),
    presignCourseVideo: builder.mutation({
      query: (file) => ({
        url: "/uploads/admin/media/presign",
        method: "POST",
        body: fileMetadata(file),
      }),
      transformResponse: normalizePresign,
    }),
    createCourseVideoMultipart: builder.mutation({
      query: (file) => ({
        url: "/uploads/admin/media/multipart",
        method: "POST",
        body: fileMetadata(file),
      }),
      transformResponse: normalizeMultipartCreate,
    }),
    presignCourseVideoParts: builder.mutation({
      query: ({ sessionId, partNumbers }) => ({
        url: `/uploads/admin/multipart/${sessionId}/parts/presign`,
        method: "POST",
        body: { partNumbers },
      }),
      transformResponse: normalizePartPresigns,
    }),
    getCourseVideoMultipartStatus: builder.query({
      query: (sessionId) => ({
        url: `/uploads/admin/multipart/${sessionId}`,
      }),
      transformResponse: normalizeMultipartStatus,
    }),
    completeCourseVideoMultipart: builder.mutation({
      query: ({ sessionId, parts }) => ({
        url: `/uploads/admin/multipart/${sessionId}/complete`,
        method: "POST",
        body: { parts },
      }),
      transformResponse: normalizeMultipartStatus,
    }),
    abortCourseVideoMultipart: builder.mutation({
      query: ({ sessionId }) => ({
        url: `/uploads/admin/multipart/${sessionId}`,
        method: "DELETE",
      }),
    }),
    confirmCourseVideoResource: builder.mutation({
      query: (resourceId) => ({
        url: `/uploads/resources/${resourceId}/confirm`,
        method: "PUT",
      }),
      transformResponse: normalizeCourseVideoResource,
    }),
  }),
});

export const {
  useAbortCourseVideoMultipartMutation,
  useCompleteCourseVideoMultipartMutation,
  useConfirmCourseVideoResourceMutation,
  useCreateCourseVideoMultipartMutation,
  useGetCourseVideoMultipartStatusQuery,
  useLazyGetCourseVideoMultipartStatusQuery,
  useGetCourseVideoUploadCapabilityQuery,
  useGetVideoCoverUploadCapabilityQuery,
  usePresignVideoCoverMutation,
  useConfirmVideoCoverResourceMutation,
  useCreateVideoCoverMultipartMutation,
  usePresignCourseVideoMutation,
  usePresignCourseVideoPartsMutation,
} = videoUploadApi;
