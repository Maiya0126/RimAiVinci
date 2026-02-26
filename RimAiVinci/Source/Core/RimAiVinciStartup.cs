using Verse;
using HarmonyLib;

namespace RimAiVinci
{
    // 这个标签告诉游戏：在游戏启动、加载完所有 Def 之后，自动执行这个类
    [StaticConstructorOnStartup]
    public static class RimAiVinciStartup
    {
        static RimAiVinciStartup()
        {
            // 实例化 Harmony，括号里是您的模组唯一ID，通常是 "作者名.模组名"
            Harmony harmony = new Harmony("Maiya.RimAiVinci");

            // 自动扫描当前程序集里所有带有 [HarmonyPatch] 标签的类，并应用它们
            harmony.PatchAll();

            Log.Message("[Rim AiVinci] Harmony patches applied successfully! 🎨");
        }
    }
}