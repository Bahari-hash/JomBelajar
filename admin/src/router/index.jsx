import { createBrowserRouter } from "react-router-dom";
import AdminLayout from "@/layouts/AdminLayout.jsx";
import Dashboard from "@/pages/Dashboard.jsx";
import NotFound from "@/pages/NotFound.jsx";
import RouteError from "@/pages/RouteError.jsx";

export const routes = [
  {
    path: "/",
    element: <AdminLayout />,
    errorElement: <RouteError />,
    children: [
      {
        index: true,
        element: <Dashboard />,
      },
      {
        path: "*",
        element: <NotFound />,
      },
    ],
  },
];

export const router = createBrowserRouter(routes);
