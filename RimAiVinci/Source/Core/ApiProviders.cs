namespace RimAiVinci
{
    public static class ApiProviders
    {
        public const int SiliconFlow = 0;
        public const int Pollinations = 1;
        public const int OpenAI = 2;
        public const int ZhipuAI = 3;
        public const int TogetherAI = 4;
        public const int FireworksAI = 5;
        public const int ComfyUI = 6;
        public const int SDWebUI = 7;
        public const int Custom = 8;
        public const int Count = 9;

        public static readonly string[] TranslationKeys = new string[Count]
        {
            "RAV_Provider_SiliconFlow",
            "RAV_Provider_Pollinations",
            "RAV_Provider_OpenAI",
            "RAV_Provider_ZhipuAI",
            "RAV_Provider_TogetherAI",
            "RAV_Provider_FireworksAI",
            "RAV_Provider_ComfyUI",
            "RAV_Provider_SDWebUI",
            "RAV_Provider_Custom"
        };

        public static readonly string[] DefaultUrls = new string[Count]
        {
            "https://api.siliconflow.cn/v1/images/generations",
            "https://gen.pollinations.ai/v1/images/generations",
            "https://api.openai.com/v1/images/generations",
            "https://open.bigmodel.cn/api/paas/v4/images/generations",
            "https://api.together.xyz/v1/images/generations",
            "https://api.fireworks.ai/inference/v1/images/generations",
            "http://127.0.0.1:8188",
            "http://127.0.0.1:7860",
            ""
        };

        public static readonly string[] DefaultModelTxt2Img = new string[Count]
        {
            "Qwen/Qwen-Image",
            "flux",
            "dall-e-3",
            "cogview-4",
            "black-forest-labs/FLUX.1-schnell",
            "flux-1-schnell-fp8",
            "",
            "",
            ""
        };

        public static readonly string[] DefaultModelImg2Img = new string[Count]
        {
            "Qwen/Qwen-Image-Edit-2509",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            ""
        };

        public static readonly string[] DocTips = new string[Count]
        {
            "RAV_Tip_Doc_SiliconFlow",
            "RAV_Tip_Doc_Pollinations",
            "RAV_Tip_Doc_OpenAI",
            "RAV_Tip_Doc_ZhipuAI",
            "RAV_Tip_Doc_TogetherAI",
            "RAV_Tip_Doc_FireworksAI",
            "RAV_Tip_Doc_ComfyUI",
            "RAV_Tip_Doc_SDWebUI",
            "RAV_Tip_Doc_Custom"
        };

        public static bool IsLocalProvider(int index)
        {
            return index == ComfyUI || index == SDWebUI;
        }

        public static void ApplyProviderDefaults(int index)
        {
            if (index < 0 || index >= Count) return;
            var s = RimAiVinciMod.settings;
            if (s == null) return;
            s.apiUrl = DefaultUrls[index];
            s.modelName = DefaultModelTxt2Img[index];
            s.modelNameI2I = DefaultModelImg2Img[index];
        }
    }
}
