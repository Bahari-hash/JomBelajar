import { describe, expect, it } from "vitest";
import {
  normalizeAuthUser,
  parseUserRole,
  USER_ROLES,
} from "@/services/roles.js";

describe("UserRole HTTP contract", () => {
  it("accepts exact server enum member names", () => {
    expect(parseUserRole("User")).toBe(USER_ROLES.USER);
    expect(parseUserRole("Admin")).toBe(USER_ROLES.ADMIN);
    expect(
      normalizeAuthUser({
        id: "11111111-1111-1111-1111-111111111111",
        email: "admin@example.test",
        role: "Admin",
      }),
    ).toMatchObject({ role: USER_ROLES.ADMIN });
  });

  it.each([0, 1, 2, "Editor", "admin", "Unknown", null])(
    "rejects obsolete or invalid role value %s",
    (value) => {
      expect(() => parseUserRole(value)).toThrow(
        "API returned an invalid user role.",
      );
    },
  );
});
