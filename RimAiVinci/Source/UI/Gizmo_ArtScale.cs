using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    public class Gizmo_ArtScale : Gizmo
    {
        public Building_PhotoFrame frame;

        public Gizmo_ArtScale(Building_PhotoFrame frame)
        {
            this.frame = frame;
            // 注意：删掉了 this.order = -100f; 以防止红字报错
        }

        public override float GetWidth(float maxWidth)
        {
            return 160f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);

            // 1. 背景
            Widgets.DrawWindowBackground(rect);

            // 2. 标题 (显示当前具体数值)
            Rect labelRect = new Rect(rect.x, rect.y + 5, rect.width, 20);
            Text.Anchor = TextAnchor.UpperCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(labelRect, "RAV_Gizmo_CurrentScale".Translate(frame.ImageScale.ToString("P0")));
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // 3. 滑动条
            Rect sliderRect = new Rect(rect.x + 10, rect.y + 25, rect.width - 20, 24);

            // ✨✨ 核心修复：参数位置修正 ✨✨
            // 参数顺序：rect, value, min, max, middleAlignment, middleLabel, leftLabel, rightLabel
            // 之前把 "10%" 传给了 middleLabel，导致显示在中间。
            // 这次我们把 middleLabel 设为 null，确保左右标签正确。

            float newVal = Widgets.HorizontalSlider(
                sliderRect,
                frame.ImageScale,
                0.1f,
                10.0f,
                false,
                null,   // middleLabel (不显示中间文字)
                "10%",  // leftLabel (左边显示 10%)
                "1000%" // rightLabel (右边显示 1000%)
            );

            if (newVal != frame.ImageScale)
            {
                frame.SetScale(newVal);
            }

            // 4. 重置按钮
            Rect resetRect = new Rect(rect.x + (rect.width - 80) / 2, rect.y + 50, 80, 20);
            if (Widgets.ButtonText(resetRect, "RAV_Gizmo_ResetScale".Translate()))
            {
                frame.SetScale(1.0f);
            }

            return new GizmoResult(GizmoState.Clear);
        }
    }
}