import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { Components } from "react-markdown";
import { cn } from "@/lib/utils";

const components: Components = {
    a({ href, children, ...props }) {
        return (
            <a
                href={href}
                target="_blank"
                rel="noopener noreferrer"
                {...props}
            >
                {children}
            </a>
        );
    },
    pre({ children, ...props }) {
        return (
            <pre
                className="my-2 max-w-full overflow-x-auto overflow-y-hidden whitespace-pre p-3"
                {...props}
            >
                {children}
            </pre>
        );
    },
};

export function Markdown({
    children,
    className = "text-xs/relaxed text-muted-foreground",
}: {
    children: string;
    className?: string;
}) {
    return (
        <div
            className={cn(
                className,
                "[&_a]:font-medium [&_a]:text-primary [&_a]:underline [&_a]:underline-offset-2 hover:[&_a]:text-primary/80 [&_code]:rounded [&_code]:bg-muted [&_code]:px-1 [&_code]:py-0.5 [&_code]:font-mono [&_p]:my-0 [&_p:not(:last-child)]:mb-2 [&_ul]:my-0 [&_ul]:list-disc [&_ul]:pl-4 [&_ol]:my-0 [&_ol]:list-decimal [&_ol]:pl-4"
            )}
        >
            <ReactMarkdown remarkPlugins={[remarkGfm]} components={components}>
                {children}
            </ReactMarkdown>
        </div>
    );
}
