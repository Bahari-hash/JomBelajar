import type { LucideIcon } from "lucide-react";

interface ModulePlaceholderProps {
  title: string;
  description: string;
  icon: LucideIcon;
}

/** Honest empty state used until each public learning module is connected. */
export default function ModulePlaceholder({
  title,
  description,
  icon: Icon,
}: ModulePlaceholderProps) {
  return (
    <section className="space-y-8" aria-labelledby="module-title">
      <header className="space-y-2">
        <h1 id="module-title" className="text-3xl font-bold">
          {title}
        </h1>
        <p className="max-w-2xl leading-7 text-base-content/70">
          {description}
        </p>
      </header>
      <div className="flex min-h-64 flex-col items-center justify-center rounded-lg border border-dashed border-base-300 px-6 text-center">
        <span className="mb-4 grid size-12 place-items-center rounded-md bg-base-200">
          <Icon aria-hidden="true" className="size-6" />
        </span>
        <h2 className="text-lg font-semibold">内容正在准备中</h2>
        <p className="mt-2 max-w-md text-sm leading-6 text-base-content/65">
          该学习模块尚未开放，后续接入内容后会在这里展示。
        </p>
      </div>
    </section>
  );
}
