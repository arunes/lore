import { useMutation } from "@tanstack/react-query";
import { Card } from "./ui/card";
import { postChat, streamChat } from "@/api/chatApi";
import type { LoreChatRequest } from "@/api/types";
import { useState } from "react";
import { ScrollArea } from "./ui/scroll-area";


async function sendMessage(message: string) {
    const request: LoreChatRequest = {
        prompt: message
    };

    return postChat(request);
}

export function Chat() {
    const [activeChatId, setActiveChatId] = useState<string | null>(null);
    const [messages, setMessages] = useState<Array<{ role: string; content: string }>>([]);
    const [input, setInput] = useState('');
    const [isStreaming, setIsStreaming] = useState(false);

    const handleSend = async () => {
        if (!input.trim() || isStreaming) return;

        const userText = input;
        setInput('');
        setIsStreaming(true);

        // Add user message & create empty placeholder for streaming assistant response
        setMessages((prev) => [
            ...prev,
            { role: 'user', content: userText },
            { role: 'assistant', content: '' },
        ]);

        try {
            const newChatId = await streamChat({
                request: {
                    chatId: activeChatId ?? undefined,
                    prompt: userText,
                },

                onChunk: (chunk) => {
                    setMessages((prev) => {
                        const updated = [...prev];
                        const lastIndex = updated.length - 1;

                        updated[lastIndex] = {
                            ...updated[lastIndex],
                            content: updated[lastIndex].content + chunk,
                        };
                        return updated;
                    });
                },
            });

            if (newChatId && !activeChatId) {
                setActiveChatId(newChatId);
            }
        } catch (error) {
            console.error('Streaming error:', error);
        } finally {
            setIsStreaming(false);
        }
    };

    return (
        <Card className="flex flex-col h-[600px] w-full max-w-2xl mx-auto p-4">
            <div className="text-xs text-muted-foreground">
                Thread ID: {activeChatId ?? 'New Session'}
            </div>

            <ScrollArea className="space-y-3 border p-4 rounded-lg min-h-[350px]">
                {messages.map((m, idx) => (
                    <div
                        key={idx}
                        className={`p-2 rounded ${m.role === 'user'
                            ? 'bg-primary text-primary-foreground text-right ml-auto max-w-[80%]'
                            : 'bg-muted text-left mr-auto max-w-[80%]'
                            }`}
                    >
                        {m.content}
                    </div>
                ))}
            </ScrollArea>

            <div className="flex gap-2">
                <input
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                    placeholder="Type message..."
                    className="border p-2 rounded flex-1"
                    disabled={isStreaming}
                />
                <button
                    onClick={handleSend}
                    disabled={isStreaming}
                    className="bg-primary text-primary-foreground px-4 py-2 rounded"
                >
                    {isStreaming ? 'Streaming...' : 'Send'}
                </button>
            </div>
        </Card>
    );
}