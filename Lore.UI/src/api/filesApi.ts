import { ApiError } from "./chatApi";
import type {
    FileCatalogParams,
    FileCatalogResponse,
    FileSource,
} from "./filesTypes";

export async function fetchFiles(params: FileCatalogParams): Promise<FileCatalogResponse> {
    const searchParams = new URLSearchParams({
        page: String(params.page),
        pageSize: String(params.pageSize),
        sortBy: params.sortBy,
        sortDirection: params.sortDirection,
    });

    if (params.search) searchParams.set("search", params.search);
    if (params.status) searchParams.set("status", params.status);
    if (params.extension) searchParams.set("extension", params.extension);
    if (params.category) searchParams.set("category", params.category);
    if (params.documentType) searchParams.set("documentType", params.documentType);

    const response = await fetch(`/api/files?${searchParams}`);
    if (!response.ok) {
        const detail = await response.text();
        throw new ApiError(detail || `Request failed (${response.status})`, response.status);
    }

    return response.json() as Promise<FileCatalogResponse>;
}

async function checkResponse(response: Response): Promise<void> {
    if (response.ok) return;
    const detail = await response.text();
    throw new ApiError(detail || `Request failed (${response.status})`, response.status);
}

export async function fetchFileSources(): Promise<FileSource[]> {
    const response = await fetch("/api/files/sources");
    await checkResponse(response);
    return response.json() as Promise<FileSource[]>;
}

export async function addFileSource(path: string, excludeExtensions: string): Promise<FileSource> {
    const response = await fetch("/api/files/sources", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ path, excludeExtensions: excludeExtensions || null }),
    });
    await checkResponse(response);
    return response.json() as Promise<FileSource>;
}

export async function updateFileSource(id: number, excludeExtensions: string): Promise<FileSource> {
    const response = await fetch(`/api/files/sources/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ excludeExtensions: excludeExtensions || null }),
    });
    await checkResponse(response);
    return response.json() as Promise<FileSource>;
}

export async function deleteFileSource(id: number): Promise<void> {
    const response = await fetch(`/api/files/sources/${id}`, { method: "DELETE" });
    await checkResponse(response);
}
