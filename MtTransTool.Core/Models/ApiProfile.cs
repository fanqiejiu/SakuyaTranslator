namespace MtTransTool.Core.Models;

public sealed class ApiProfile
{
    public string Provider { get; set; } = "OpenAI";
    public string DisplayName { get; set; } = "默认配置";
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    public string LocalModelPath { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public static class ApiProfileRules
{
    public static string NormalizeProvider(string? provider)
    {
        var value = provider?.Trim() ?? "";
        return value.ToLowerInvariant() switch
        {
            "openai兼容" => "自定义",
            "deepseek" => "DeepSeek",
            "mimo" or "xiaomi mimo" or "小米 mimo" or "小米mimo" => "MiMo",
            "openai" => "OpenAI",
            "gemini" => "Gemini",
            "openrouter" => "OpenRouter",
            "kimi" => "Kimi",
            "qwen" or "通义千问" or "通義千問" => "通义千问",
            "glm" or "zhipu glm" or "智谱 glm" or "智譜 glm" => "智谱 GLM",
            "siliconflow" or "硅基流动" => "硅基流动",
            "local gguf" or "本地 gguf" => "本地 GGUF",
            _ => value
        };
    }

    public static string NormalizeStoredProvider(string? provider)
    {
        var normalized = NormalizeProvider(provider);
        return string.IsNullOrWhiteSpace(normalized) ? "自定义" : normalized;
    }

    public static bool IsRemoteProvider(string? provider) => NormalizeProvider(provider) != "本地 GGUF";

    public static string GetPresetBaseUrl(string? provider)
    {
        return NormalizeProvider(provider) switch
        {
            "OpenAI" => "https://api.openai.com/v1",
            "Gemini" => "https://generativelanguage.googleapis.com/v1beta/openai",
            "DeepSeek" => "https://api.deepseek.com",
            "MiMo" => "https://api.xiaomimimo.com/v1",
            "OpenRouter" => "https://openrouter.ai/api/v1",
            "Kimi" => "https://api.moonshot.ai/v1",
            "通义千问" => "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "智谱 GLM" => "https://open.bigmodel.cn/api/paas/v4",
            "硅基流动" => "https://api.siliconflow.cn/v1",
            _ => ""
        };
    }

    public static string GetEffectiveBaseUrl(ApiProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.BaseUrl))
        {
            return profile.BaseUrl.Trim();
        }

        return GetPresetBaseUrl(profile.Provider);
    }

    public static void NormalizeForUse(ApiProfile profile)
    {
        profile.Provider = NormalizeStoredProvider(profile.Provider);
        if (string.IsNullOrWhiteSpace(profile.BaseUrl))
        {
            profile.BaseUrl = GetPresetBaseUrl(profile.Provider);
        }
    }

    public static bool IsDisplayableTranslationProfile(ApiProfile profile)
    {
        return IsRemoteProvider(profile.Provider)
            && !string.IsNullOrWhiteSpace(GetEffectiveBaseUrl(profile))
            && !string.IsNullOrWhiteSpace(profile.ApiKey)
            && !string.IsNullOrWhiteSpace(profile.Model);
    }

    public static bool IsReadyRemoteProfile(ApiProfile profile)
    {
        return IsDisplayableTranslationProfile(profile);
    }
}
