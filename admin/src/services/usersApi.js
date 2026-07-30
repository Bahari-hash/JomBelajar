import { baseApi } from "@/services/baseApi.js";
import { parseUserRole } from "@/services/roles.js";

function normalizeUser(value) {
  if (!value || typeof value.id !== "string" || typeof value.email !== "string") {
    throw new Error("API returned invalid user data.");
  }

  return { ...value, role: parseUserRole(value.role) };
}

function normalizePage(value) {
  if (!value || !Array.isArray(value.items)) {
    throw new Error("API returned invalid pagination data.");
  }

  return { ...value, items: value.items.map(normalizeUser) };
}

function buildListUrl(filters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  });
  if (filters.keyword) params.set("keyword", filters.keyword);
  if (filters.role) params.set("role", filters.role);
  if (filters.status) params.set("status", filters.status);
  return `/admin/users?${params.toString()}`;
}

function invalidateUserOnSuccess(_result, error, { userId }) {
  return error
    ? []
    : [
        { type: "AdminUser", id: userId },
        { type: "AdminUser", id: "LIST" },
      ];
}

/** Real administrator user endpoints backed by the server's current DTO contract. */
export const usersApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    getAdminUsers: builder.query({
      query: (filters) => ({ url: buildListUrl(filters) }),
      transformResponse: normalizePage,
      providesTags: (result) =>
        result
          ? [
              { type: "AdminUser", id: "LIST" },
              ...result.items.map((user) => ({ type: "AdminUser", id: user.id })),
            ]
          : [{ type: "AdminUser", id: "LIST" }],
    }),
    getAdminUser: builder.query({
      query: (userId) => ({ url: `/admin/users/${userId}` }),
      transformResponse: normalizeUser,
      providesTags: (_result, _error, userId) => [{ type: "AdminUser", id: userId }],
    }),
    banUser: builder.mutation({
      query: ({ userId, reason }) => ({
        url: `/admin/users/${userId}/ban`,
        method: "POST",
        body: { reason },
      }),
      invalidatesTags: invalidateUserOnSuccess,
    }),
    unbanUser: builder.mutation({
      query: ({ userId }) => ({
        url: `/admin/users/${userId}/unban`,
        method: "POST",
      }),
      invalidatesTags: invalidateUserOnSuccess,
    }),
    updateUserRole: builder.mutation({
      query: ({ userId, role }) => ({
        url: `/admin/users/${userId}/role`,
        method: "POST",
        body: { role },
      }),
      invalidatesTags: invalidateUserOnSuccess,
    }),
    revokeUserSessions: builder.mutation({
      query: ({ userId }) => ({
        url: `/auth/admin/users/${userId}/revoke`,
        method: "POST",
      }),
      invalidatesTags: invalidateUserOnSuccess,
    }),
  }),
});

export const {
  useBanUserMutation,
  useGetAdminUserQuery,
  useGetAdminUsersQuery,
  useRevokeUserSessionsMutation,
  useUnbanUserMutation,
  useUpdateUserRoleMutation,
} = usersApi;
