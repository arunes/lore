import { createContext, useContext, useState, type ReactNode } from "react";
import type { Message } from "@/components/Chat";

type ChatContextType = {
    messages: Message[];
    setMessages: React.Dispatch<React.SetStateAction<Message[]>>;
    activeChatId: string | null;
    setActiveChatId: React.Dispatch<React.SetStateAction<string | null>>;
};

const ChatContext = createContext<ChatContextType | null>(null);

export function ChatProvider({ children }: { children: ReactNode }) {
    const [messages, setMessages] = useState<Message[]>([]);
    const [activeChatId, setActiveChatId] = useState<string | null>(null);

    return (
        <ChatContext.Provider value={{ messages, setMessages, activeChatId, setActiveChatId }}>
            {children}
        </ChatContext.Provider>
    );
}

export function useChat() {
    const ctx = useContext(ChatContext);
    if (!ctx) {
        throw new Error("useChat must be used within a ChatProvider");
    }
    return ctx;
}