import {
  Clapperboard,
  FileText,
  FileAudio,
  FolderTree,
  LayoutDashboard,
  Languages,
  ListChecks,
  Settings,
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
      { label: "单词管理", href: "/words", icon: Languages },
      { label: "音频资源", href: "/audio", icon: FileAudio },
      { label: "试卷管理", href: "/papers", icon: ListChecks },
    ],
  },
  {
    label: "设置",
    items: [
      { label: "系统设置", href: "/settings", icon: Settings },
    ],
  },
]);

export const NAVIGATION_ITEMS = Object.freeze(
  NAVIGATION_GROUPS.flatMap((group) => group.items),
);
