using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace RimAiVinci
{
    [HarmonyPatch(typeof(Window), "WindowOnGUI")]
    public static class Patch_PortraitDisplay
    {
        // 立绘贴图 LRU 缓存：切换选中角色时避免反复读盘解码
        private static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
        private static readonly List<string> textureOrder = new List<string>();
        private const int MaxCachedTextures = 6;

        private const float TAB_HEIGHT_OFFSET = 30f;
        private const float BASE_PORTRAIT_HEIGHT = 350f;
        private const float BASE_PORTRAIT_X = 20f;

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

            string currentPath = store.GetStatePortraitPath(selectedPawn, PortraitStateHelper.GetCurrentState(selectedPawn));
            if (string.IsNullOrEmpty(currentPath)) currentPath = store.GetActivePortraitPath(selectedPawn);
            if (string.IsNullOrEmpty(currentPath)) return;

            Texture2D portraitTex = GetTexture(currentPath);
            if (portraitTex == null) return;

            // 优先使用该小人的独立排版，否则回退到全局默认
            float offsetX = RimAiVinciMod.settings.portraitOffsetX;
            float offsetY = RimAiVinciMod.settings.portraitOffsetY;
            float scale = RimAiVinciMod.settings.portraitScale;
            PortraitLayout layout;
            if (store.TryGetPortraitLayout(selectedPawn.ThingID, out layout))
            {
                offsetX = layout.offsetX;
                offsetY = layout.offsetY;
                scale = layout.scale;
            }

            float portraitHeight = BASE_PORTRAIT_HEIGHT * scale;
            float ratio = (float)portraitTex.width / portraitTex.height;
            float portraitWidth = portraitHeight * ratio;

            float bottomY = inspectWindow.PaneTopY - TAB_HEIGHT_OFFSET;

            // 偏移量以立绘高度为单位：分辨率与 UI 缩放变化时保持相对位置
            Rect drawRect = new Rect(
                BASE_PORTRAIT_X + offsetX * portraitHeight,
                bottomY - portraitHeight + offsetY * portraitHeight,
                portraitWidth,
                portraitHeight
            );

            GUI.color = Color.white;
            GUI.DrawTexture(drawRect, portraitTex, ScaleMode.StretchToFill);
        }

        private static Texture2D GetTexture(string path)
        {
            Texture2D tex;
            if (textureCache.TryGetValue(path, out tex))
            {
                if (tex != null)
                {
                    textureOrder.Remove(path);
                    textureOrder.Add(path);
                    return tex;
                }
                textureCache.Remove(path);
                textureOrder.Remove(path);
            }

            tex = ArtFileSystem.LoadTextureFromDisk(path);
            if (tex == null) return null;

            textureCache[path] = tex;
            textureOrder.Add(path);
            while (textureOrder.Count > MaxCachedTextures)
            {
                string oldest = textureOrder[0];
                textureOrder.RemoveAt(0);
                Texture2D oldTex;
                if (textureCache.TryGetValue(oldest, out oldTex))
                {
                    textureCache.Remove(oldest);
                    if (oldTex != null) Object.Destroy(oldTex);
                }
            }
            return tex;
        }
    }
}
