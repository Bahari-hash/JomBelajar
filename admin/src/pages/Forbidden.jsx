import { ShieldX } from "lucide-react";
import { useDispatch, useSelector } from "react-redux";
import { useNavigate } from "react-router-dom";
import { ThemeMenu } from "@/components/ThemeMenu.jsx";
import { Button } from "@/components/ui/button.jsx";
import { authSession } from "@/services/authSession.js";
import { sessionUnauthenticated } from "@/store/authSlice.js";

function Forbidden() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const message = useSelector((state) => state.auth.message);

  const handleReturnToLogin = () => {
    authSession.clear();
    dispatch(sessionUnauthenticated());
    navigate("/login", { replace: true });
  };

  return (
    <main className="flex min-h-dvh items-center justify-center bg-background px-4">
      <div className="absolute right-4 top-4">
        <ThemeMenu />
      </div>
      <section
        className="max-w-md text-center"
        aria-labelledby="forbidden-title"
      >
        <span className="mx-auto flex size-10 items-center justify-center rounded-lg bg-muted text-foreground">
          <ShieldX aria-hidden="true" className="size-5" />
        </span>
        <p className="mt-4 text-sm font-medium text-muted-foreground">403</p>
        <h1 id="forbidden-title" className="mt-1 text-xl font-semibold">
          无权访问管理后台
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          {message ?? "当前账户不是管理员，无法继续访问。"}
        </p>
        <Button className="mt-6" onClick={handleReturnToLogin}>
          返回登录
        </Button>
      </section>
    </main>
  );
}

export default Forbidden;
