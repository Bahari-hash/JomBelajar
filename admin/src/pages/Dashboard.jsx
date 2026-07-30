import { LayoutDashboard } from "lucide-react";

function Dashboard() {
  return (
    <div className="space-y-8">
      <header>
        <p className="text-sm font-medium text-primary">TinyLang 管理后台</p>
        <h1 className="mt-1 text-2xl font-semibold">工作台</h1>
      </header>

      <section aria-labelledby="modules-title" className="border-t pt-6">
        <div className="flex min-h-48 flex-col items-center justify-center text-center">
          <LayoutDashboard
            aria-hidden="true"
            className="size-8 text-muted-foreground"
          />
          <h2 id="modules-title" className="mt-4 text-sm font-medium">
            暂无可用管理模块
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            后续管理功能将在此处显示。
          </p>
        </div>
      </section>
    </div>
  );
}

export default Dashboard;
