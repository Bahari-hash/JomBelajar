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
          {
            path: "papers",
            lazy: async () => ({
              Component: (await import("@/pages/PapersPage")).default,
            }),
          },
          {
            path: "papers/:paperId",
            lazy: async () => ({
              Component: (await import("@/pages/PaperDetailPage")).default,
            }),
          },
          {
            path: "wrong-questions",
            lazy: async () => ({
              Component: (await import("@/pages/WrongQuestionsPage")).default,
            }),
          },
          {
            path: "paper-attempts/:attemptId",
            lazy: async () => ({
              Component: (await import("@/pages/PaperAttemptPage")).default,
            }),
          },
        ],
      },
      {
        Component: ProtectedRoute,
        children: [
          {
            index: true,
            lazy: async () => ({
              Component: (await import("@/pages/HomePage")).default,
            }),
          },
        ],
      },
      {
        Component: ProtectedRoute,
        children: [
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
        ],
      },
      {
        Component: ProtectedRoute,
        children: [
          {
            path: "videos",
            lazy: async () => ({
              Component: (await import("@/pages/VideosPage")).default,
            }),
          },
          {
            path: "videos/:videoId",
            lazy: async () => ({
              Component: (await import("@/pages/VideoDetailPage")).default,
            }),
          },
        ],
      },
      {
        Component: ProtectedRoute,
        children: [
          {
            path: "words",
            lazy: async () => ({
              Component: (await import("@/pages/WordsPage")).default,
            }),
          },
          {
            path: "words/learning",
            lazy: async () => ({
              Component: (await import("@/pages/WordLearningPage")).default,
            }),
          },
          {
            path: "words/review",
            lazy: async () => ({
              Component: (await import("@/pages/WordReviewPage")).default,
            }),
          },
        ],
      },
      {
        Component: ProtectedRoute,
        children: [
          {
            path: "*",
            lazy: async () => ({
              Component: (await import("@/pages/NotFoundPage")).default,
            }),
          },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes);
