using LudeonTK;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    public static class DebugActions
    {
        // ==========================================
        // 1. UI 测试：直接打开画廊
        // ==========================================
        [DebugAction("Rim AiVinci", "Open Gallery UI", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void OpenGalleryWindow()
        {
            Find.WindowStack.Add(new Window_Gallery());
        }

        // ==========================================
        // 2. 核心工具：创作魔法棒 (点选小人 -> 打开创作面板)
        // ==========================================
        [DebugAction("Rim AiVinci", "Tool: Open Art Creator", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void OpenArtCreatorTool(Pawn p)
        {
            if (p == null) return;
            Find.WindowStack.Add(new Window_ArtCreator(p));
        }

        // ==========================================
        // 3. 快速冒烟测试 (已修复参数错误)
        // ==========================================
        [DebugAction("Rim AiVinci", "Test: Quick Random Generate", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void TestQuickGenerate()
        {
            Pawn p = Find.CurrentMap.mapPawns.FreeColonists.RandomElementWithFallback(null);
            if (p == null) return;

            // 🔴 修复点：这里补齐了新增加的参数
            // extraPrompt: "" (空字符串)
            // forceFullBody: true (默认开启全身)
            // useCinematicLighting: true (默认开启光影)
            PromptOptions opt = new PromptOptions(); // 使用默认设置
            string prompt = PawnPromptBuilder.BuildPromptFromPawn(p, opt);

            Messages.Message("RAV_Debug_QuickGen".Translate(p.Name != null ? p.Name.ToStringShort : p.def.label), MessageTypeDefOf.NeutralEvent, false);

            SiliconClient.GenerateImageAsync(prompt, (texture) =>
            {
                Find.WindowStack.Add(new Dialog_ShowImage(texture));
                Messages.Message("RAV_Debug_GenDone".Translate(), MessageTypeDefOf.TaskCompletion, false);
            });
        }
    }
}