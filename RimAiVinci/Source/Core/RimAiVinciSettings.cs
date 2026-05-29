using Verse;
using UnityEngine;

namespace RimAiVinci
{
    public class RimAiVinciSettings : ModSettings
    {
        public string apiKey = "";
        public string apiUrl = "https://api.siliconflow.cn/v1/images/generations";
        public string modelName = "Qwen/Qwen-Image";
        public string modelNameI2I = "Qwen/Qwen-Image-Edit-2509";
        public float i2iStrength = 0.7f;
        public int providerIndex = 0;
        public int comfyWorkflowMode = 0;
        public string comfyFluxClipName = "";
        public string comfyFluxVaeName = "";

        public bool showPortraitInInspectPane = true;
        public bool showBottomTab = true;
        public float portraitScale = 1.0f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref apiUrl, "apiUrl", "https://api.siliconflow.cn/v1/images/generations");
            Scribe_Values.Look(ref modelName, "modelName", "Qwen/Qwen-Image");
            Scribe_Values.Look(ref modelNameI2I, "modelNameI2I", "Qwen/Qwen-Image-Edit-2509");
            Scribe_Values.Look(ref i2iStrength, "i2iStrength", 0.7f);
            Scribe_Values.Look(ref providerIndex, "providerIndex", 0);

            Scribe_Values.Look(ref comfyWorkflowMode, "comfyWorkflowMode", 0);
            Scribe_Values.Look(ref comfyFluxClipName, "comfyFluxClipName", "");
            Scribe_Values.Look(ref comfyFluxVaeName, "comfyFluxVaeName", "");

            Scribe_Values.Look(ref showPortraitInInspectPane, "showPortraitInInspectPane", true);
            Scribe_Values.Look(ref showBottomTab, "showBottomTab", true);
            Scribe_Values.Look(ref portraitScale, "portraitScale", 1.0f);

            base.ExposeData();
        }

        public void ResetToDefault()
        {
            providerIndex = ApiProviders.SiliconFlow;
            ApiProviders.ApplyProviderDefaults(providerIndex);
            i2iStrength = 0.7f;
            portraitScale = 1.0f;
        }
    }
}
