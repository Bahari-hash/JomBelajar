import { RotateCcw } from "lucide-react";
import { isRouteErrorResponse, Link, useRouteError } from "react-router-dom";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function RouteErrorPage() {
  useDocumentTitle("页面错误");
  const error = useRouteError();
  const status = isRouteErrorResponse(error) ? error.status : null;

  return (
    <main className="grid min-h-dvh place-items-center bg-base-100 px-4 text-base-content">
      <section className="max-w-lg text-center">
        <p className="text-sm font-semibold text-error">
          {status ?? "页面错误"}
        </p>
        <h1 className="mt-2 text-3xl font-bold">暂时无法打开此页面</h1>
        <p className="mt-3 leading-7 text-base-content/70">
          请稍后重试。若问题持续存在，可以先返回首页继续使用其他功能。
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <button
            className="btn btn-primary"
            type="button"
            onClick={() => window.location.reload()}
          >
            <RotateCcw aria-hidden="true" className="size-4" />
            重新加载
          </button>
          <Link className="btn btn-ghost" to="/">
            返回首页
          </Link>
        </div>
      </section>
    </main>
  );
}
