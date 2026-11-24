using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    // 立绘排版窗口：拖动调整位置、滚轮缩放，支持仅当前小人或设为全局默认
    public class Window_PortraitLayout : Window
    {
        private const float PREVIEW_WIDTH = 356f;
        private const float PREVIEW_HEIGHT = 520f;
        private const float PREVIEW_TOP = 34f;
        private const float BASE_PORTRAIT_HEIGHT = 350f;
        private const float PREVIEW_BOTTOM_INSET = 30f;
        private const float PREVIEW_LEFT_INSET = 20f;

        private Pawn pawn;
        private PortraitLayout layout;
        private Texture2D portraitTex;
        private bool dragging;
        private bool canOperate;

        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public Window_PortraitLayout(Pawn pawn)
        {
            this.pawn = pawn;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = true;
            this.resizeable = false;
            this.absorbInputAroundWindow = true;

            layout = new PortraitLayout();
            canOperate = Current.ProgramState == ProgramState.Playing;
            if (!canOperate) return;

            layout.offsetX = RimAiVinciMod.settings.portraitOffsetX;
            layout.offsetY = RimAiVinciMod.settings.portraitOffsetY;
            layout.scale = RimAiVinciMod.settings.portraitScale;

            var store = Find.World != null ? Find.World.GetComponent<ArtDataStore>() : null;
            PortraitLayout saved;
            if (store != null && pawn != null && store.TryGetPortraitLayout(pawn.ThingID, out saved))
            {
                layout.CopyFrom(saved);
            }

            LoadPortraitTexture();
        }

        public override void PostClose()
        {
            base.PostClose();
            if (portraitTex != null)
            {
                Object.Destroy(portraitTex);
                portraitTex = null;
            }
        }

        private void LoadPortraitTexture()
        {
            if (pawn == null) return;
            var store = Find.World != null ? Find.World.GetComponent<ArtDataStore>() : null;
            if (store == null) return;

            string path = store.GetStatePortraitPath(pawn, PortraitStateHelper.GetCurrentState(pawn));
            if (string.IsNullOrEmpty(path)) path = store.GetActivePortraitPath(pawn);
            if (string.IsNullOrEmpty(path)) return;

            portraitTex = ArtFileSystem.LoadTextureFromDisk(path);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!canOperate)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inRect, "RAV_Layout_EntryHint".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            string pawnName = pawn != null ? (pawn.Name != null ? pawn.Name.ToStringShort : pawn.def.label) : "";
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 30f), "RAV_Layout_Title".Translate(pawnName));
            Text.Font = GameFont.Small;

            Rect previewRect = new Rect(0, PREVIEW_TOP, PREVIEW_WIDTH, PREVIEW_HEIGHT);
            DrawPreview(previewRect);

            Rect sideRect = new Rect(PREVIEW_WIDTH + 14f, PREVIEW_TOP, inRect.width - PREVIEW_WIDTH - 14f, PREVIEW_HEIGHT);
            DrawSidePanel(sideRect);

            DrawBottomButtons(inRect);
        }

        private void DrawPreview(Rect previewRect)
        {
            Widgets.DrawMenuSection(previewRect);
            if (portraitTex == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(previewRect, "RAV_Layout_NoPortrait".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float displayedHeight = BASE_PORTRAIT_HEIGHT * layout.scale;
            float ratio = (float)portraitTex.width / portraitTex.height;
            float displayedWidth = displayedHeight * ratio;
            float localBottom = previewRect.height - PREVIEW_BOTTOM_INSET;

            GUI.BeginClip(previewRect);
            Rect localRect = new Rect(
                PREVIEW_LEFT_INSET + layout.offsetX * displayedHeight,
                localBottom - displayedHeight + layout.offsetY * displayedHeight,
                displayedWidth,
                displayedHeight
            );
            GUI.DrawTexture(localRect, portraitTex, ScaleMode.StretchToFill);

            // 参考线：信息面板底部
            Widgets.DrawBoxSolid(new Rect(0, previewRect.height - 2f, previewRect.width, 2f), new Color(0.4f, 0.4f, 0.4f, 0.6f));
            GUI.EndClip();

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(previewRect.x + 6f, previewRect.yMax + 2f, previewRect.width - 12f, 18f), "RAV_Layout_Hint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            HandleDrag(previewRect, displayedHeight);
        }

        private void HandleDrag(Rect previewRect, float displayedHeight)
        {
            if (displayedHeight <= 0f) return;
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && previewRect.Contains(e.mousePosition))
            {
                dragging = true;
                e.Use();
            }
            else if (dragging && e.type == EventType.MouseDrag)
            {
                layout.offsetX = Mathf.Clamp(layout.offsetX + e.delta.x / displayedHeight, -3f, 3f);
                layout.offsetY = Mathf.Clamp(layout.offsetY - e.delta.y / displayedHeight, -3f, 3f);
                e.Use();
            }
            else if (dragging && (e.type == EventType.MouseUp || e.type == EventType.Ignore))
            {
                dragging = false;
                e.Use();
            }

            if ((e.type == EventType.ScrollWheel || e.type == EventType.MouseDown) && previewRect.Contains(e.mousePosition))
            {
                if (e.type == EventType.ScrollWheel)
                {
                    layout.scale = Mathf.Clamp(layout.scale - e.delta.y * 0.02f, 0.3f, 2.5f);
                    e.Use();
                }
            }
        }

        private void DrawSidePanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);
            float y = inner.y;

            GUI.color = new Color(1f, 0.9f, 0.5f);
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "RAV_Layout_OffsetX".Translate(layout.offsetX.ToString("P0")));
            GUI.color = Color.white;
            y += 22f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "RAV_Layout_OffsetY".Translate(layout.offsetY.ToString("P0")));
            y += 22f;
            Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "RAV_Layout_Scale".Translate(layout.scale.ToString("P0")));
            y += 30f;

            float step = Event.current.shift ? 0.05f : 0.01f;
            float btnW = 56f;
            float btnH = 26f;
            float centerX = inner.x + (inner.width - btnW) / 2f;

            if (Widgets.ButtonText(new Rect(centerX, y, btnW, btnH), "↑")) layout.offsetY += step;
            y += btnH + 4f;
            if (Widgets.ButtonText(new Rect(centerX - btnW - 4f, y, btnW, btnH), "←")) layout.offsetX -= step;
            if (Widgets.ButtonText(new Rect(centerX, y, btnW, btnH), "•")) { layout.offsetX = 0f; layout.offsetY = 0f; }
            if (Widgets.ButtonText(new Rect(centerX + btnW + 4f, y, btnW, btnH), "→")) layout.offsetX += step;
            y += btnH + 4f;
            if (Widgets.ButtonText(new Rect(centerX, y, btnW, btnH), "↓")) layout.offsetY -= step;
            y += btnH + 8f;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.75f, 0.75f, 0.75f);
            Widgets.Label(new Rect(inner.x, y, inner.width, 36f), "RAV_Layout_NudgeHint".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 40f;

            // 数值越界时也做一次夹紧，避免按钮微调把立绘推飞
            layout.offsetX = Mathf.Clamp(layout.offsetX, -3f, 3f);
            layout.offsetY = Mathf.Clamp(layout.offsetY, -3f, 3f);
            layout.scale = Mathf.Clamp(layout.scale, 0.3f, 2.5f);
        }

        private void DrawBottomButtons(Rect inRect)
        {
            float y = PREVIEW_TOP + PREVIEW_HEIGHT + 24f;
            float half = (inRect.width - 6f) / 2f;

            if (Widgets.ButtonText(new Rect(0, y, half, 30f), "RAV_Layout_ApplyPawn".Translate()))
            {
                var store = Find.World.GetComponent<ArtDataStore>();
                if (store != null && pawn != null)
                {
                    store.SetPortraitLayout(pawn.ThingID, layout);
                    Messages.Message("RAV_Layout_Saved".Translate(), MessageTypeDefOf.TaskCompletion, false);
                }
            }
            if (Widgets.ButtonText(new Rect(half + 6f, y, half, 30f), "RAV_Layout_ApplyGlobal".Translate()))
            {
                RimAiVinciMod.settings.portraitOffsetX = layout.offsetX;
                RimAiVinciMod.settings.portraitOffsetY = layout.offsetY;
                RimAiVinciMod.settings.portraitScale = layout.scale;
                RimAiVinciMod.settings.Write();
                var store = Find.World.GetComponent<ArtDataStore>();
                if (store != null) store.ClearAllPortraitLayouts();
                Messages.Message("RAV_Layout_GlobalSaved".Translate(), MessageTypeDefOf.TaskCompletion, false);
            }

            y += 36f;
            if (Widgets.ButtonText(new Rect(0, y, half, 30f), "RAV_Layout_UseGlobal".Translate()))
            {
                var store = Find.World.GetComponent<ArtDataStore>();
                if (store != null && pawn != null) store.ClearPortraitLayout(pawn.ThingID);
                layout.offsetX = RimAiVinciMod.settings.portraitOffsetX;
                layout.offsetY = RimAiVinciMod.settings.portraitOffsetY;
                layout.scale = RimAiVinciMod.settings.portraitScale;
                Messages.Message("RAV_Layout_ResetDone".Translate(), MessageTypeDefOf.TaskCompletion, false);
            }
            if (Widgets.ButtonText(new Rect(half + 6f, y, half, 30f), "RAV_Layout_Reset".Translate()))
            {
                layout.offsetX = 0f;
                layout.offsetY = 0f;
                layout.scale = 1f;
            }
        }
    }
}
