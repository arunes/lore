import { createRouter, createRootRoute, createRoute, redirect } from "@tanstack/react-router";
import { Chat } from "./components/Chat";
import { Settings } from "./components/Settings";
import { AppShell } from "./components/layout/AppShell";
import { MyFiles } from "./components/MyFiles";

const rootRoute = createRootRoute({
    component: () => <AppShell />,
});

const chatRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/chat",
    component: () => <Chat />,
});

const settingsRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/settings",
    component: () => <Settings />,
});

const filesRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/files",
    component: () => <MyFiles />,
});

const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: "/",
    beforeLoad: () => {
        throw redirect({ to: "/chat" });
    },
    component: () => null,
});

const routeTree = rootRoute.addChildren([chatRoute, filesRoute, settingsRoute, indexRoute]);

export const router = createRouter({ routeTree });

declare module "@tanstack/react-router" {
    interface Register {
        router: typeof router;
    }
}
