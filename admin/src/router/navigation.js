import { LayoutDashboard, Users } from "lucide-react";

/** Single navigation source shared by desktop and mobile admin shells. */
export const NAVIGATION_ITEMS = Object.freeze([
  {
    label: "工作台",
    href: "/",
    icon: LayoutDashboard,
  },
  {
    label: "用户管理",
    href: "/users",
    icon: Users,
  },
]);
