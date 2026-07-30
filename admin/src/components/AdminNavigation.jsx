import { NavLink } from "react-router-dom";
import { cn } from "@/lib/utils.js";
import { NAVIGATION_ITEMS } from "@/router/navigation.js";

/** Renders the shared admin navigation for desktop and mobile containers. */
export function AdminNavigation({ onNavigate }) {
  return (
    <nav aria-label="主导航" className="flex flex-col gap-1">
      {NAVIGATION_ITEMS.map((item) => {
        const Icon = item.icon;
        return (
          <NavLink
            key={item.href}
            to={item.href}
            end
            onClick={onNavigate}
            className={({ isActive }) =>
              cn(
                "flex h-9 items-center gap-3 rounded-lg px-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sidebar-ring",
                isActive
                  ? "bg-sidebar-primary text-sidebar-primary-foreground"
                  : "text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground",
              )
            }
          >
            <Icon aria-hidden="true" className="size-4" />
            <span className="min-w-0 truncate">{item.label}</span>
          </NavLink>
        );
      })}
    </nav>
  );
}
