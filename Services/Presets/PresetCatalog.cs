namespace MiniRouter.Services.Presets;

/// <summary>
/// Hardcoded catalog of preset providers.
/// Follows the pattern from docs/reff/open-sse/providers/registry/*.js
/// </summary>
public static class PresetCatalog
{
    public static List<ProviderPreset> All => new()
    {
        // OpenCode Free — free/no-auth, filters to -free suffix + big-pickle
        new(
            Id: "opencode-free",
            Name: "OpenCode Free",
            Category: PresetCategory.Free,
            BaseUrl: "https://opencode.ai/zen",
            ApiKeyRequired: false,
            DefaultApiKey: "public",
            ModelsUrl: "https://opencode.ai/zen/v1/models",
            ModelsFilter: "opencode-free", // apply filter logic (-free suffix + big-pickle)
            DefaultModels: new(), // dynamic via fetch
            Display: new(
                ColorHex: "#E87040",
                TextIcon: "OC",
                WebsiteUrl: "https://opencode.ai",
                Notice: "Free tier with 200+ models including GPT, Claude, Grok, Llama"
            )
        ),

        // OpenRouter — apikey (free-tier badge), fetches all models then filters to free
        new(
            Id: "openrouter",
            Name: "OpenRouter",
            Category: PresetCategory.ApiKey,
            BaseUrl: "https://openrouter.ai/api",
            ApiKeyRequired: true,
            DefaultApiKey: null,
            ModelsUrl: "https://openrouter.ai/api/v1/models",
            ModelsFilter: "openrouter-free", // pricing.prompt == 0 && pricing.completion == 0
            DefaultModels: new(),
            Display: new(
                ColorHex: "#F97316",
                TextIcon: "OR",
                WebsiteUrl: "https://openrouter.ai",
                ApiKeyUrl: "https://openrouter.ai/settings/keys",
                Notice: "Free tier: 27+ free models after signup, no credit card needed"
            )
        ),

        // DeepSeek — apikey, static models
        new(
            Id: "deepseek",
            Name: "DeepSeek",
            Category: PresetCategory.ApiKey,
            BaseUrl: "https://api.deepseek.com",
            ApiKeyRequired: true,
            DefaultApiKey: null,
            ModelsUrl: "https://api.deepseek.com/models",
            ModelsFilter: null, // static list below
            DefaultModels: new()
            {
                "deepseek-chat",
                "deepseek-reasoner"
            },
            Display: new(
                ColorHex: "#4D6BFE",
                TextIcon: "DS",
                WebsiteUrl: "https://deepseek.com",
                ApiKeyUrl: "https://platform.deepseek.com/api_keys",
                Notice: "High-performance models with reasoning capabilities"
            )
        ),

        // Groq — apikey, static models
        new(
            Id: "groq",
            Name: "Groq",
            Category: PresetCategory.ApiKey,
            BaseUrl: "https://api.groq.com/openai",
            ApiKeyRequired: true,
            DefaultApiKey: null,
            ModelsUrl: "https://api.groq.com/openai/v1/models",
            ModelsFilter: null, // static list below
            DefaultModels: new()
            {
                "llama-3.3-70b-versatile",
                "meta-llama/llama-4-maverick-17b-128e-instruct",
                "qwen/qwen3-32b",
                "openai/gpt-oss-120b"
            },
            Display: new(
                ColorHex: "#F55036",
                TextIcon: "GQ",
                WebsiteUrl: "https://groq.com",
                ApiKeyUrl: "https://console.groq.com/keys",
                Notice: "Ultra-fast inference for open-source models"
            )
        )
    };
}
