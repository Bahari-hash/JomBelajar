import { ArrowLeft } from "lucide-react";
import { Link } from "react-router-dom";
import { Button } from "@/components/ui/button.jsx";

function NotFound() {
  return (
    <section className="flex min-h-[60vh] flex-col items-center justify-center px-4 text-center">
      <p className="text-sm font-medium text-primary">404</p>
      <h1 className="mt-2 text-2xl font-semibold">页面未找到</h1>
      <p className="mt-2 max-w-md text-sm text-muted-foreground">
        该地址不存在或已经失效。
      </p>
      <Button asChild className="mt-6">
        <Link to="/">
          <ArrowLeft aria-hidden="true" data-icon="inline-start" />
          返回工作台
        </Link>
      </Button>
    </section>
  );
}

export default NotFound;
