import { useSelector } from "react-redux";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { SessionLoading } from "@/components/SessionLoading.jsx";
import { AUTH_STATUS } from "@/store/authSlice.js";

function getInternalDestination(location) {
  return `${location.pathname}${location.search}${location.hash}`;
}

/** Protects the complete administrator shell without duplicating role checks in pages. */
export function AuthGuard() {
  const { status } = useSelector((state) => state.auth);
  const location = useLocation();

  if (status === AUTH_STATUS.BOOTSTRAPPING) {
    return <SessionLoading />;
  }
  if (status === AUTH_STATUS.AUTHENTICATED) {
    return <Outlet />;
  }
  if (status === AUTH_STATUS.FORBIDDEN) {
    return <Navigate to="/forbidden" replace />;
  }

  return (
    <Navigate
      to="/login"
      replace
      state={{ from: getInternalDestination(location) }}
    />
  );
}

/** Keeps public auth routes hidden until bootstrap completes and redirects active admins. */
export function PublicAuthGuard() {
  const { status } = useSelector((state) => state.auth);

  if (status === AUTH_STATUS.BOOTSTRAPPING) {
    return <SessionLoading />;
  }
  if (status === AUTH_STATUS.AUTHENTICATED) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
}
