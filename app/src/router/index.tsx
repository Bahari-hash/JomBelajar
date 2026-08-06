import { createBrowserRouter, type RouteObject } from "react-router-dom";
import RouteLoading from "@/components/RouteLoading";
import AppLayout from "@/layouts/AppLayout";
import RouteErrorPage from "@/pages/RouteErrorPage";
import { ProtectedRoute, PublicOnlyRoute } from "@/router/AuthRouteBoundaries";

export const routes: RouteObject[] = [
  {
    path: "/",
    Component: AppLayout,
    ErrorBoundary: RouteErrorPage,
    HydrateFallback: RouteLoading,
    children: [
      {
        Component: PublicOnlyRoute,
        children: [
          {
            path: "login",
            lazy: async () => ({
              Component: (await import("@/pages/LoginPage")).default,
            }),
          },
          {
            path: "register",
            lazy: async () => ({
              Component: (await import("@/pages/RegisterPage")).default,
            }),
          },
          {
            path: "forgot-password",
            lazy: async () => ({
              Component: (await import("@/pages/ForgotPasswordPage")).default,
            }),
          },
        ],
      },
      {
        Component: ProtectedRoute,
        children: [
          {
            path: "profile",
            lazy: async () => ({
              Component: (await import("@/pages/ProfilePage")).default,
            }),
          },
        ],
      },
      {
        index: true,
        lazy: async () => ({
          Component: (await import("@/pages/HomePage")).default,
        }),
      },
      {
        path: "articles",
        lazy: async () => ({
          Component: (await import("@/pages/ArticlesPage")).default,
        }),
      },
      {
        path: "articles/:articleId",
        lazy: async () => ({
          Component: (await import("@/pages/ArticleDetailPage")).default,
        }),
      },
      {
        path: "videos",
        lazy: async () => ({
          Component: (await import("@/pages/VideosPage")).default,
        }),
      },
      {
        path: "words",
        lazy: async () => ({
          Component: (await import("@/pages/WordsPage")).default,
        }),
      },
      {
        path: "papers",
        lazy: async () => ({
          Component: (await import("@/pages/PapersPage")).default,
        }),
      },
      {
        path: "*",
        lazy: async () => ({
          Component: (await import("@/pages/NotFoundPage")).default,
        }),
      },
    ],
  },
];

export const router = createBrowserRouter(routes);
