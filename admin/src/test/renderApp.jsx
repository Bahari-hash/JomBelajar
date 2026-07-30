import { render } from "@testing-library/react";
import { Provider } from "react-redux";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { ThemeProvider } from "@/components/ThemeProvider.jsx";
import { SessionLoading } from "@/components/SessionLoading.jsx";
import { TooltipProvider } from "@/components/ui/tooltip.jsx";
import { routes } from "@/router/index.jsx";
import { AUTH_STATUS } from "@/store/authSlice.js";
import { createAppStore } from "@/store/index.js";

export const TEST_ADMIN = Object.freeze({
  id: "11111111-1111-1111-1111-111111111111",
  email: "admin@example.test",
  role: "Admin",
});

/** Renders the production route tree with application providers at a chosen URL. */
export function renderAppAt(pathname, options = {}) {
  const appStore =
    options.store ??
    createAppStore({
      auth: options.auth ?? {
        status: AUTH_STATUS.AUTHENTICATED,
        user: TEST_ADMIN,
        message: null,
      },
    });
  const router = createMemoryRouter(routes, {
    initialEntries: [options.initialEntry ?? pathname],
  });

  return {
    router,
    store: appStore,
    ...render(
      <Provider store={appStore}>
        <ThemeProvider>
          <TooltipProvider delayDuration={0}>
            <RouterProvider
              router={router}
              fallbackElement={<SessionLoading />}
            />
          </TooltipProvider>
        </ThemeProvider>
      </Provider>,
    ),
  };
}
