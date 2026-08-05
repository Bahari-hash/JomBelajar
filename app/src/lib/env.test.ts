import { describe, expect, it } from "vitest";
import { normalizeApiBaseUrl } from "@/lib/env";

describe("normalizeApiBaseUrl", () => {
  it.each([
    [undefined, "/api"],
    ["  ", "/api"],
    ["/api/", "/api"],
    ["/custom/api///", "/custom/api"],
    ["https://api.example.com/", "https://api.example.com"],
  ])("normalizes %s", (input, expected) => {
    expect(normalizeApiBaseUrl(input)).toBe(expected);
  });

  it.each([
    "ftp://example.com",
    "javascript:alert(1)",
    "api",
    "//example.com/api",
  ])("falls back for unsupported API base URL %s", (input) => {
    expect(normalizeApiBaseUrl(input)).toBe("/api");
  });
});
