/** Stable UserRole strings exposed by the HTTP JSON contract. */
export const USER_ROLES = Object.freeze({
  USER: "User",
  EDITOR: "Editor",
  ADMIN: "Admin",
});

const VALID_USER_ROLES = new Set(Object.values(USER_ROLES));

export const ROLE_OPTIONS = Object.freeze([
  { value: USER_ROLES.USER, label: "普通用户" },
  { value: USER_ROLES.EDITOR, label: "编辑" },
  { value: USER_ROLES.ADMIN, label: "管理员" },
]);

/** Validates and returns an exact UserRole string from an API response. */
export function parseUserRole(value) {
  if (typeof value !== "string" || !VALID_USER_ROLES.has(value)) {
    throw new Error("API returned an invalid user role.");
  }

  return value;
}

export function getRoleLabel(role) {
  return ROLE_OPTIONS.find((option) => option.value === role)?.label ?? "未知角色";
}

/** Validates the minimal authenticated-user response without retaining tokens. */
export function normalizeAuthUser(value) {
  if (
    !value ||
    typeof value.id !== "string" ||
    typeof value.email !== "string"
  ) {
    throw new Error("API returned invalid administrator identity data.");
  }

  return {
    id: value.id,
    email: value.email,
    role: parseUserRole(value.role),
  };
}
