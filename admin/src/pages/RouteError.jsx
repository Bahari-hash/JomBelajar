import { AlertTriangle, ArrowLeft, RotateCcw } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import { Button } from "@/components/ui/button.jsx";

function RouteError() {
  const navigate = useNavigate();

  const handleRetry = () => navigate(0);

  return (
    <main className="flex min-h-dvh items-center justify-center bg-background px-4">
      <section className="max-w-md text-center" aria-labelledby="route-error-title">
        <span className="mx-auto flex size-10 items-center justify-center rounded-lg bg-destructive/10 text-destructive">
          <AlertTriangle aria-hidden="true" className="size-5" />
        </span>
        <h1 id="route-error-title" className="mt-4 text-xl font-semibold">
          页面暂时无法显示
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          请重试，或返回工作台继续操作。
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-2">
          <Button variant="outline" onClick={handleRetry}>
            <RotateCcw aria-hidden="true" data-icon="inline-start" />
            重试
          </Button>
          <Button asChild>
            <Link to="/">
              <ArrowLeft aria-hidden="true" data-icon="inline-start" />
              返回工作台
            </Link>
          </Button>
        </div>
      </section>
    </main>
  );
}

export default RouteError;
