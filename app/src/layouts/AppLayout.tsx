import { useEffect, useRef, useState } from "react";
import { Menu, X } from "lucide-react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import ThemeControl from "@/components/ThemeControl";
import AuthControls from "@/components/AuthControls";
import { navigationItems } from "@/constants/navigation";
import { cn } from "@/lib/utils";

function NavigationLinks({ onNavigate }: { onNavigate?: () => void }) {
  return navigationItems.map((item) => {
    const Icon = item.icon;
    return (
      <NavLink
        key={item.to}
        to={item.to}
        end={item.to === "/"}
        onClick={onNavigate}
        className={({ isActive }) =>
          cn(
            "flex min-h-10 items-center gap-2 rounded-md px-3 text-sm font-medium transition-colors",
            isActive
              ? "bg-primary text-primary-content"
              : "text-base-content/75 hover:bg-base-200 hover:text-base-content",
          )
        }
      >
        <Icon aria-hidden="true" className="size-4" />
        {item.label}
      </NavLink>
    );
  });
}

/** Shared responsive shell for every public TinyLang route. */
export default function AppLayout() {
  const [menuOpen, setMenuOpen] = useState(false);
  const menuButtonRef = useRef<HTMLButtonElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const menuPanelRef = useRef<HTMLDivElement>(null);
  const location = useLocation();

  useEffect(() => {
    setMenuOpen(false);
  }, [location.key]);

  useEffect(() => {
    if (!menuOpen) {
      return;
    }

    closeButtonRef.current?.focus();
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMenuOpen(false);
        menuButtonRef.current?.focus();
        return;
      }

      if (event.key === "Tab") {
        const focusableElements = Array.from(
          menuPanelRef.current?.querySelectorAll<HTMLElement>(
            'a[href], button:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])',
          ) ?? [],
        );
        const firstElement = focusableElements[0];
        const lastElement = focusableElements.at(-1);

        if (event.shiftKey && document.activeElement === firstElement) {
          event.preventDefault();
          lastElement?.focus();
        } else if (!event.shiftKey && document.activeElement === lastElement) {
          event.preventDefault();
          firstElement?.focus();
        }
      }
    };

    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.body.style.overflow = previousOverflow;
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [menuOpen]);

  const closeMenu = () => {
    setMenuOpen(false);
    menuButtonRef.current?.focus();
  };

  return (
    <div className="flex min-h-dvh flex-col bg-base-100 text-base-content">
      <header className="sticky top-0 z-30 border-b border-base-300 bg-base-100/95 backdrop-blur">
        <div className="mx-auto flex h-16 w-full max-w-7xl items-center gap-4 px-4 sm:px-6 lg:px-8">
          <NavLink
            aria-label="TinyLang 首页"
            className="flex shrink-0 items-center gap-2 text-lg font-bold"
            to="/"
          >
            <img
              src="/logo.png"
              alt=""
              className="size-9 shrink-0 rounded-md object-contain"
            />
            <span>TinyLang</span>
          </NavLink>

          <nav
            aria-label="主导航"
            className="ml-auto hidden items-center gap-1 md:flex"
          >
            <NavigationLinks />
          </nav>

          <div className="ml-auto hidden md:block">
            <ThemeControl />
          </div>
          <div className="hidden md:block">
            <AuthControls />
          </div>

          <button
            ref={menuButtonRef}
            aria-controls="mobile-navigation"
            aria-expanded={menuOpen}
            aria-label="打开导航菜单"
            className="btn btn-square btn-ghost ml-auto md:hidden"
            type="button"
            onClick={() => setMenuOpen(true)}
          >
            <Menu aria-hidden="true" className="size-5" />
          </button>
        </div>
      </header>

      {menuOpen ? (
        <div
          aria-label="移动导航"
          aria-modal="true"
          className="fixed inset-0 z-40 md:hidden"
          role="dialog"
        >
          <button
            aria-label="关闭导航菜单"
            className="absolute inset-0 bg-neutral/45"
            type="button"
            onClick={closeMenu}
          />
          <div
            ref={menuPanelRef}
            id="mobile-navigation"
            className="absolute inset-y-0 right-0 flex w-[min(20rem,88vw)] flex-col border-l border-base-300 bg-base-100 p-4 shadow-xl"
          >
            <div className="flex h-12 items-center justify-between">
              <span className="font-semibold">导航</span>
              <button
                ref={closeButtonRef}
                aria-label="关闭导航菜单"
                className="btn btn-square btn-ghost btn-sm"
                type="button"
                onClick={closeMenu}
              >
                <X aria-hidden="true" className="size-5" />
              </button>
            </div>
            <nav aria-label="移动端主导航" className="mt-4 flex flex-col gap-1">
              <NavigationLinks onNavigate={closeMenu} />
            </nav>
            <div className="mt-auto space-y-4 border-t border-base-300 pt-4">
              <AuthControls mobile onNavigate={closeMenu} />
              <ThemeControl />
            </div>
          </div>
        </div>
      ) : null}

      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 sm:py-10 lg:px-8">
        <Outlet />
      </main>

      <footer className="border-t border-base-300">
        <div className="mx-auto flex min-h-16 w-full max-w-7xl items-center px-4 text-sm text-base-content/65 sm:px-6 lg:px-8">
          <p>TinyLang 外语学习平台</p>
        </div>
      </footer>
    </div>
  );
}
