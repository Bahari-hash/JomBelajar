import { describe, expect, it } from "vitest";
import {
  authStorageKey,
  clearStoredRefreshToken,
  readStoredRefreshToken,
  writeStoredRefreshToken,
} from "@/features/auth/authStorage";

describe("authStorage", () => {
  it("stores only a refresh token in the current tab", () => {
    writeStoredRefreshToken("refresh-value");
    expect(sessionStorage.getItem(authStorageKey)).toBe(
      '{"refreshToken":"refresh-value"}',
    );
    expect(readStoredRefreshToken()).toBe("refresh-value");
  });

  it("rejects malformed values and tolerates unavailable storage", () => {
    sessionStorage.setItem(authStorageKey, '{"refreshToken":123}');
    expect(readStoredRefreshToken()).toBeNull();

    const unavailable = {
      getItem: () => {
        throw new Error("blocked");
      },
      setItem: () => {
        throw new Error("blocked");
      },
      removeItem: () => {
        throw new Error("blocked");
      },
    };
    expect(readStoredRefreshToken(unavailable)).toBeNull();
    expect(() => writeStoredRefreshToken("refresh", unavailable)).not.toThrow();
    expect(() => clearStoredRefreshToken(unavailable)).not.toThrow();
  });
});
