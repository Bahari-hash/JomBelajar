/** Central mapping for the numeric UserRole values returned by the API. */
export const USER_ROLES = Object.freeze({
  USER: "User",
  EDITOR: "Editor",
  ADMIN: "Admin",
});

const ROLE_BY_VALUE = Object.freeze({
  0: USER_ROLES.USER,
  1: USER_ROLES.EDITOR,
  2: USER_ROLES.ADMIN,
});

export const ROLE_OPTIONS = Object.freeze([
  { value: USER_ROLES.USER, label: "普通用户" },
  { value: USER_ROLES.EDITOR, label: "编辑" },
  { value: USER_ROLES.ADMIN, label: "管理员" },
]);

export function parseUserRole(value) {
  const role = ROLE_BY_VALUE[value];
  if (!role || !Number.isInteger(value)) {
    throw new Error("API returned an invalid user role.");
  }

  return role;
}

export function getRoleLabel(role) {
  return ROLE_OPTIONS.find((option) => option.value === role)?.label ?? "未知角色";
}

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
