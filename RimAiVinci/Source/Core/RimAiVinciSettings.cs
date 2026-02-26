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

        // UI 显示设置
        public bool showPortraitInInspectPane = true;
        public bool showBottomTab = true;

        // ✨ 新增：立绘缩放倍率 (默认 1.0)
        public float portraitScale = 1.0f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref apiKey, "apiKey", "");
            Scribe_Values.Look(ref apiUrl, "apiUrl", "https://api.siliconflow.cn/v1/images/generations");
            Scribe_Values.Look(ref modelName, "modelName", "Qwen/Qwen-Image");
            Scribe_Values.Look(ref modelNameI2I, "modelNameI2I", "Qwen/Qwen-Image-Edit-2509");
            Scribe_Values.Look(ref i2iStrength, "i2iStrength", 0.7f);

            Scribe_Values.Look(ref showPortraitInInspectPane, "showPortraitInInspectPane", true);
            Scribe_Values.Look(ref showBottomTab, "showBottomTab", true);

            // 保存缩放设置
            Scribe_Values.Look(ref portraitScale, "portraitScale", 1.0f);

            base.ExposeData();
        }

        public void ResetToDefault()
        {
            apiUrl = "https://api.siliconflow.cn/v1/images/generations";
            modelName = "Qwen/Qwen-Image";
            modelNameI2I = "Qwen/Qwen-Image-Edit-2509";
            i2iStrength = 0.7f;
            portraitScale = 1.0f; // 重置时恢复大小
        }
    }
}