import { Card } from "./ui/card";
import { SlidersHorizontalIcon } from "@hugeicons/core-free-icons";
import { Icon } from "./ui/icon";

export function Settings() {
    return (
        <div className="flex h-full items-center justify-center p-4">
            <Card className="flex w-full max-w-md flex-col items-center gap-3 p-10 text-center">
                <div className="flex size-11 items-center justify-center rounded-xl bg-muted text-muted-foreground">
                    <Icon icon={SlidersHorizontalIcon} size={20} />
                </div>
                <div>
                    <h2 className="text-base font-semibold tracking-tight">Settings</h2>
                    <p className="mt-1 text-sm text-muted-foreground">
                        Configuration will live here soon.
                    </p>
                </div>
            </Card>
        </div>
    );
}
