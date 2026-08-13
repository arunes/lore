namespace Lore.Common.Models;

public enum UserSettingGroup
{
    AISettings,
    OCRSettings,
    SearchSettings,
}

public enum UserSettingsType
{
    AIBackendAPIUrl,

    AIBackendAPIKey,

    AIBackendAPIModel,

    AIBackendRAGService,

    TraditionalSystemPrompt,

    RetrievalQueryPrompt,

    AgenticSystemPrompt,

    MaxNumberSearchResults,

    SearchFTSWeight,

    SearchVectorWeight,

    ChatTemperature,

    RetrievalQueryTemperature,

    OCRModelsDetFileName,

    OCRModelsClsFileName,

    OCRModelsRecFileName,

    OCRModelsKeysFileName,
}
