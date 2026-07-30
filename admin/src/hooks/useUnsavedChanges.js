import { useBeforeUnload, useBlocker } from "react-router-dom";

/** Blocks in-app navigation and browser unload while an editor has unsaved work. */
export function useUnsavedChanges(shouldBlock, allowNavigationRef) {
  const hasActiveUpload = () =>
    Boolean(document.querySelector('[data-uploading="true"]'));
  const blocker = useBlocker(
    () => (shouldBlock || hasActiveUpload()) && !allowNavigationRef?.current,
  );
  useBeforeUnload((event) => {
    if ((shouldBlock || hasActiveUpload()) && !allowNavigationRef?.current)
      event.preventDefault();
  });
  return blocker;
}
