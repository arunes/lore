import type { LoreChatRequest } from "./types";

export type StreamChatOptions = {
    request: LoreChatRequest;
    onChunk: (chunkText: string) => void;
};

export class ApiError extends Error {
    status?: number;
    traceId?: string;

    constructor(message: string, status?: number, traceId?: string) {
        super(message);
        this.name = "ApiError";
        this.status = status;
        this.traceId = traceId;
    }
}

type ProblemDetails = {
    type?: string;
    title?: string;
    status?: number;
    detail?: string;
    traceId?: string;
};

async function readProblemDetails(response: Response): Promise<ProblemDetails> {
    const text = await response.text();
    if (!text) {
        return {};
    }

    try {
        return JSON.parse(text) as ProblemDetails;
    } catch {
        return { detail: text };
    }
}

export async function streamChat({ request, onChunk }: StreamChatOptions): Promise<string> {
    const response = await fetch('/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        const problem = await readProblemDetails(response);
        const message =
            problem.detail ||
            problem.title ||
            `Request failed (${response.status} ${response.statusText})`;
        const traceId =
            problem.traceId ?? response.headers.get('x-correlation-id') ?? undefined;
        throw new ApiError(message, problem.status ?? response.status, traceId);
    }

    if (!response.body) {
        throw new ApiError('The server did not return a response body.');
    }

    const chatId = response.headers.get('x-chat-id');
    const reader = response.body.getReader();
    const decoder = new TextDecoder('utf-8');

    try {
        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            const chunkText = decoder.decode(value, { stream: true });
            onChunk(chunkText);
        }
    } catch (error) {
        if (error instanceof ApiError) {
            throw error;
        }
        throw new ApiError(
            'The connection to the server was interrupted while streaming the response.',
            undefined
        );
    }

    return chatId!;
}
