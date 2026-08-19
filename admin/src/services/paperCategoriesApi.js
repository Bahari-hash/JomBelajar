import { baseApi } from "@/services/baseApi.js";
import {
  normalizePaperCategory,
  normalizePaperCategoryPage,
} from "@/services/paperCategoryContracts.js";

function listUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
    includeInactive: String(filters.includeInactive),
  });
  if (filters.keyword) params.set("keyword", filters.keyword);
  return `/admin/paper-categories?${params.toString()}`;
}
function invalidate(_result, error, { categoryId } = {}) {
  return error
    ? []
    : [
        { type: "PaperCategory", id: categoryId ?? "LIST" },
        { type: "PaperCategory", id: "LIST" },
        { type: "PaperCategory", id: "OPTIONS" },
        { type: "Paper", id: "LIST" },
      ];
}
export const paperCategoriesApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getPaperCategories: builder.query({
      query: (filters) => ({ url: listUrl(filters) }),
      transformResponse: normalizePaperCategoryPage,
      providesTags: (result) =>
        result
          ? [
              { type: "PaperCategory", id: "LIST" },
              ...result.items.map(({ id }) => ({ type: "PaperCategory", id })),
            ]
          : [{ type: "PaperCategory", id: "LIST" }],
    }),
    getAllPaperCategoryOptions: builder.query({
      async queryFn(_arg, _api, _extra, baseQuery) {
        const items = [];
        let page = 1;
        let totalPages = 1;
        do {
          const result = await baseQuery({
            url: listUrl({
              page,
              pageSize: 100,
              includeInactive: true,
              keyword: "",
            }),
          });
          if (result.error) return { error: result.error };
          const normalized = normalizePaperCategoryPage(result.data);
          items.push(...normalized.items);
          totalPages = normalized.totalPages;
          page += 1;
        } while (page <= totalPages && page <= 1000);
        return {
          data: Array.from(
            new Map(items.map((item) => [item.id, item])).values(),
          ),
        };
      },
      providesTags: [{ type: "PaperCategory", id: "OPTIONS" }],
    }),
    createPaperCategory: builder.mutation({
      query: (body) => ({
        url: "/admin/paper-categories",
        method: "POST",
        body,
      }),
      transformResponse: normalizePaperCategory,
      invalidatesTags: (_r, error) =>
        error
          ? []
          : [
              { type: "PaperCategory", id: "LIST" },
              { type: "PaperCategory", id: "OPTIONS" },
            ],
    }),
    updatePaperCategory: builder.mutation({
      query: ({ categoryId, ...body }) => ({
        url: `/admin/paper-categories/${categoryId}`,
        method: "PUT",
        body,
      }),
      transformResponse: normalizePaperCategory,
      invalidatesTags: invalidate,
    }),
    deletePaperCategory: builder.mutation({
      query: ({ categoryId }) => ({
        url: `/admin/paper-categories/${categoryId}`,
        method: "DELETE",
      }),
      invalidatesTags: invalidate,
    }),
  }),
});
export const {
  useGetPaperCategoriesQuery,
  useGetAllPaperCategoryOptionsQuery,
  useCreatePaperCategoryMutation,
  useUpdatePaperCategoryMutation,
  useDeletePaperCategoryMutation,
} = paperCategoriesApi;
