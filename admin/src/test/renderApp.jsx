import { render } from "@testing-library/react";
import { Provider } from "react-redux";
import { createMemoryRouter, RouterProvider } from "react-router-dom";
import { ThemeProvider } from "@/components/ThemeProvider.jsx";
import { TooltipProvider } from "@/components/ui/tooltip.jsx";
import { routes } from "@/router/index.jsx";
import { store } from "@/store/index.js";

/** Renders the production route tree with application providers at a chosen URL. */
export function renderAppAt(pathname) {
  const router = createMemoryRouter(routes, {
    initialEntries: [pathname],
  });

  return {
    router,
    ...render(
      <Provider store={store}>
        <ThemeProvider>
          <TooltipProvider delayDuration={0}>
            <RouterProvider router={router} />
          </TooltipProvider>
        </ThemeProvider>
      </Provider>,
    ),
  };
}
