export async function withRefreshLock<T>(
  operation: () => Promise<T>,
): Promise<T> {
  if (typeof navigator === "undefined" || !navigator.locks?.request) {
    return operation();
  }

  return navigator.locks.request("tiny-lang.auth.refresh", operation);
}
