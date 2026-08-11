import { baseApi } from "@/services/baseApi.js";
import {
  normalizeAdminPaper,
  normalizePaperPage,
  normalizePaperTagPage,
  normalizePaperValidation,
} from "@/services/paperContracts.js";

const CONTRACT_ERROR = Object.freeze({
  status: "CUSTOM_ERROR",
  detail: "服务端返回的试卷数据格式无法识别，请刷新后重试。",
  errorCode: null,
  fieldErrors: {},
  kind: "contract",
});

async function executeNormalized(args, baseQuery, normalize) {
  const result = await baseQuery(args);
  if (result.error) return result;
  try {
    return { data: normalize(result.data) };
  } catch {
    return { error: CONTRACT_ERROR };
  }
}

function normalizedQuery(buildArgs, normalize) {
  return (argument, _api, _extraOptions, baseQuery) =>
    executeNormalized(buildArgs(argument), baseQuery, normalize);
}

function buildListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  });
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (filters.language) params.set("language", filters.language);
  if (filters.status) params.set("status", filters.status);
  if (filters.tag) params.set("tag", filters.tag);
  return `/admin/papers?${params.toString()}`;
}

function invalidatePaper(_result, error, { paperId }) {
  return error
    ? []
    : [
        { type: "Paper", id: paperId },
        { type: "Paper", id: "LIST" },
        { type: "Paper", id: "TAG_LIST" },
      ];
}

/** RTK Query endpoints for administrator Paper aggregate management. */
export const papersApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAdminPapers: builder.query({
      queryFn: normalizedQuery(
        (filters) => ({ url: buildListUrl(filters) }),
        normalizePaperPage,
      ),
      providesTags: (result) =>
        result
          ? [
              { type: "Paper", id: "LIST" },
              ...result.items.map((paper) => ({
                type: "Paper",
                id: paper.id,
              })),
            ]
          : [{ type: "Paper", id: "LIST" }],
    }),
    getAdminPaperTags: builder.query({
      queryFn: normalizedQuery(({ page, pageSize, keyword }) => {
        const params = new URLSearchParams({
          page: String(page),
          pageSize: String(pageSize),
        });
        if (keyword) params.set("keyword", keyword);
        return { url: `/admin/paper-tags?${params.toString()}` };
      }, normalizePaperTagPage),
      providesTags: [{ type: "Paper", id: "TAG_LIST" }],
    }),
    getAdminPaper: builder.query({
      queryFn: normalizedQuery(
        (paperId) => ({ url: `/admin/papers/${paperId}` }),
        normalizeAdminPaper,
      ),
      providesTags: (result, _error, paperId) => [
        { type: "Paper", id: result?.id ?? paperId },
      ],
    }),
    createPaper: builder.mutation({
      queryFn: normalizedQuery(
        (body) => ({ url: "/admin/papers", method: "POST", body }),
        normalizeAdminPaper,
      ),
      invalidatesTags: (_result, error) =>
        error
          ? []
          : [
              { type: "Paper", id: "LIST" },
              { type: "Paper", id: "TAG_LIST" },
            ],
    }),
    updatePaper: builder.mutation({
      queryFn: normalizedQuery(
        ({ paperId, ...body }) => ({
          url: `/admin/papers/${paperId}`,
          method: "PUT",
          body,
        }),
        normalizeAdminPaper,
      ),
      invalidatesTags: invalidatePaper,
    }),
    validatePaper: builder.mutation({
      queryFn: normalizedQuery(
        ({ paperId, concurrencyStamp }) => ({
          url: `/admin/papers/${paperId}/validate`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizePaperValidation,
      ),
    }),
    publishPaper: builder.mutation({
      queryFn: normalizedQuery(
        ({ paperId, concurrencyStamp }) => ({
          url: `/admin/papers/${paperId}/publish`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminPaper,
      ),
      invalidatesTags: invalidatePaper,
    }),
    unpublishPaper: builder.mutation({
      queryFn: normalizedQuery(
        ({ paperId, concurrencyStamp }) => ({
          url: `/admin/papers/${paperId}/unpublish`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminPaper,
      ),
      invalidatesTags: invalidatePaper,
    }),
    archivePaper: builder.mutation({
      queryFn: normalizedQuery(
        ({ paperId, concurrencyStamp }) => ({
          url: `/admin/papers/${paperId}/archive`,
          method: "POST",
          body: { concurrencyStamp },
        }),
        normalizeAdminPaper,
      ),
      invalidatesTags: invalidatePaper,
    }),
    deletePaper: builder.mutation({
      query: ({ paperId, concurrencyStamp }) => ({
        url: `/admin/papers/${paperId}`,
        method: "DELETE",
        body: { concurrencyStamp },
      }),
      invalidatesTags: invalidatePaper,
    }),
  }),
});

export const {
  useArchivePaperMutation,
  useCreatePaperMutation,
  useDeletePaperMutation,
  useGetAdminPaperQuery,
  useGetAdminPapersQuery,
  useGetAdminPaperTagsQuery,
  usePublishPaperMutation,
  useUnpublishPaperMutation,
  useUpdatePaperMutation,
  useValidatePaperMutation,
} = papersApi;
