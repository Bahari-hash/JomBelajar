import { useEffect } from "react";

/** Keeps route-level document titles consistent with the JomBelajar suffix. */
export function useDocumentTitle(title: string) {
  useEffect(() => {
    document.title = title === "JomBelajar" ? title : `${title} | JomBelajar`;
  }, [title]);
}
