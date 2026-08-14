import { useEffect, useState } from "react";
import {
    Chat01Icon,
    Settings01Icon,
    Sun01Icon,
    Moon01Icon,
    SparklesIcon,
} from "@hugeicons/core-free-icons";
import { cn } from "@/lib/utils";
import { Icon } from "@/components/ui/icon";
import { Link, Outlet, useLocation } from "@tanstack/react-router";
import { Toaster } from "sonner";

const NAV_ITEMS = [
    { path: "/chat", label: "Chat", icon: Chat01Icon },
    { path: "/settings", label: "Settings", icon: Settings01Icon },
] as const;

function Logo() {
    return (
        <div className="flex items-center gap-2.5">
            <div
                className="flex size-9 items-center justify-center rounded-xl text-white shadow-sm"
                style={{ backgroundColor: "#863bff" }}
            >
                <Icon icon={SparklesIcon} size={18} />
            </div>
            <span className="text-sm font-semibold tracking-tight">Lore</span>
        </div>
    );
}

export function AppShell() {
    const [dark, setDark] = useState(false);
    const location = useLocation();
    const currentPath = location.pathname;

    useEffect(() => {
        document.documentElement.classList.toggle("dark", dark);
    }, [dark]);

    return (
        <div className="flex h-dvh w-full overflow-hidden bg-background text-foreground">
            <aside className="flex h-full w-56 shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground">
                <div className="flex h-14 items-center px-4">
                    <Logo />
                </div>

                <nav className="flex flex-col gap-1 px-2 py-2">
                    {NAV_ITEMS.map(({ path, label, icon }) => {
                        const isActive = currentPath.startsWith(path);
                        return (
                            <Link
                                key={path}
                                to={path}
                                className={cn(
                                    "flex h-9 items-center gap-2.5 rounded-lg px-2.5 text-sm font-medium transition-colors",
                                    isActive
                                        ? "bg-sidebar-accent text-sidebar-accent-foreground"
                                        : "text-sidebar-foreground/70 hover:bg-sidebar-accent/50 hover:text-sidebar-foreground"
                                )}
                            >
                                <Icon icon={icon} size={18} />
                                {label}
                            </Link>
                        );
                    })}
                </nav>
            </aside>

            <div className="flex flex-1 flex-col overflow-hidden">
                <header className="flex h-14 shrink-0 items-center justify-between border-b border-border px-4">
                    <p className="text-sm font-medium text-muted-foreground">
                        {NAV_ITEMS.find((item) => currentPath.startsWith(item.path))?.label ?? "Lore"}
                    </p>
                    <button
                        type="button"
                        onClick={() => setDark((prev) => !prev)}
                        aria-label="Toggle theme"
                        className="flex size-8 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                    >
                        {dark ? <Icon icon={Sun01Icon} size={18} /> : <Icon icon={Moon01Icon} size={18} />}
                    </button>
                </header>

                <main className="min-h-0 flex-1 overflow-hidden">
                    <Outlet />
                </main>
            </div>
            <Toaster position="bottom-right" richColors />
        </div>
    );
}
