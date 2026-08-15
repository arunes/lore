import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent, CardHeader, CardTitle } from "./ui/card";
import { Input } from "./ui/input";
import { Textarea } from "./ui/textarea";
import { Label } from "./ui/label";
import { Button } from "./ui/button";
import { Checkbox } from "./ui/checkbox";
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "./ui/select";
import { ErrorMessage } from "./ui/error-message";
import { Markdown } from "./ui/markdown";
import { cn } from "@/lib/utils";
import { fetchSettings, fetchSettingsPresets, saveSettings } from "../api/settingsApi";
import type { Setting } from "../api/settingsTypes";

const GROUP_LABELS: Record<string, string> = {
    AISettings: "AI",
    SearchSettings: "Search",
    OCRSettings: "OCR",
};

type Values = Record<string, string | null>;

function normalizeValue(setting: Setting, raw: string | null): string | null {
    if (raw == null) return null;
    return setting.widget === "Checkbox" ? raw.toLowerCase() : raw;
}

function SettingField({
    setting,
    value,
    onChange,
}: {
    setting: Setting;
    value: string | null;
    onChange: (key: string, value: string | null) => void;
}) {
    const invalid = setting.isRequired && !value?.trim();
    const valueKey = setting.key;
    const isNull = setting.isNullable && value == null;

    if (setting.widget === "Checkbox") {
        const checked = value != null && /^true$/i.test(value);
        const defaultOn = /^true$/i.test(setting.defaultValue ?? "false");
        return (
            <div className="flex flex-col gap-1.5">
                <div className="flex items-center justify-between gap-2">
                    <div className="flex items-center gap-2">
                        <Checkbox
                            id={`setting-${valueKey}`}
                            checked={checked}
                            onCheckedChange={(next) =>
                                onChange(valueKey, next ? "true" : "false")
                            }
                        />
                        <Label htmlFor={`setting-${valueKey}`}>
                            {setting.displayName}
                            {setting.isRequired && (
                                <span className="text-destructive" aria-hidden>
                                    {" "}
                                    *
                                </span>
                            )}
                        </Label>
                    </div>
                    <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        onClick={() => onChange(valueKey, defaultOn ? "true" : "false")}
                    >
                        Reset
                    </Button>
                </div>
                <Markdown>{setting.description}</Markdown>
            </div>
        );
    }

    const control =
        setting.widget === "TextArea" ? (
            <Textarea
                value={value ?? ""}
                disabled={isNull}
                onChange={(e) => onChange(valueKey, e.target.value)}
                className="min-h-40"
            />
        ) : setting.widget === "Number" ? (
            <Input
                type="number"
                value={value ?? ""}
                disabled={isNull}
                min={setting.min ?? undefined}
                max={setting.max ?? undefined}
                step={setting.step ?? undefined}
                onChange={(e) => onChange(valueKey, e.target.value)}
            />
        ) : setting.widget === "Password" ? (
            <Input
                type="password"
                value={value ?? ""}
                disabled={isNull}
                onChange={(e) => onChange(valueKey, e.target.value)}
            />
        ) : setting.widget === "Select" ? (
            <Select
                value={value ?? ""}
                onValueChange={(v) => onChange(valueKey, v || null)}
            >
                <SelectTrigger className="w-full">
                    <SelectValue />
                </SelectTrigger>
                <SelectContent align="start" alignItemWithTrigger={false}>
                    {setting.validValues.map((option) => (
                        <SelectItem key={option} value={option}>
                            {option}
                        </SelectItem>
                    ))}
                </SelectContent>
            </Select>
        ) : (
            <Input
                type="text"
                value={value ?? ""}
                disabled={isNull}
                onChange={(e) => onChange(valueKey, e.target.value)}
            />
        );

    return (
        <div className="flex flex-col gap-1.5">
            <Label htmlFor={`setting-${valueKey}`}>
                {setting.displayName}
                {setting.isRequired && (
                    <span className="text-destructive" aria-hidden>
                        {" "}
                        *
                    </span>
                )}
            </Label>
            <div className="flex items-start gap-1.5">
                <div className="min-w-0 flex-1">{control}</div>
                <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    onClick={() => onChange(valueKey, setting.defaultValue)}
                >
                    Reset
                </Button>
            </div>
            {setting.isNullable && (
                <label className="flex w-fit items-center gap-2 text-xs/relaxed text-muted-foreground">
                    <Checkbox
                        checked={isNull}
                        onCheckedChange={(checked) =>
                            onChange(valueKey, checked ? null : setting.defaultValue)
                        }
                    />
                    Not set
                </label>
            )}
            <Markdown>{setting.description}</Markdown>
            {invalid && (
                <p className="text-xs/relaxed text-destructive">
                    This setting is required.
                </p>
            )}
        </div>
    );
}

export function Settings() {
    const queryClient = useQueryClient();
    const [values, setValues] = useState<Values>({});
    const [saveError, setSaveError] = useState<string | null>(null);
    const [selectedPreset, setSelectedPreset] = useState("");
    const [activeGroup, setActiveGroup] = useState("AISettings");

    const settingsQuery = useQuery({
        queryKey: ["settings"],
        queryFn: fetchSettings,
    });
    const presetsQuery = useQuery({
        queryKey: ["settings-presets"],
        queryFn: fetchSettingsPresets,
    });

    const allSettings = useMemo(
        () => settingsQuery.data?.groups.flatMap((g) => g.settings) ?? [],
        [settingsQuery.data]
    );

    useEffect(() => {
        if (!settingsQuery.data) return;
        setValues((prev) => {
            const next: Values = { ...prev };
            for (const group of settingsQuery.data.groups) {
                for (const setting of group.settings) {
                    if (!(setting.key in next)) {
                        next[setting.key] = normalizeValue(setting, setting.value);
                    }
                }
            }
            return next;
        });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [settingsQuery.data]);

    const dirtyKeys = useMemo(
        () =>
            allSettings
                .filter(
                    (s) =>
                        normalizeValue(s, values[s.key]) !==
                        normalizeValue(s, s.value)
                )
                .map((s) => s.key),
        [allSettings, values]
    );

    const invalidKeys = useMemo(
        () =>
            allSettings
                .filter((s) => s.isRequired && !values[s.key]?.trim())
                .map((s) => s.key),
        [allSettings, values]
    );

    const saveMutation = useMutation({
        mutationFn: async () => {
            setSaveError(null);
            await saveSettings({
                settings: dirtyKeys.map((key) => {
                    const value = values[key];
                    return { key, value: value?.trim() ? value : null };
                }),
            });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ["settings"] });
            toast.success("Settings saved", {
                description: "Your changes have been applied and will be used on the next chat.",
            });
        },
        onError: (error) => {
            const message =
                error instanceof Error ? error.message : "Failed to save settings.";
            setSaveError(message);
            toast.error("Failed to save settings", { description: message });
        },
    });

    if (settingsQuery.isLoading) {
        return (
            <div className="flex h-full min-h-0 flex-col overflow-auto px-4 py-6 sm:px-6">
                <div className="mx-auto flex w-full max-w-7xl items-center justify-center py-16 text-sm text-muted-foreground">
                    Loading settings…
                </div>
            </div>
        );
    }

    if (settingsQuery.isError || !settingsQuery.data) {
        return (
            <div className="flex h-full min-h-0 flex-col overflow-auto px-4 py-6 sm:px-6">
                <div className="mx-auto flex w-full max-w-7xl flex-col gap-3">
                    <div>
                        <h1 className="text-xl font-semibold tracking-tight">Settings</h1>
                        <p className="mt-1 text-sm text-muted-foreground">
                            Configure how Lore connects to models and processes your files.
                        </p>
                    </div>
                    <ErrorMessage
                        title="Failed to load settings"
                        message={
                            settingsQuery.error instanceof Error
                                ? settingsQuery.error.message
                                : "Try again in a moment."
                        }
                        action={
                            <Button variant="outline" onClick={() => settingsQuery.refetch()}>
                                Retry
                            </Button>
                        }
                    />
                </div>
            </div>
        );
    }

    const saveClickable = dirtyKeys.length > 0 && invalidKeys.length === 0;
    const visibleGroup =
        settingsQuery.data.groups.find((group) => group.group === activeGroup) ??
        settingsQuery.data.groups[0];
    const loadPreset = () => {
        const preset = presetsQuery.data?.find((item) => item.name === selectedPreset);
        if (!preset) return;

        const knownKeys = new Set(allSettings.map((setting) => setting.key));
        setValues((current) => {
            const next = { ...current };
            for (const [key, value] of Object.entries(preset.values)) {
                if (knownKeys.has(key)) {
                    next[key] = value;
                }
            }
            return next;
        });
        toast.success("Preset loaded", {
            description: "Review the changes and save them when you are ready.",
        });
        setSelectedPreset("");
    };

    return (
        <div className="flex h-full min-h-0 flex-col overflow-auto px-4 py-6 sm:px-6">
            <div className="mx-auto flex w-full max-w-7xl flex-col gap-5">
                <div className="flex flex-wrap items-end justify-between gap-3">
                    <h1 className="text-xl font-semibold tracking-tight">Settings</h1>
                    <div className="flex items-end gap-2">
                        <label className="flex items-center gap-2 text-xs font-medium text-muted-foreground">
                            Presets:
                            <Select value={selectedPreset || "none"} onValueChange={(value) => setSelectedPreset(value === "none" ? "" : value ?? "")}>
                                <SelectTrigger className="w-full"><SelectValue placeholder="Load a preset" /></SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="none">Load a preset</SelectItem>
                                    {presetsQuery.data?.map((preset) => (
                                        <SelectItem key={preset.name} value={preset.name}>{preset.name}</SelectItem>
                                    ))}
                                </SelectContent>
                            </Select>
                        </label>
                        <Button type="button" variant="outline" disabled={!selectedPreset} onClick={loadPreset}>Load</Button>
                    </div>
                    <p className="order-last basis-full text-sm text-muted-foreground">
                        Configure how Lore connects to models and processes your files.
                    </p>
                </div>

                <div
                    className="flex gap-1 overflow-x-auto border-b border-border"
                    role="tablist"
                    aria-label="Settings categories"
                >
                    {settingsQuery.data.groups.map((group) => {
                        const hasInvalidSetting = group.settings.some((setting) =>
                            invalidKeys.includes(setting.key)
                        );

                        return (
                            <button
                                key={group.group}
                                type="button"
                                role="tab"
                                aria-selected={group.group === visibleGroup.group}
                                aria-controls={`settings-panel-${group.group}`}
                                onClick={() => setActiveGroup(group.group)}
                                className={cn(
                                    "relative shrink-0 px-3 py-2 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground",
                                    group.group === visibleGroup.group && "text-foreground",
                                    hasInvalidSetting && "text-destructive"
                                )}
                            >
                                {GROUP_LABELS[group.group] ?? group.group}
                                {hasInvalidSetting && (
                                    <span className="ml-1" aria-label="contains invalid settings">
                                        ·
                                    </span>
                                )}
                                {group.group === visibleGroup.group && (
                                    <span className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-primary" />
                                )}
                            </button>
                        );
                    })}
                </div>

                <div
                    id={`settings-panel-${visibleGroup.group}`}
                    role="tabpanel"
                    aria-label={GROUP_LABELS[visibleGroup.group] ?? visibleGroup.group}
                >
                    <Card>
                        <CardHeader>
                            <CardTitle>
                                {GROUP_LABELS[visibleGroup.group] ?? visibleGroup.group} settings
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="flex flex-col gap-4">
                            {visibleGroup.settings.map((setting) => (
                                <SettingField
                                    key={setting.key}
                                    setting={setting}
                                    value={values[setting.key] ?? null}
                                    onChange={(key, value) =>
                                        setValues((prev) => ({ ...prev, [key]: value }))
                                    }
                                />
                            ))}
                        </CardContent>
                    </Card>
                </div>

                <div className="flex items-center gap-3 rounded-xl bg-card p-3 ring-1 ring-foreground/10">
                    {saveError ? (
                        <p className="text-xs text-destructive">Save failed — see details above.</p>
                    ) : dirtyKeys.length > 0 ? (
                        <p className="text-xs text-muted-foreground">
                            {dirtyKeys.length} unsaved change
                            {dirtyKeys.length > 1 ? "s" : ""}
                        </p>
                    ) : (
                        <p className="text-xs text-muted-foreground">All changes saved.</p>
                    )}
                    <div className="ml-auto flex items-center gap-2">
                        <Button
                            type="button"
                            variant="outline"
                            disabled={saveMutation.isPending}
                            onClick={() =>
                                setValues((prev) => {
                                    const next: Values = { ...prev };
                                    for (const setting of allSettings) {
                                        next[setting.key] = normalizeValue(
                                            setting,
                                            setting.defaultValue
                                        );
                                    }
                                    return next;
                                })
                            }
                        >
                            Reset all
                        </Button>
                        <Button
                            onClick={() => saveMutation.mutate()}
                            disabled={!saveClickable || saveMutation.isPending}
                        >
                            {saveMutation.isPending ? "Saving…" : "Save changes"}
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
}
