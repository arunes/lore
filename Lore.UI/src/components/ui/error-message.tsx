import { AlertCircleIcon } from "@hugeicons/core-free-icons";
import { Icon } from "./icon";
import { cn } from "@/lib/utils";

export function ErrorMessage({
    title = "Something went wrong",
    message,
    traceId,
    className,
    action,
}: {
    title?: string;
    message?: string;
    traceId?: string;
    className?: string;
    action?: React.ReactNode;
}) {
    return (
        <div
            role="alert"
            className={cn(
                "flex w-full items-start gap-3 rounded-xl border border-destructive/30 bg-destructive/10 px-3.5 py-3 text-sm",
                className
            )}
        >
            <Icon
                icon={AlertCircleIcon}
                size={18}
                className="mt-0.5 shrink-0 text-destructive"
            />
            <div className="min-w-0 flex-1">
                <p className="font-medium text-destructive">{title}</p>
                {message && (
                    <p className="mt-0.5 text-foreground/90">{message}</p>
                )}
                {traceId && (
                    <p className="mt-1 text-xs text-muted-foreground">
                        Reference: <span className="font-mono">{traceId}</span>
                    </p>
                )}
            </div>
            {action && <div className="shrink-0">{action}</div>}
        </div>
    );
}
