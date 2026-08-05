import { Navigate, Outlet, useLocation } from "react-router-dom";
import RouteLoading from "@/components/RouteLoading";
import { getCurrentPath, getSafeReturnTo } from "@/features/auth/returnTo";
import { useAuth } from "@/hooks/useAuth";

export function ProtectedRoute() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === "loading") {
    return <RouteLoading />;
  }
  if (status === "anonymous") {
    const returnTo = getCurrentPath(
      location.pathname,
      location.search,
      location.hash,
    );
    return (
      <Navigate
        replace
        to={`/login?returnTo=${encodeURIComponent(returnTo)}`}
      />
    );
  }
  return <Outlet />;
}

export function PublicOnlyRoute() {
  const { status } = useAuth();
  const location = useLocation();

  if (status === "loading") {
    return <RouteLoading />;
  }
  if (status === "authenticated") {
    const target = getSafeReturnTo(
      new URLSearchParams(location.search).get("returnTo"),
      "/profile",
    );
    return <Navigate replace to={target} />;
  }
  return <Outlet />;
}
