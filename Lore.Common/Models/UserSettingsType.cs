namespace Lore.Common.Models;

public enum UserSettingGroup
{
    AISettings,
    OCRSettings,
    SearchSettings,
    Tools
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

    ToolsSearchFileContents,

    ToolsSearchFilesByName,

    ToolsGetFullFileContent,

    ToolsGetDirectoryContents,

    ToolsSearchDirectoriesByName,

    ToolsGetFilesByMetadata,

    ToolsListAvailableCategoriesAndTypes

}
