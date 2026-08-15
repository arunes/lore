import { ApiError } from "./chatApi";
import type { FileCatalogParams, FileCatalogResponse } from "./filesTypes";

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
