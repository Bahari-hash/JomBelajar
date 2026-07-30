import { useContext, useEffect } from "react";
import { AdminPageContext } from "@/lib/adminPageContext.js";

/** Synchronizes a route page with the shared administrator title and breadcrumb. */
export function useAdminPage(title, breadcrumb = title) {
  const setPageLabel = useContext(AdminPageContext);
  useEffect(() => {
    document.title = `${title} | TinyLang 管理后台`;
    setPageLabel?.(breadcrumb);
    return () => {
      document.title = "TinyLang 管理后台";
      setPageLabel?.(null);
    };
  }, [breadcrumb, setPageLabel, title]);
}
