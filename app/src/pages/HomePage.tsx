import { ArrowRight } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { moduleItems } from "@/constants/navigation";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

export default function HomePage() {
  useDocumentTitle("TinyLang");
  const location = useLocation();

  return (
    <div className="space-y-10">
      {location.state?.notice ? (
        <div className="alert alert-success text-sm" role="status">
          {location.state.notice}
        </div>
      ) : null}
      <section className="max-w-3xl space-y-3" aria-labelledby="welcome-title">
        <p className="text-sm font-semibold text-primary">开始学习</p>
        <h1 id="welcome-title" className="text-3xl font-bold sm:text-4xl">
          今天想练习什么？
        </h1>
        <p className="max-w-2xl text-base leading-7 text-base-content/70">
          从阅读、视频、词汇或在线测试中选择一个方向，按自己的节奏继续学习。
        </p>
      </section>

      <section aria-labelledby="modules-title">
        <h2 id="modules-title" className="mb-4 text-xl font-semibold">
          学习模块
        </h2>
        <div className="grid gap-4 sm:grid-cols-2">
          {moduleItems.map((item) => {
            const Icon = item.icon;
            return (
              <Link
                key={item.to}
                className="group flex min-h-36 items-start gap-4 rounded-lg border border-base-300 bg-base-100 p-5 transition-colors hover:border-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
                to={item.to}
              >
                <span className="grid size-11 shrink-0 place-items-center rounded-md badge badge-outline text-secondary-content">
                  <Icon aria-hidden="true" className="size-5" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-center justify-between gap-3 text-lg font-semibold">
                    {item.label}
                    <ArrowRight
                      aria-hidden="true"
                      className="size-5 shrink-0 transition-transform group-hover:translate-x-1"
                    />
                  </span>
                  <span className="mt-2 block leading-6 text-base-content/70">
                    {item.description}
                  </span>
                </span>
              </Link>
            );
          })}
        </div>
      </section>
    </div>
  );
}
