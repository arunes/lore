import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { fetchFiles } from "@/api/filesApi";
import type { FileCatalogParams, FileProcessStatus } from "@/api/filesTypes";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "./ui/select";
import { ErrorMessage } from "./ui/error-message";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 25;

const STATUSES: { value: FileProcessStatus; label: string; group: "pipeline" | "attention" }[] = [
    { value: "Pending", label: "Pending", group: "pipeline" },
    { value: "TextExtracted", label: "Extracted", group: "pipeline" },
    { value: "Classified", label: "Classified", group: "pipeline" },
    { value: "ChunksCreated", label: "Chunked", group: "pipeline" },
    { value: "Done", label: "Done", group: "pipeline" },
    { value: "NotSupportedFile", label: "Unsupported", group: "attention" },
    { value: "EmptyContent", label: "Empty", group: "attention" },
    { value: "TextExtractionFailed", label: "Extraction failed", group: "attention" },
    { value: "ClassificationFailed", label: "Classification failed", group: "attention" },
    { value: "VectorizationFailed", label: "Vectorization failed", group: "attention" },
];

const STATUS_LABELS = Object.fromEntries(STATUSES.map((status) => [status.value, status.label])) as Record<
    FileProcessStatus,
    string
>;

const STATUS_STYLES: Record<FileProcessStatus, string> = {
    Pending: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
    TextExtracted: "bg-sky-500/10 text-sky-700 dark:text-sky-300",
    Classified: "bg-indigo-500/10 text-indigo-700 dark:text-indigo-300",
    ChunksCreated: "bg-violet-500/10 text-violet-700 dark:text-violet-300",
    Done: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
    NotSupportedFile: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
    EmptyContent: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
    TextExtractionFailed: "bg-red-500/10 text-red-700 dark:text-red-300",
    ClassificationFailed: "bg-red-500/10 text-red-700 dark:text-red-300",
    VectorizationFailed: "bg-red-500/10 text-red-700 dark:text-red-300",
};

const initialParams: FileCatalogParams = {
    page: 1,
    pageSize: PAGE_SIZE,
    search: "",
    status: "",
    extension: "",
    category: "",
    documentType: "",
    sortBy: "modified",
    sortDirection: "desc",
};

function formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}

function formatDate(value: string): string {
    return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(value));
}

function StatusBadge({ status }: { status: FileProcessStatus }) {
    return (
        <span className={cn("inline-flex rounded-full px-2 py-0.5 text-[0.675rem] font-medium", STATUS_STYLES[status])}>
            {STATUS_LABELS[status]}
        </span>
    );
}

function SortButton({
    label,
    column,
    params,
    onSort,
}: {
    label: string;
    column: string;
    params: FileCatalogParams;
    onSort: (column: string) => void;
}) {
    const active = params.sortBy === column;
    return (
        <button
            type="button"
            className="inline-flex items-center gap-1 font-medium text-muted-foreground hover:text-foreground"
            onClick={() => onSort(column)}
        >
            {label}
            <span aria-hidden>{active ? (params.sortDirection === "asc" ? "↑" : "↓") : "↕"}</span>
        </button>
    );
}

export function MyFiles() {
    const [params, setParams] = useState(initialParams);
    const [searchInput, setSearchInput] = useState("");
    const { data, error, isLoading, isFetching } = useQuery({
        queryKey: ["files", params],
        queryFn: () => fetchFiles(params),
        refetchInterval: (query) => {
            const counts = query.state.data?.statusCounts;
            if (!counts) return false;
            const activeStatuses = ["Pending", "TextExtracted", "Classified", "ChunksCreated"] as const;
            return activeStatuses.some((status) => counts[status] > 0) ? 3000 : false;
        },
    });

    useEffect(() => {
        const timeoutId = window.setTimeout(() => {
            setParams((current) =>
                current.search === searchInput
                    ? current
                    : { ...current, search: searchInput, page: 1 }
            );
        }, 300);

        return () => window.clearTimeout(timeoutId);
    }, [searchInput]);

    const updateParam = (key: keyof FileCatalogParams, value: string) => {
        setParams((current) => ({
            ...current,
            [key]: key === "page" ? Number(value) : value,
            ...(key !== "page" ? { page: 1 } : {}),
        }));
    };

    const handleSort = (column: string) => {
        setParams((current) => ({
            ...current,
            page: 1,
            sortBy: column,
            sortDirection: current.sortBy === column && current.sortDirection === "asc" ? "desc" : "asc",
        }));
    };

    const resetFilters = () => {
        setSearchInput("");
        setParams((current) => ({
            ...current,
            page: 1,
            search: "",
            status: "",
            extension: "",
            category: "",
            documentType: "",
        }));
    };

    const pipelineStatuses = useMemo(() => STATUSES.filter((status) => status.group === "pipeline"), []);
    const attentionStatuses = useMemo(() => STATUSES.filter((status) => status.group === "attention"), []);
    const searchableCount = data?.statusCounts.Done ?? 0;
    const processingCount = pipelineStatuses
        .filter((status) => status.value !== "Done")
        .reduce((total, status) => total + (data?.statusCounts[status.value] ?? 0), 0);

    return (
        <div className="flex h-full min-h-0 flex-col overflow-auto px-4 py-6 sm:px-6">
            <div className="mx-auto flex w-full max-w-7xl flex-col gap-5">
                <div className="flex flex-wrap items-end justify-between gap-3">
                    <div>
                        <h1 className="text-xl font-semibold tracking-tight">My Files</h1>
                        <p className="mt-1 text-sm text-muted-foreground">See what is indexed and what is still being processed.</p>
                    </div>
                    <div className="text-right text-xs text-muted-foreground">
                        <div><span className="font-semibold text-foreground">{searchableCount}</span> searchable</div>
                        {processingCount > 0 && <div>{processingCount} processing</div>}
                    </div>
                </div>

                <section aria-label="Processing status" className="rounded-xl bg-card p-3 ring-1 ring-foreground/10">
                    <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
                        {pipelineStatuses.map((status) => (
                            <button
                                type="button"
                                key={status.value}
                                onClick={() => updateParam("status", params.status === status.value ? "" : status.value)}
                                className={cn(
                                    "rounded-lg px-3 py-2 text-left transition-colors hover:bg-muted",
                                    params.status === status.value && "bg-muted ring-1 ring-primary/40"
                                )}
                            >
                                <div className="text-[0.675rem] font-medium uppercase tracking-wider text-muted-foreground">{status.label}</div>
                                <div className="mt-1 text-xl font-semibold tabular-nums">{data?.statusCounts[status.value] ?? 0}</div>
                            </button>
                        ))}
                    </div>
                    <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 border-t border-border pt-3">
                        <span className="text-[0.675rem] font-medium uppercase tracking-wider text-muted-foreground">Needs attention</span>
                        {attentionStatuses.map((status) => (
                            <button
                                type="button"
                                key={status.value}
                                onClick={() => updateParam("status", params.status === status.value ? "" : status.value)}
                                className={cn("text-xs text-muted-foreground hover:text-foreground", params.status === status.value && "font-semibold text-foreground")}
                            >
                                {status.label} <span className="font-semibold tabular-nums">{data?.statusCounts[status.value] ?? 0}</span>
                            </button>
                        ))}
                    </div>
                </section>

                <section className="flex min-h-0 flex-col overflow-hidden rounded-xl bg-card ring-1 ring-foreground/10">
                    <div className="flex flex-wrap items-center gap-2 border-b border-border p-3">
                        <Input
                            className="h-8 min-w-56 flex-1 text-xs"
                            placeholder="Search name or path..."
                            value={searchInput}
                            onChange={(event) => setSearchInput(event.target.value)}
                        />
                        <Input className="h-8 w-28 text-xs" placeholder="Extension" value={params.extension} onChange={(event) => updateParam("extension", event.target.value)} />
                        <Input className="h-8 w-32 text-xs" placeholder="Category" value={params.category} onChange={(event) => updateParam("category", event.target.value)} />
                        <Input className="h-8 w-36 text-xs" placeholder="Document type" value={params.documentType} onChange={(event) => updateParam("documentType", event.target.value)} />
                        <Select value={params.status || "all"} onValueChange={(value) => updateParam("status", value === "all" ? "" : value ?? "")}>
                            <SelectTrigger className="h-8 w-36 text-xs"><SelectValue /></SelectTrigger>
                            <SelectContent>
                                <SelectItem value="all">All statuses</SelectItem>
                                {STATUSES.map((status) => <SelectItem key={status.value} value={status.value}>{status.label}</SelectItem>)}
                            </SelectContent>
                        </Select>
                        <Button type="button" variant="outline" size="sm" onClick={resetFilters}>
                            Reset filters
                        </Button>
                    </div>

                    {error ? <ErrorMessage message={error instanceof Error ? error.message : "Unable to load files."} /> : (
                        <div className="min-h-0 overflow-x-auto">
                            <table className="w-full min-w-[900px] border-collapse text-xs">
                                <thead className="bg-muted/50 text-left">
                                    <tr className="border-b border-border">
                                        <th className="px-3 py-2"><SortButton label="Name" column="name" params={params} onSort={handleSort} /></th>
                                        <th className="px-3 py-2"><SortButton label="Directory" column="path" params={params} onSort={handleSort} /></th>
                                        <th className="px-3 py-2"><SortButton label="Extension" column="extension" params={params} onSort={handleSort} /></th>
                                        <th className="px-3 py-2"><SortButton label="Category" column="category" params={params} onSort={handleSort} /></th>
                                        <th className="px-3 py-2"><SortButton label="Document type" column="documenttype" params={params} onSort={handleSort} /></th>
                                        <th className="px-3 py-2"><SortButton label="Status" column="status" params={params} onSort={handleSort} /></th>
                                        <th className="px-3 py-2 text-right"><SortButton label="Size" column="size" params={params} onSort={handleSort} /></th>
                                        <th className="px-3 py-2"><SortButton label="Modified" column="modified" params={params} onSort={handleSort} /></th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {isLoading ? <tr><td colSpan={8} className="px-3 py-12 text-center text-muted-foreground">Loading files...</td></tr> : data?.items.length === 0 ? <tr><td colSpan={8} className="px-3 py-12 text-center text-muted-foreground">No files match these filters.</td></tr> : data?.items.map((file) => (
                                        <tr key={file.id} className="border-b border-border/60 last:border-0">
                                            <td className="max-w-52 truncate px-3 py-2.5 font-medium" title={file.name}>{file.name}</td>
                                            <td className="max-w-64 truncate px-3 py-2.5 text-muted-foreground" title={file.path}>{file.directory}</td>
                                            <td className="px-3 py-2.5 text-muted-foreground">{file.extension}</td>
                                            <td className="px-3 py-2.5">{file.category ?? "-"}</td>
                                            <td className="px-3 py-2.5">{file.documentType ?? "-"}</td>
                                            <td className="px-3 py-2.5"><StatusBadge status={file.processStatus} /></td>
                                            <td className="px-3 py-2.5 text-right tabular-nums text-muted-foreground">{formatSize(file.size)}</td>
                                            <td className="whitespace-nowrap px-3 py-2.5 text-muted-foreground">{formatDate(file.fileModifiedAt)}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    <div className="flex flex-wrap items-center justify-between gap-2 border-t border-border p-3 text-xs text-muted-foreground">
                        <span>{data?.totalCount ?? 0} files{isFetching && !isLoading ? " · Updating..." : ""}</span>
                        <div className="flex items-center gap-2">
                            <Button variant="outline" size="sm" disabled={!data || data.page <= 1} onClick={() => updateParam("page", String((data?.page ?? 1) - 1))}>Previous</Button>
                            <span>Page {data?.page ?? 1} of {data?.totalPages || 1}</span>
                            <Button variant="outline" size="sm" disabled={!data || data.page >= data.totalPages} onClick={() => updateParam("page", String((data?.page ?? 1) + 1))}>Next</Button>
                        </div>
                    </div>
                </section>
            </div>
        </div>
    );
}
