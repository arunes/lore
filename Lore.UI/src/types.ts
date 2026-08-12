export type Role = 'user' | 'lore';

export interface Message {
  id: string;
  role: Role;
  content: string;
  timestamp: Date;
}

export interface UserSettings {
  apiKey: string;
  model: string;
  temperature: number;
  darkMode: boolean;
}