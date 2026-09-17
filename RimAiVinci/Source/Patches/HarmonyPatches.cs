using System;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace RimAiVinci
{
    [HarmonyPatch(typeof(Pawn), "Kill")]
    public static class Pawn_Kill_Memorial_Patch
    {
        public static void Postfix(Pawn __instance)
        {
            try
            {
                if (__instance == null) return;
                if (__instance.RaceProps == null || !__instance.RaceProps.Humanlike) return;
                if (__instance.Faction != Faction.OfPlayer) return;
                if (Find.World == null) return;
                LongEventHandler.ExecuteWhenFinished(() => MemorialCapturer.CaptureAndSave(__instance));
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Dialog_InfoCard), "FillCard")]
    public static class Dialog_InfoCard_FillCard_Patch
    {
        private static Dictionary<string, Texture2D> portraitCache = new Dictionary<string, Texture2D>();

        // ✨ 改回 Prefix (前置补丁)
        // 注意：Prefix 如果返回 true，会继续执行原版方法；返回 false 则拦截原版方法。
        // 我们这里只需要画个背景，所以必须返回 true。
        public static bool Prefix(Rect cardRect, Dialog_InfoCard.InfoCardTab ___tab, Thing ___thing)
        {
            // 1. 基础检查
            if (___tab != Dialog_InfoCard.InfoCardTab.Stats) return true;
            Pawn pawn = ___thing as Pawn;
            if (pawn == null) return true;

            // 2. 读取数据 (状态立绘优先，回退到普通立绘)
            ArtDataStore store = Find.World.GetComponent<ArtDataStore>();
            if (store == null) return true;
            string activePath = store.GetStatePortraitPath(pawn, PortraitStateHelper.GetCurrentState(pawn));
            if (string.IsNullOrEmpty(activePath)) activePath = store.GetActivePortraitPath(pawn);
            if (string.IsNullOrEmpty(activePath)) return true;

            // 3. 加载图片 (带缓存)
            Texture2D displayTex = null;
            if (portraitCache.ContainsKey(activePath)) displayTex = portraitCache[activePath];
            else
            {
                displayTex = ArtFileSystem.LoadTextureFromDisk(activePath);
                if (displayTex != null) portraitCache[activePath] = displayTex;
            }
            if (displayTex == null) return true;

            // 4. 计算区域 (Mimikko 的经典布局)
            Rect position = new Rect(0, 0, 384f, 576f);
            position.x = cardRect.width * 0.76f - position.width / 2f + 18f;
            position.y = cardRect.center.y - position.height / 2f;

            // ✨✨✨ 核心修改：设置透明度 ✨✨✨
            // 保存当前的 GUI 颜色
            Color oldColor = GUI.color;
            // 设置为半透明 (0.4f 是一个比较舒服的值，您可以根据喜好调整 0.1 ~ 1.0)
            GUI.color = new Color(1f, 1f, 1f, 0.4f);

            // 绘制立绘 (作为背景)
            GUI.DrawTexture(position, displayTex, ScaleMode.ScaleToFit, true);

            // ✨✨✨ 恢复 GUI 颜色 ✨✨✨
            // 这一步至关重要！如果不恢复，后面所有的 UI 都会变成半透明的。
            GUI.color = oldColor;

            // 必须返回 true，让原版的文字继续绘制
            return true;
        }

        public static void ClearCache()
        {
            foreach (var tex in portraitCache.Values)
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
            portraitCache.Clear();
        }
    }
}