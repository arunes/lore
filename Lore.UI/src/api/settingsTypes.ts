export type SettingWidget =
  | "Text"
  | "Password"
  | "TextArea"
  | "Number"
  | "Select"
  | "Checkbox";

export type Setting = {
  key: string;
  displayName: string;
  description: string;
  group: string;
  widget: SettingWidget;
  isSecret: boolean;
  isRequired: boolean;
  isNullable: boolean;
  min: number | null;
  max: number | null;
  step: number | null;
  value: string | null;
  defaultValue: string | null;
  validValues: string[];
  hasOverride: boolean;
};

export type SettingsGroup = {
  group: string;
  settings: Setting[];
};

export type SettingsResponse = {
  groups: SettingsGroup[];
};

export type SettingValue = {
  key: string;
  value: string | null;
};

export type SettingsRequest = {
  settings: SettingValue[];
};
