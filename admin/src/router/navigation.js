import {
  Clapperboard,
  FileText,
  FolderTree,
  LayoutDashboard,
  Tags,
  Users,
} from "lucide-react";

/** Single navigation source shared by desktop and mobile admin shells. */
export const NAVIGATION_GROUPS = Object.freeze([
  {
    label: null,
    items: [
      { label: "工作台", href: "/", icon: LayoutDashboard },
      { label: "用户管理", href: "/users", icon: Users },
    ],
  },
  {
    label: "内容管理",
    items: [
      { label: "文章管理", href: "/articles", icon: FileText },
      { label: "文章分类", href: "/article-categories", icon: FolderTree },
      { label: "视频管理", href: "/videos", icon: Clapperboard },
      { label: "视频分类", href: "/video-categories", icon: Tags },
    ],
  },
]);

export const NAVIGATION_ITEMS = Object.freeze(
  NAVIGATION_GROUPS.flatMap((group) => group.items),
);
