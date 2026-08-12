import type { LoreChatRequest } from "./types";

export type StreamChatOptions = {
    request: LoreChatRequest;
    onChunk: (chunkText: string) => void;
};

export async function streamChat({ request, onChunk }: StreamChatOptions): Promise<string> {
    const response = await fetch('/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
    });

    if (!response.ok || !response.body) {
        throw new Error(`Stream request failed: ${response.statusText}`);
    }

    const chatId = response.headers.get('x-chat-id');
    const reader = response.body.getReader();
    const decoder = new TextDecoder('utf-8');

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        // Decode binary chunk to plain text string
        const chunkText = decoder.decode(value, { stream: true });
        onChunk(chunkText);
    }

    return chatId!;
}

export async function postChat(chatRequest: LoreChatRequest): Promise<string> {
    // do the fetching
    return `echoing back ${chatRequest.prompt}`;
}