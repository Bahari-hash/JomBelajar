import { baseApi } from "@/services/baseApi.js";
import {
  normalizeAdminPaper,
  normalizePaperPage,
  normalizePaperValidation,
} from "@/services/paperContracts.js";
import {
  normalizePaperBatchImport,
  normalizePaperBatchValidation,
} from "@/services/paperBatchContracts.js";

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
  if (filters.status) params.set("status", filters.status);
  if (filters.categoryId) params.set("categoryId", filters.categoryId);
  return `/admin/papers?${params.toString()}`;
}

function invalidatePaper(_result, error, { paperId }) {
  return error
    ? []
    : [
        { type: "Paper", id: paperId },
        { type: "Paper", id: "LIST" },
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
    validatePaperBatch: builder.mutation({
      queryFn: normalizedQuery(
        (body) => ({
          url: "/admin/papers/batch/validate",
          method: "POST",
          body,
        }),
        normalizePaperBatchValidation,
      ),
    }),
    importPaperBatch: builder.mutation({
      async queryFn(body, _api, _extraOptions, baseQuery) {
        const result = await baseQuery({
          url: "/admin/papers/batch",
          method: "POST",
          body,
        });
        if (result.error?.status === 422) {
          try {
            return {
              error: {
                ...result.error,
                data: normalizePaperBatchValidation(result.error.data),
              },
            };
          } catch {
            return { error: CONTRACT_ERROR };
          }
        }
        if (result.error) return result;
        try {
          return { data: normalizePaperBatchImport(result.data) };
        } catch {
          return { error: CONTRACT_ERROR };
        }
      },
      invalidatesTags: (_result, error) =>
        error ? [] : [{ type: "Paper", id: "LIST" }],
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
        error ? [] : [{ type: "Paper", id: "LIST" }],
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
  useImportPaperBatchMutation,
  usePublishPaperMutation,
  useUnpublishPaperMutation,
  useUpdatePaperMutation,
  useValidatePaperBatchMutation,
  useValidatePaperMutation,
} = papersApi;
