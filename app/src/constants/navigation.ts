import {
  BookOpenText,
  Clapperboard,
  House,
  Languages,
  ListCheck,
  type LucideIcon,
} from "lucide-react";

export interface NavigationItem {
  label: string;
  description: string;
  to: string;
  icon: LucideIcon;
}

export const navigationItems: NavigationItem[] = [
  { label: "首页", description: "学习入口", to: "/", icon: House },
  {
    label: "文章",
    description: "通过阅读积累表达与语感",
    to: "/articles",
    icon: BookOpenText,
  },
  {
    label: "视频",
    description: "结合画面训练真实语境听力",
    to: "/videos",
    icon: Clapperboard,
  },
  {
    label: "单词",
    description: "按节奏巩固词汇与发音",
    to: "/words",
    icon: Languages,
  },
  {
    label: "在线测试",
    description: "用习题检查当前学习成果",
    to: "/papers",
    icon: ListCheck,
  },
];

export const moduleItems = navigationItems.slice(1);
