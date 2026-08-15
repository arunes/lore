import { useEffect, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { resetChat, streamChat, ApiError } from "@/api/chatApi";
import { toast } from "sonner";
import {
    SparklesIcon,
    UserIcon,
    ArrowUpRight01Icon,
} from "@hugeicons/core-free-icons";
import { ScrollArea } from "./ui/scroll-area";
import { Button } from "./ui/button";
import { Textarea } from "./ui/textarea";
import { Icon } from "./ui/icon";
import { ErrorMessage } from "./ui/error-message";
import { Markdown } from "./ui/markdown";
import { cn } from "@/lib/utils";
import { useChat } from "@/chat/ChatContext";

export type Message = {
    id: string;
    role: string;
    content: string;
    prompt?: string;
    error?: { message?: string; traceId?: string };
};

const SUGGESTIONS = [
    "What documents are in my library?",
    "Summarize my latest notes",
    "Find files about project planning",
];

function toApiError(error: unknown): ApiError {
    if (error instanceof ApiError) {
        return error;
    }
    const message =
        error instanceof Error ? error.message : "An unexpected error occurred.";
    return new ApiError(message);
}

export function Chat() {
    const { messages, setMessages, activeChatId, setActiveChatId } = useChat();
    const [input, setInput] = useState("");
    const [isStreaming, setIsStreaming] = useState(false);
    const viewportRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const viewport = viewportRef.current;
        if (!viewport) return;
        const nearBottom =
            viewport.scrollHeight - viewport.scrollTop - viewport.clientHeight < 72;
        if (nearBottom) {
            viewport.scrollTop = viewport.scrollHeight;
        }
    }, [messages, isStreaming]);

    const streamMutation = useMutation({
        mutationFn: async ({
            prompt,
            messageId,
        }: {
            prompt: string;
            messageId: string;
        }) => {
            let accumulated = "";
            const newChatId = await streamChat({
                request: {
                    chatId: activeChatId ?? undefined,
                    prompt,
                },
                onChunk: (chunk) => {
                    accumulated += chunk;
                    setMessages((prev) =>
                        prev.map((m) =>
                            m.id === messageId ? { ...m, content: accumulated } : m
                        )
                    );
                },
            });
            return newChatId;
        },
    });

    const resetMutation = useMutation({
        mutationFn: resetChat,
    });

    const runStream = async (prompt: string, messageId: string) => {
        setIsStreaming(true);
        try {
            const newChatId = await streamMutation.mutateAsync({ prompt, messageId });
            if (newChatId && !activeChatId) {
                setActiveChatId(newChatId);
            }
        } catch (error) {
            const apiError = toApiError(error);
            setMessages((prev) =>
                prev.map((m) =>
                    m.id === messageId
                        ? {
                              ...m,
                              content: "",
                              error: {
                                  message: apiError.message,
                                  traceId: apiError.traceId,
                              },
                          }
                        : m
                )
            );
            setInput(prompt);
        } finally {
            setIsStreaming(false);
        }
    };

    const handleSend = async () => {
        const userText = input.trim();
        if (!userText || isStreaming) return;

        setInput("");
        const messageId = crypto.randomUUID();
        setMessages((prev) => [
            ...prev,
            { id: crypto.randomUUID(), role: "user", content: userText },
            { id: messageId, role: "assistant", content: "", prompt: userText },
        ]);

        await runStream(userText, messageId);
    };

    const handleRetry = (message: Message) => {
        if (!message.prompt || isStreaming) return;
        setMessages((prev) =>
            prev.map((m) =>
                m.id === message.id ? { ...m, content: "", error: undefined } : m
            )
        );
        runStream(message.prompt, message.id);
    };

    const handleNewChat = async () => {
        if (isStreaming || resetMutation.isPending) return;

        try {
            await resetMutation.mutateAsync();
            setMessages([]);
            setActiveChatId(null);
            setInput("");
        } catch (error) {
            const apiError = toApiError(error);
            toast.error("Couldn't start a new chat", { description: apiError.message });
        }
    };

    const isEmpty = messages.length === 0;

    return (
        <div className="flex h-full flex-col">
            <header className="shrink-0 px-4 py-6 sm:px-6">
                <div className="mx-auto flex w-full max-w-7xl flex-wrap items-end justify-between gap-3">
                    <div>
                        <h1 className="text-xl font-semibold tracking-tight">Chat</h1>
                        <p className="mt-1 text-sm text-muted-foreground">
                            Ask questions and find answers in your indexed documents.
                        </p>
                    </div>
                    <Button
                        type="button"
                        onClick={handleNewChat}
                        disabled={isStreaming || resetMutation.isPending}
                    >
                        {resetMutation.isPending ? "Starting..." : "New Chat"}
                    </Button>
                </div>
            </header>
            {isEmpty ? (
                <div className="flex flex-1 flex-col items-center justify-center gap-8 px-6">
                    <div className="flex flex-col items-center gap-4 text-center">
                        <div
                            className="flex size-14 items-center justify-center rounded-2xl text-white shadow-md"
                            style={{ backgroundColor: "#863bff" }}
                        >
                            <Icon icon={SparklesIcon} size={28} />
                        </div>
                        <div>
                            <h1 className="text-xl font-semibold tracking-tight">
                                Ask your documents
                            </h1>
                            <p className="mt-1 text-sm text-muted-foreground">
                                Lore searches your indexed files and answers with sources.
                            </p>
                        </div>
                    </div>

                    <div className="flex flex-wrap justify-center gap-2">
                        {SUGGESTIONS.map((suggestion) => (
                            <Button
                                key={suggestion}
                                variant="outline"
                                size="lg"
                                onClick={() => {
                                    setInput(suggestion);
                                }}
                            >
                                {suggestion}
                            </Button>
                        ))}
                    </div>
                </div>
            ) : (
                <ScrollArea
                    viewportRef={viewportRef}
                    className="min-h-0 flex-1 px-4 py-6"
                >
                    <div className="mx-auto flex w-full max-w-3xl flex-col gap-4">
                        {messages.map((m, idx) => {
                            const isUser = m.role === "user";
                            const isStreamingMessage =
                                m.role === "assistant" &&
                                isStreaming &&
                                idx === messages.length - 1;

                            if (m.error) {
                                return (
                                    <div key={m.id} className="mx-auto w-full max-w-[75%]">
                                        <ErrorMessage
                                            title="Couldn't get a response"
                                            message={m.error.message}
                                            traceId={m.error.traceId}
                                            action={
                                                <Button
                                                    size="sm"
                                                    variant="outline"
                                                    onClick={() => handleRetry(m)}
                                                >
                                                    Retry
                                                </Button>
                                            }
                                        />
                                    </div>
                                );
                            }

                            return (
                                <div
                                    key={m.id}
                                    className={cn(
                                        "flex items-start gap-3",
                                        isUser && "flex-row-reverse"
                                    )}
                                >
                                    <div
                                        className={cn(
                                            "flex size-8 shrink-0 items-center justify-center rounded-full",
                                            isUser
                                                ? "bg-primary text-primary-foreground"
                                                : "bg-muted text-muted-foreground"
                                        )}
                                    >
                                        {isUser ? (
                                            <Icon icon={UserIcon} size={16} />
                                        ) : (
                                            <Icon icon={SparklesIcon} size={16} />
                                        )}
                                    </div>
                                    <div
                                        className={cn(
                                            "max-w-[75%] rounded-xl px-3.5 py-2.5 text-sm leading-relaxed",
                                            isUser
                                                ? "bg-primary text-primary-foreground"
                                                : "bg-muted text-foreground"
                                        )}
                                    >
                                        {isUser ? (
                                            <span className="whitespace-pre-wrap">
                                                {m.content}
                                            </span>
                                        ) : m.content ? (
                                            isStreamingMessage ? (
                                                <span className="whitespace-pre-wrap">
                                                    {m.content}
                                                </span>
                                            ) : (
                                                <Markdown className="text-sm/relaxed leading-relaxed text-foreground [&_code]:bg-foreground/10 [&_p:not(:last-child)]:mb-3">
                                                    {m.content}
                                                </Markdown>
                                            )
                                        ) : (
                                            isStreamingMessage && (
                                                <span className="inline-flex gap-1">
                                                    <span className="size-1.5 animate-bounce rounded-full bg-current" />
                                                    <span className="size-1.5 animate-bounce rounded-full bg-current [animation-delay:120ms]" />
                                                    <span className="size-1.5 animate-bounce rounded-full bg-current [animation-delay:240ms]" />
                                                </span>
                                            )
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </ScrollArea>
            )}

            <div className="shrink-0 border-t border-border bg-background/80 px-4 py-4 backdrop-blur">
                <div className="mx-auto w-full max-w-3xl">
                    <div className="flex items-end gap-2 rounded-xl border border-input bg-card p-2 shadow-sm focus-within:border-ring focus-within:ring-2 focus-within:ring-ring/30">
                        <Textarea
                            value={input}
                            onChange={(e) => setInput(e.target.value)}
                            onKeyDown={(e) => {
                                if (e.key === "Enter" && !e.shiftKey) {
                                    e.preventDefault();
                                    handleSend();
                                }
                            }}
                            placeholder="Ask about your documents..."
                            className="min-h-9 flex-1 border-0 bg-transparent shadow-none focus-visible:ring-0"
                            rows={1}
                        />
                        <Button
                            onClick={handleSend}
                            disabled={!input.trim() || isStreaming}
                            size="icon-lg"
                            className="h-9 w-9 shrink-0 rounded-lg"
                            aria-label="Send message"
                        >
                            <Icon icon={ArrowUpRight01Icon} size={18} />
                        </Button>
                    </div>
                    <p className="mt-2 hidden text-xs text-muted-foreground sm:block">
                        Shift + Enter for a new line.
                    </p>
                </div>
            </div>
        </div>
    );
}
