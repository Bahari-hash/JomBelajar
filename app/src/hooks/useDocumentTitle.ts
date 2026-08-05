import { useEffect } from "react";

/** Keeps route-level document titles consistent with the TinyLang suffix. */
export function useDocumentTitle(title: string) {
  useEffect(() => {
    document.title = title === "TinyLang" ? title : `${title} | TinyLang`;
  }, [title]);
}
