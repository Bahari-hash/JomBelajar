import { ArrowLeft } from "lucide-react";
import { Link } from "react-router-dom";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function NotFoundPage() {
  useDocumentTitle("页面未找到");
  return (
    <section className="flex min-h-96 flex-col items-center justify-center text-center">
      <p className="text-sm font-semibold text-primary">404</p>
      <h1 className="mt-2 text-3xl font-bold">页面未找到</h1>
      <p className="mt-3 text-base-content/70">
        地址可能已失效，或页面尚未开放。
      </p>
      <Link className="btn btn-primary mt-6" to="/">
        <ArrowLeft aria-hidden="true" className="size-4" />
        返回首页
      </Link>
    </section>
  );
}
