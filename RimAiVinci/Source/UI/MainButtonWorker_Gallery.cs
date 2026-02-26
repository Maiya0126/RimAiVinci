using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    // 继承 MainButtonWorker_ToggleTab 是对的，它对应 XML 里的 MainButtonDef
    public class MainButtonWorker_Gallery : MainButtonWorker_ToggleTab
    {
        private Texture2D cachedIcon;

        // ✨✨✨ 核心新增：控制按钮显示/隐藏 ✨✨✨
        // 游戏每帧都会检查这个属性，如果返回 false，按钮就会消失
        public override bool Visible
        {
            get
            {
                // 1. 如果 Mod 设置里关掉了底部 Tab，直接隐藏
                if (RimAiVinciMod.settings != null && !RimAiVinciMod.settings.showBottomTab)
                {
                    return false;
                }

                // 2. 否则执行原版逻辑 (通常只要 def 定义了就会显示)
                return base.Visible;
            }
        }

        // 获取图标 (保持您原有的逻辑)
        private Texture2D Icon
        {
            get
            {
                if (cachedIcon == null && !def.iconPath.NullOrEmpty())
                {
                    cachedIcon = ContentFinder<Texture2D>.Get(def.iconPath, true);
                }
                return cachedIcon;
            }
        }

        // 绘制按钮 (保持您原有的漂亮样式)
        public override void DoButton(Rect rect)
        {
            // 1. 绘制背景
            Widgets.DrawAtlas(rect, Widgets.ButtonSubtleAtlas);

            // 2. 绘制高亮
            if (Find.WindowStack.IsOpen(def.TabWindow))
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlightIfMouseover(rect);
            }

            // 3. 绘制图标
            if (Icon != null)
            {
                Widgets.DrawTextureFitted(rect, Icon, 0.85f);
            }

            // 4. 右键打开设置
            if (Mouse.IsOver(rect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
            {
                Event.current.Use();
                // 确保这里能获取到 Mod 实例
                var mod = LoadedModManager.GetMod<RimAiVinciMod>();
                if (mod != null) Find.WindowStack.Add(new Dialog_ModSettings(mod));
            }

            // 5. 左键点击逻辑
            // 注意：MainButtonWorker_ToggleTab 默认的 Activate 会切换 TabWindow
            if (Widgets.ButtonInvisible(rect))
            {
                Activate();
            }

            // 6. 悬停提示
            if (!def.description.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rect, def.description);
            }
        }
    }
}