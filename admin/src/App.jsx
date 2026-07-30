import { Provider } from "react-redux";
import { RouterProvider } from "react-router-dom";
import { ThemeProvider } from "@/components/ThemeProvider.jsx";
import { SessionLoading } from "@/components/SessionLoading.jsx";
import { TooltipProvider } from "@/components/ui/tooltip.jsx";
import { router } from "@/router/index.jsx";
import { store } from "@/store/index.js";

function App() {
  return (
    <Provider store={store}>
      <ThemeProvider>
        <TooltipProvider delayDuration={300}>
          <RouterProvider
            router={router}
            fallbackElement={<SessionLoading />}
          />
        </TooltipProvider>
      </ThemeProvider>
    </Provider>
  );
}

export default App;
