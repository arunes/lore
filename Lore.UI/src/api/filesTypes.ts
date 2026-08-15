export type FileProcessStatus =
    | "Pending"
    | "TextExtracted"
    | "Classified"
    | "ChunksCreated"
    | "Done"
    | "NotSupportedFile"
    | "EmptyContent"
    | "TextExtractionFailed"
    | "ClassificationFailed"
    | "VectorizationFailed";

export type FileCatalogItem = {
    id: number;
    name: string;
    path: string;
    directory: string;
    extension: string;
    size: number;
    fileCreatedAt: string;
    fileModifiedAt: string;
    processStatus: FileProcessStatus;
    category: string | null;
    documentType: string | null;
};

export type FileCatalogResponse = {
    items: FileCatalogItem[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    statusCounts: Record<FileProcessStatus, number>;
};

export type FileCatalogParams = {
    page: number;
    pageSize: number;
    search: string;
    status: string;
    extension: string;
    category: string;
    documentType: string;
    sortBy: string;
    sortDirection: "asc" | "desc";
};

export type FileSource = {
    id: number;
    path: string;
    excludeExtensions: string | null;
    isEnabled: boolean;
};
