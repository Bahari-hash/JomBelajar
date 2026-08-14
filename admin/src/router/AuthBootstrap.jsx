import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Outlet } from "react-router-dom";
import { authSession } from "@/services/authSession.js";
import { markApiSessionActive } from "@/services/baseApi.js";
import {
  AUTH_STATUS,
  sessionAuthenticated,
  sessionForbidden,
  sessionUnauthenticated,
} from "@/store/authSlice.js";

/** Restores the rotating refresh-token session once before route guards reveal content. */
export function AuthBootstrap() {
  const dispatch = useDispatch();
  const status = useSelector((state) => state.auth.status);

  useEffect(() => {
    let active = true;

    if (status !== AUTH_STATUS.BOOTSTRAPPING) {
      return () => {
        active = false;
      };
    }

    authSession
      .refresh()
      .then((session) => {
        if (active) {
          markApiSessionActive();
          dispatch(sessionAuthenticated(session.user));
        }
      })
      .catch((error) => {
        if (!active) return;
        authSession.clear();
        if (error?.status === 403) {
          dispatch(sessionForbidden("仅管理员可以访问管理后台。"));
        } else {
          dispatch(sessionUnauthenticated("登录状态已失效，请重新登录。"));
        }
      });

    return () => {
      active = false;
    };
  }, [dispatch, status]);

  return <Outlet />;
}
