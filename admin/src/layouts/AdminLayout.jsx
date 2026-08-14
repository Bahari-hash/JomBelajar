import { useState } from "react";
import { Menu } from "lucide-react";
import { Outlet, useLocation } from "react-router-dom";
import { AccountMenu } from "@/components/AccountMenu.jsx";
import { AdminNavigation } from "@/components/AdminNavigation.jsx";
import { ThemeMenu } from "@/components/ThemeMenu.jsx";
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
} from "@/components/ui/breadcrumb.jsx";
import { Button } from "@/components/ui/button.jsx";
import { Separator } from "@/components/ui/separator.jsx";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from "@/components/ui/sheet.jsx";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip.jsx";
import { NAVIGATION_ITEMS } from "@/router/navigation.js";
import { AdminPageContext } from "@/lib/adminPageContext.js";

function Brand() {
  return (
    <div className="flex min-w-0 items-center gap-3">
      <img
        src="/logo.png"
        alt=""
        className="size-8 shrink-0 rounded-lg object-contain"
      />
      <span className="min-w-0">
        <span className="block truncate text-sm font-semibold">TinyLang</span>
        <span className="block truncate text-xs text-muted-foreground">
          管理后台
        </span>
      </span>
    </div>
  );
}

/** Responsive administrator shell with a shared desktop and mobile navigation source. */
function AdminLayout() {
  const [mobileNavigationOpen, setMobileNavigationOpen] = useState(false);
  const [pageLabel, setPageLabel] = useState(null);
  const location = useLocation();
  const navigationPage = NAVIGATION_ITEMS.find((item) =>
    item.href === "/"
      ? location.pathname === "/"
      : location.pathname.startsWith(item.href),
  )?.label;
  const currentPage = pageLabel ?? navigationPage ?? "页面未找到";

  const handleMobileNavigate = () => setMobileNavigationOpen(false);

  return (
    <div className="min-h-dvh lg:grid lg:grid-cols-[15rem_minmax(0,1fr)]">
      <aside className="sticky top-0 hidden h-dvh flex-col border-r border-sidebar-border bg-sidebar p-4 text-sidebar-foreground lg:flex">
        <Brand />
        <Separator className="my-4 bg-sidebar-border" />
        <AdminNavigation />
      </aside>

      <div className="min-w-0 max-w-full overflow-x-hidden">
        <header className="sticky top-0 z-30 flex h-14 items-center gap-2 border-b bg-background/95 px-3 backdrop-blur-sm sm:px-4">
          <Sheet
            open={mobileNavigationOpen}
            onOpenChange={setMobileNavigationOpen}
          >
            <Tooltip>
              <TooltipTrigger asChild>
                <SheetTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="lg:hidden"
                    aria-label="打开导航菜单"
                  >
                    <Menu aria-hidden="true" />
                  </Button>
                </SheetTrigger>
              </TooltipTrigger>
              <TooltipContent>打开导航菜单</TooltipContent>
            </Tooltip>
            <SheetContent
              side="left"
              className="w-[min(20rem,85vw)] gap-0 bg-sidebar p-0"
            >
              <SheetHeader className="border-b border-sidebar-border p-4 text-left">
                <SheetTitle asChild>
                  <Brand />
                </SheetTitle>
                <SheetDescription className="sr-only">
                  后台主导航
                </SheetDescription>
              </SheetHeader>
              <div className="p-4">
                <AdminNavigation onNavigate={handleMobileNavigate} />
              </div>
            </SheetContent>
          </Sheet>

          <Breadcrumb className="min-w-0 flex-1">
            <BreadcrumbList className="flex-nowrap">
              <BreadcrumbItem className="min-w-0">
                <BreadcrumbPage className="truncate">
                  {currentPage}
                </BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>

          <div className="flex shrink-0 items-center gap-1">
            <ThemeMenu />
            <AccountMenu />
          </div>
        </header>

        <AdminPageContext.Provider value={setPageLabel}>
          <main className="mx-auto w-full max-w-[min(80rem,100vw)] p-4 sm:p-6 lg:p-8">
            <Outlet />
          </main>
        </AdminPageContext.Provider>
      </div>
    </div>
  );
}

export default AdminLayout;
