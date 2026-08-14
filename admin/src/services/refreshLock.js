export async function withRefreshLock(operation) {
  if (typeof navigator === "undefined" || !navigator.locks?.request) {
    return operation();
  }

  return navigator.locks.request("tiny-lang.auth.refresh", operation);
}
