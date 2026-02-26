using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace RimAiVinci
{
    [HarmonyPatch(typeof(Window), "WindowOnGUI")]
    public static class Patch_PortraitDisplay
    {
        private static Texture2D cachedPortraitTex;
        private static string cachedPath;
        private const float TAB_HEIGHT_OFFSET = 30f;

        public static void Postfix(Window __instance)
        {
            if (RimAiVinciMod.settings == null || !RimAiVinciMod.settings.showPortraitInInspectPane) return;

            MainTabWindow_Inspect inspectWindow = __instance as MainTabWindow_Inspect;
            if (inspectWindow == null || !inspectWindow.IsOpen) return;

            if (Find.World == null || Find.CurrentMap == null) return;
            Pawn selectedPawn = Find.Selector.SingleSelectedThing as Pawn;
            if (selectedPawn == null) return;

            var store = Find.World.GetComponent<ArtDataStore>();
            if (store == null) return;

            string currentPath = store.GetActivePortraitPath(selectedPawn);

            if (currentPath != cachedPath)
            {
                cachedPath = currentPath;
                cachedPortraitTex = null;
                if (!string.IsNullOrEmpty(currentPath))
                {
                    cachedPortraitTex = ArtFileSystem.LoadTextureFromDisk(currentPath);
                }
            }

            if (cachedPortraitTex != null)
            {
                // ✨✨ 核心修改：应用缩放倍率 ✨✨
                // 基础高度 350f * 用户设置的倍率
                float baseHeight = 350f;
                float portraitHeight = baseHeight * RimAiVinciMod.settings.portraitScale;

                float ratio = (float)cachedPortraitTex.width / cachedPortraitTex.height;
                float portraitWidth = portraitHeight * ratio;

                float bottomY = inspectWindow.PaneTopY - TAB_HEIGHT_OFFSET;

                Rect drawRect = new Rect(
                    20f,
                    bottomY - portraitHeight,   // 高度变了，顶部位置自动调整，底部保持不变
                    portraitWidth,
                    portraitHeight
                );

                GUI.color = Color.white;
                GUI.DrawTexture(drawRect, cachedPortraitTex, ScaleMode.StretchToFill);
            }
        }
    }
}