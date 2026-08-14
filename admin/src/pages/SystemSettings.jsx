import { Settings } from "lucide-react";
import { useAdminPage } from "@/hooks/useAdminPage.js";

function SystemSettings() {
  useAdminPage("系统设置");

  return (
    <div className="space-y-8">
      <header>
        <p className="text-sm font-medium text-muted-foreground">设置</p>
        <h1 className="mt-1 text-2xl font-semibold">系统设置</h1>
        {/* <p className="mt-1 text-sm text-muted-foreground">
          集中管理平台级设置项。
        </p> */}
      </header>

      <section
        aria-labelledby="system-settings-empty-title"
        className="border-t pt-6"
      >
        <div className="flex min-h-48 flex-col items-center justify-center text-center">
          <Settings
            aria-hidden="true"
            className="size-8 text-muted-foreground"
          />
          <h2
            id="system-settings-empty-title"
            className="mt-4 text-sm font-medium"
          >
            暂无系统设置项
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            系统设置项将在后续版本中开放。
          </p>
        </div>
      </section>
    </div>
  );
}

export default SystemSettings;
