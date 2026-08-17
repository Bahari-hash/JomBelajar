import { baseApi } from "@/services/baseApi.js";
import {
  normalizeAdminWord,
  normalizeWordPage,
} from "@/services/wordContracts.js";
import {
  normalizeWordBatchImport,
  normalizeWordBatchValidation,
} from "@/services/wordBatchContracts.js";

const CONTRACT_ERROR = Object.freeze({
  status: "CUSTOM_ERROR",
  detail: "服务端返回的单词数据格式无法识别，请刷新后重试。",
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

function buildWordListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  });
  for (const key of ["keyword", "partOfSpeech", "definition"])
    if (filters[key]) params.set(key, filters[key]);
  return `/admin/words?${params.toString()}`;
}

function invalidateWord(_result, error, { wordId }) {
  return error
    ? []
    : [
        { type: "Word", id: wordId },
        { type: "Word", id: "LIST" },
      ];
}

/** RTK Query endpoints for global word management. */
export const wordsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAdminWords: builder.query({
      queryFn: normalizedQuery(
        (filters) => ({ url: buildWordListUrl(filters) }),
        normalizeWordPage,
      ),
      providesTags: (result) =>
        result
          ? [
              { type: "Word", id: "LIST" },
              ...result.items.map(({ id }) => ({ type: "Word", id })),
            ]
          : [{ type: "Word", id: "LIST" }],
    }),
    getAdminWord: builder.query({
      queryFn: normalizedQuery(
        (wordId) => ({ url: `/admin/words/${wordId}` }),
        normalizeAdminWord,
      ),
      providesTags: (_result, _error, wordId) => [{ type: "Word", id: wordId }],
    }),
    validateWordBatch: builder.mutation({
      queryFn: normalizedQuery(
        (body) => ({
          url: "/admin/words/batch/validate",
          method: "POST",
          body,
        }),
        normalizeWordBatchValidation,
      ),
    }),
    importWordBatch: builder.mutation({
      async queryFn(body, _api, _extraOptions, baseQuery) {
        const result = await baseQuery({
          url: "/admin/words/batch",
          method: "POST",
          body,
        });
        if (result.error?.status === 422) {
          try {
            return {
              error: {
                ...result.error,
                data: normalizeWordBatchValidation(result.error.data),
              },
            };
          } catch {
            return { error: CONTRACT_ERROR };
          }
        }
        if (result.error) return result;
        try {
          return { data: normalizeWordBatchImport(result.data) };
        } catch {
          return { error: CONTRACT_ERROR };
        }
      },
      invalidatesTags: (_result, error) =>
        error ? [] : [{ type: "Word", id: "LIST" }],
    }),
    createWord: builder.mutation({
      queryFn: normalizedQuery(
        (body) => ({ url: "/admin/words", method: "POST", body }),
        normalizeAdminWord,
      ),
      invalidatesTags: (_result, error) =>
        error ? [] : [{ type: "Word", id: "LIST" }],
    }),
    updateWord: builder.mutation({
      queryFn: normalizedQuery(
        ({ wordId, ...body }) => ({
          url: `/admin/words/${wordId}`,
          method: "PUT",
          body,
        }),
        normalizeAdminWord,
      ),
      invalidatesTags: invalidateWord,
    }),
    deleteWord: builder.mutation({
      query: ({ wordId, concurrencyStamp }) => ({
        url: `/admin/words/${wordId}`,
        method: "DELETE",
        body: { concurrencyStamp },
      }),
      invalidatesTags: invalidateWord,
    }),
  }),
});

export const {
  useCreateWordMutation,
  useDeleteWordMutation,
  useGetAdminWordQuery,
  useGetAdminWordsQuery,
  useImportWordBatchMutation,
  useUpdateWordMutation,
  useValidateWordBatchMutation,
} = wordsApi;
