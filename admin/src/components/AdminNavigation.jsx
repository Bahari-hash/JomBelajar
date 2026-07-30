import { NavLink } from "react-router-dom";
import { cn } from "@/lib/utils.js";
import { NAVIGATION_GROUPS } from "@/router/navigation.js";

/** Renders the shared admin navigation for desktop and mobile containers. */
export function AdminNavigation({ onNavigate }) {
  return (
    <nav aria-label="主导航" className="flex flex-col gap-4">
      {NAVIGATION_GROUPS.map((group, groupIndex) => (
        <div key={group.label ?? groupIndex} className="space-y-1">
          {group.label ? (
            <p className="px-3 pb-1 text-xs font-medium text-muted-foreground">
              {group.label}
            </p>
          ) : null}
          {group.items.map((item) => {
            const Icon = item.icon;
            return (
              <NavLink
                key={item.href}
                to={item.href}
                end={item.href === "/"}
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
        </div>
      ))}
    </nav>
  );
}
