using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    /// <summary>
    /// 种族描述编辑器：左侧种族列表（自动扫描已加载的人类种族），右侧编辑。
    /// 玩家自定义描述优先于内置词库，保存在存档数据目录，跨存档共享。
    /// </summary>
    public class Window_RacePromptEditor : Window
    {
        private List<ThingDef> humanRaces;
        private ThingDef selectedRace;
        private string editingText = "";
        private Vector2 scrollPosLeft = Vector2.zero;
        private Vector2 scrollPosRight = Vector2.zero;
        private bool initialized = false;

        public override Vector2 InitialSize => new Vector2(760f, 620f);

        public Window_RacePromptEditor(ThingDef preselect = null)
        {
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            resizeable = false;
            if (preselect != null) selectedRace = preselect;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            if (!initialized)
            {
                LoadRaces();
                if (selectedRace == null && humanRaces.Count > 0) selectedRace = humanRaces[0];
                if (selectedRace != null) LoadEditingText();
                initialized = true;
            }
        }

        private void LoadRaces()
        {
            humanRaces = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.race != null && d.race.Humanlike && d.building == null
                            && d.category == ThingCategory.Pawn          // 排除生成的尸体def（Corpse_XXX 是 Item）
                            && d.thingClass != typeof(Corpse))           // 双保险：挡掉 UnnaturalCorpse 等
                .OrderBy(d => d.label)
                .ToList();
        }

        private void LoadEditingText()
        {
            if (selectedRace == null) { editingText = ""; return; }
            // 玩家自定义优先，其次内置 tagForm 供参考编辑
            string custom = CustomRacePromptStore.AllCustom.TryGetValue(selectedRace.defName, out string c) ? c : null;
            if (!string.IsNullOrEmpty(custom)) editingText = custom;
            else
            {
                var builtIn = GetBuiltInForDef(selectedRace);
                editingText = builtIn != null ? builtIn.tagForm : "";
            }
        }

        private RacePromptEntry GetBuiltInForDef(ThingDef def)
        {
            var pawnLike = RaceDescriptionMapper.GetBuiltInEntryForDef(def);
            return pawnLike;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width - 100, 34), "RAV_RaceEditor_Title".Translate());
            Text.Font = GameFont.Small;

            float topY = 38f;
            float leftW = 230f;
            float margin = 8f;

            // 左侧：种族列表
            Rect leftRect = new Rect(0, topY, leftW, inRect.height - topY - 40f);
            Widgets.DrawMenuSection(leftRect);
            DrawRaceList(leftRect);

            // 右侧：编辑区
            Rect rightRect = new Rect(leftW + margin, topY, inRect.width - leftW - margin, inRect.height - topY - 40f);
            Widgets.DrawMenuSection(rightRect);
            DrawEditPanel(rightRect);

            // 底部按钮
            float btnY = inRect.height - 34f;
            float btnW = 170f;
            if (Widgets.ButtonText(new Rect(0, btnY, btnW, 30), "RAV_RaceEditor_Save".Translate())) SaveCurrent();
            if (Widgets.ButtonText(new Rect(btnW + 8, btnY, btnW, 30), "RAV_RaceEditor_Reset".Translate())) ResetCurrent();
        }

        private void DrawRaceList(Rect rect)
        {
            Rect inner = rect.ContractedBy(6);
            Rect searchHint = new Rect(inner.x, inner.y, inner.width, 20);
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Text.Font = GameFont.Tiny;
            Widgets.Label(searchHint, "RAV_RaceEditor_ListHint".Translate() + " (" + humanRaces.Count + ")");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Rect listRect = new Rect(inner.x, inner.y + 24, inner.width, inner.height - 24);
            Rect viewRect = new Rect(0, 0, listRect.width - 16, humanRaces.Count * 30);
            Widgets.BeginScrollView(listRect, ref scrollPosLeft, viewRect);

            float y = 0;
            foreach (var def in humanRaces)
            {
                Rect row = new Rect(0, y, viewRect.width, 28);
                bool sel = selectedRace == def;
                if (sel) Widgets.DrawHighlightSelected(row);
                else Widgets.DrawHighlightIfMouseover(row);
                if (Widgets.ButtonInvisible(row))
                {
                    if (selectedRace != def)
                    {
                        selectedRace = def;
                        LoadEditingText();
                        scrollPosRight = Vector2.zero;
                    }
                }

                bool hasCustom = CustomRacePromptStore.AllCustom.ContainsKey(def.defName);
                string mark = hasCustom ? "● " : "";
                string label = mark + def.label;
                if (sel) GUI.color = Color.yellow;
                Widgets.Label(new Rect(4, y + 3, viewRect.width - 8, 24), label);
                GUI.color = Color.white;
                y += 30;
            }
            Widgets.EndScrollView();
        }

        private void DrawEditPanel(Rect rect)
        {
            Rect inner = rect.ContractedBy(8);

            if (selectedRace == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inner, "RAV_RaceEditor_SelectFirst".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // 种族名 + 状态标记
            bool hasCustom = CustomRacePromptStore.AllCustom.ContainsKey(selectedRace.defName);
            bool hasBuiltIn = RaceDescriptionMapper.HasBuiltInForDef(selectedRace);

            string statusKey = hasCustom ? "RAV_RaceEditor_StatusCustom" : (hasBuiltIn ? "RAV_RaceEditor_StatusBuiltIn" : "RAV_RaceEditor_StatusNone");
            GUI.color = hasCustom ? Color.green : (hasBuiltIn ? new Color(0.5f, 0.8f, 1f) : Color.gray);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22), selectedRace.label + "  [" + statusKey.Translate() + "]");
            GUI.color = Color.white;
            float y = inner.y + 26;

            // 内置参考（只读灰字）
            var builtIn = GetBuiltInForDef(selectedRace);
            if (builtIn != null)
            {
                GUI.color = new Color(0.55f, 0.55f, 0.55f);
                Text.Font = GameFont.Tiny;
                string refText = "RAV_RaceEditor_BuiltInRef".Translate() + "\n" + builtIn.tagForm;
                Rect refRect = new Rect(inner.x, y, inner.width, 58);
                Widgets.Label(refRect, refText);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 62;
            }
            else
            {
                GUI.color = new Color(0.55f, 0.55f, 0.55f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(inner.x, y, inner.width, 20), "RAV_RaceEditor_NoBuiltIn".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 24;
            }

            // 编辑框标题
            Widgets.Label(new Rect(inner.x, y, inner.width, 20), "RAV_RaceEditor_CustomLabel".Translate());
            y += 24;

            // 多行文本框
            Rect textRect = new Rect(inner.x, y, inner.width, inner.yMax - y);
            editingText = Widgets.TextArea(textRect, editingText);
        }

        private void SaveCurrent()
        {
            if (selectedRace == null) return;
            if (string.IsNullOrWhiteSpace(editingText))
            {
                Messages.Message("RAV_RaceEditor_EmptyWarn".Translate(selectedRace.label), MessageTypeDefOf.RejectInput);
                return;
            }
            CustomRacePromptStore.SetCustomByDefName(selectedRace.defName, editingText);
            Messages.Message("RAV_RaceEditor_Saved".Translate(selectedRace.label), MessageTypeDefOf.TaskCompletion);
        }

        private void ResetCurrent()
        {
            if (selectedRace == null) return;
            CustomRacePromptStore.SetCustomByDefName(selectedRace.defName, "");
            LoadEditingText();
            Messages.Message("RAV_RaceEditor_ResetDone".Translate(selectedRace.label), MessageTypeDefOf.NeutralEvent);
        }
    }
}
