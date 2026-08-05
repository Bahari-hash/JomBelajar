const unsafeQueryKeys =
  /(?:^|[?&])(?:token|refreshToken|password|verificationCode|code)=/i;

/** Accepts only same-origin paths without credential-like query parameters. */
export function getSafeReturnTo(
  value: string | null | undefined,
  fallback = "/profile",
) {
  if (
    !value ||
    !value.startsWith("/") ||
    value.startsWith("//") ||
    unsafeQueryKeys.test(value)
  ) {
    return fallback;
  }

  try {
    const parsed = new URL(value, window.location.origin);
    if (parsed.origin !== window.location.origin) {
      return fallback;
    }
    return `${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return fallback;
  }
}

export function getCurrentPath(pathname: string, search: string, hash = "") {
  return `${pathname}${search}${hash}`;
}
