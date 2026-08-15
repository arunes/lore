import { ApiError } from "./chatApi";
import type {
    SettingsPreset,
    SettingsRequest,
    SettingsResponse,
} from "./settingsTypes";

async function throwError(response: Response): Promise<never> {
    const text = await response.text();
    let problem: {
        detail?: string;
        title?: string;
        status?: number;
        traceId?: string;
    } = {};

    try {
        problem = text ? JSON.parse(text) : {};
    } catch {
        problem = { detail: text };
    }

    throw new ApiError(
        problem.detail ||
            problem.title ||
            `Request failed (${response.status} ${response.statusText})`,
        problem.status ?? response.status,
        problem.traceId ??
            response.headers.get("x-correlation-id") ??
            undefined
    );
}

export async function fetchSettings(): Promise<SettingsResponse> {
    const response = await fetch("/api/settings");

    if (!response.ok) {
        await throwError(response);
    }

    return response.json() as Promise<SettingsResponse>;
}

export async function saveSettings(request: SettingsRequest): Promise<void> {
    const response = await fetch("/api/settings", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        await throwError(response);
    }
}

export async function fetchSettingsPresets(): Promise<SettingsPreset[]> {
    const response = await fetch("/api/settings/presets");

    if (!response.ok) {
        await throwError(response);
    }

    return response.json() as Promise<SettingsPreset[]>;
}
