import { createBrowserRouter } from "react-router-dom";
import { SessionLoading } from "@/components/SessionLoading.jsx";
import RouteError from "@/pages/RouteError.jsx";
import { AuthBootstrap } from "@/router/AuthBootstrap.jsx";
import { AuthGuard, PublicAuthGuard } from "@/router/AuthGuard.jsx";

const lazyComponent = (importer) => async () => {
  const module = await importer();
  return { Component: module.default };
};

export const routes = [
  {
    path: "/",
    element: <AuthBootstrap />,
    HydrateFallback: SessionLoading,
    errorElement: <RouteError />,
    children: [
      {
        element: <PublicAuthGuard />,
        children: [
          {
            path: "login",
            lazy: lazyComponent(() => import("@/pages/Login.jsx")),
          },
          {
            path: "forbidden",
            lazy: lazyComponent(() => import("@/pages/Forbidden.jsx")),
          },
        ],
      },
      {
        element: <AuthGuard />,
        children: [
          {
            lazy: lazyComponent(() => import("@/layouts/AdminLayout.jsx")),
            children: [
              {
                index: true,
                lazy: lazyComponent(() => import("@/pages/Dashboard.jsx")),
              },
              {
                path: "users",
                lazy: lazyComponent(() => import("@/pages/Users.jsx")),
              },
              {
                path: "articles",
                lazy: lazyComponent(() => import("@/pages/Articles.jsx")),
              },
              {
                path: "articles/new",
                lazy: lazyComponent(() => import("@/pages/ArticleEditor.jsx")),
              },
              {
                path: "articles/:articleId/edit",
                lazy: lazyComponent(() => import("@/pages/ArticleEditor.jsx")),
              },
              {
                path: "articles/:articleId/preview",
                lazy: lazyComponent(() => import("@/pages/ArticlePreview.jsx")),
              },
              {
                path: "article-categories",
                lazy: lazyComponent(
                  () => import("@/pages/ArticleCategories.jsx"),
                ),
              },
              {
                path: "videos",
                lazy: lazyComponent(() => import("@/pages/Videos.jsx")),
              },
              {
                path: "videos/new",
                lazy: lazyComponent(() => import("@/pages/VideoCreate.jsx")),
              },
              {
                path: "videos/:videoId",
                lazy: lazyComponent(() => import("@/pages/VideoDetails.jsx")),
              },
              {
                path: "video-categories",
                lazy: lazyComponent(
                  () => import("@/pages/VideoCategories.jsx"),
                ),
              },
              {
                path: "words",
                lazy: lazyComponent(() => import("@/pages/Words.jsx")),
              },
              {
                path: "words/new",
                lazy: lazyComponent(() => import("@/pages/WordEditor.jsx")),
              },
              {
                path: "words/batch",
                lazy: lazyComponent(
                  () => import("@/pages/WordBatchImport.jsx"),
                ),
              },
              {
                path: "words/:wordId",
                lazy: lazyComponent(() => import("@/pages/WordEditor.jsx")),
              },
              {
                path: "papers",
                lazy: lazyComponent(() => import("@/pages/Papers.jsx")),
              },
              {
                path: "papers/new",
                lazy: lazyComponent(() => import("@/pages/PaperEditor.jsx")),
              },
              {
                path: "papers/:paperId",
                lazy: lazyComponent(() => import("@/pages/PaperEditor.jsx")),
              },
              {
                path: "settings",
                lazy: lazyComponent(
                  () => import("@/pages/SystemSettings.jsx"),
                ),
              },
              {
                path: "*",
                lazy: lazyComponent(() => import("@/pages/NotFound.jsx")),
              },
            ],
          },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes);
