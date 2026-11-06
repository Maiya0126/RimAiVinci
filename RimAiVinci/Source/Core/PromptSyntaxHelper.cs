namespace RimAiVinci
{
    /// <summary>
    /// 提示词语法模式判定：不同文本编码器对 (xxx:1.3) 权重语法的支持不同。
    /// SD1.5/SDXL (CLIP) 支持；Qwen/Flux (LLM/T5) 会当成字面文本污染提示词。
    /// </summary>
    public static class PromptSyntaxHelper
    {
        /// <summary>
        /// 当前生图渠道是否需要自然语言提示词（不支持权重语法）。
        /// </summary>
        public static bool NeedsNaturalLanguage(int providerIndex, bool isPlayer2Channel)
        {
            // Player2 背后是现代模型，不吃权重语法
            if (isPlayer2Channel) return true;

            switch (providerIndex)
            {
                case ApiProviders.ComfyUI:
                    // ComfyUI 标准模式走 CLIP；Flux 工作流走 T5
                    return RimAiVinciMod.settings != null && RimAiVinciMod.settings.comfyWorkflowMode == 1;
                case ApiProviders.SDWebUI:
                    return false;
                case ApiProviders.SiliconFlow:
                case ApiProviders.Pollinations:
                    return UsesNlModel(providerIndex);
                case ApiProviders.OpenAI:
                case ApiProviders.ZhipuAI:
                    return true; // DALL-E/CogView 无权重语法
                case ApiProviders.TogetherAI:
                case ApiProviders.FireworksAI:
                case ApiProviders.Custom:
                default:
                    return UsesNlModel(providerIndex);
            }
        }

        private static bool UsesNlModel(int providerIndex)
        {
            if (RimAiVinciMod.settings == null) return false;
            string txt = RimAiVinciMod.settings.modelName;
            string i2i = RimAiVinciMod.settings.modelNameI2I;
            return ContainsNlModel(txt) || ContainsNlModel(i2i);
        }

        private static bool ContainsNlModel(string model)
        {
            if (string.IsNullOrEmpty(model)) return false;
            string m = model.ToLower();
            // Qwen 系 / Flux 系 / DALL-E / CogView / SD3 / 现代架构模型均用自然语言编码器
            return m.Contains("qwen") || m.Contains("flux") || m.Contains("dall-e") ||
                   m.Contains("cogview") || m.Contains("sd3") || m.Contains("stable diffusion 3");
        }
    }
}
