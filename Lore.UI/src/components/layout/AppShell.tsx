import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import {
    Chat01Icon,
    Settings01Icon,
    Sun01Icon,
    Moon01Icon,
    SparklesIcon,
} from "@hugeicons/core-free-icons";
import type { IconSvgElement } from "@hugeicons/react";
import { cn } from "@/lib/utils";
import { Icon } from "@/components/ui/icon";
import { Chat } from "@/components/Chat";
import { Settings } from "@/components/Settings";

type View = "chat" | "settings";

const NAV_ITEMS: { id: View; label: string; icon: IconSvgElement }[] = [
    { id: "chat", label: "Chat", icon: Chat01Icon },
    { id: "settings", label: "Settings", icon: Settings01Icon },
];

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
    const [view, setView] = useState<View>("chat");
    const [dark, setDark] = useState(false);

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
                    {NAV_ITEMS.map(({ id, label, icon }) => (
                        <button
                            key={id}
                            type="button"
                            onClick={() => setView(id)}
                            className={cn(
                                "flex h-9 items-center gap-2.5 rounded-lg px-2.5 text-sm font-medium transition-colors",
                                view === id
                                    ? "bg-sidebar-accent text-sidebar-accent-foreground"
                                    : "text-sidebar-foreground/70 hover:bg-sidebar-accent/50 hover:text-sidebar-foreground"
                            )}
                        >
                            <Icon icon={icon} size={18} />
                            {label}
                        </button>
                    ))}
                </nav>
            </aside>

            <div className="flex flex-1 flex-col overflow-hidden">
                <header className="flex h-14 shrink-0 items-center justify-between border-b border-border px-4">
                    <p className="text-sm font-medium text-muted-foreground">
                        {NAV_ITEMS.find((item) => item.id === view)?.label}
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
                    <ViewSwitcher view={view} />
                </main>
            </div>
        </div>
    );
}

function ViewSwitcher({ view }: { view: View }) {
    const pages: Record<View, ReactNode> = {
        chat: <Chat />,
        settings: <Settings />,
    };
    return <>{pages[view]}</>;
}
