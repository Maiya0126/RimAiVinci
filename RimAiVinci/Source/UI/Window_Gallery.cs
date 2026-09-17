using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace RimAiVinci
{
    public class Window_Gallery : MainTabWindow
    {
        private ArtDataStore dataStore;
        private List<Pawn> allPawns = new List<Pawn>();
        private List<Pawn> displayPawns = new List<Pawn>();

        private Pawn selectedPawn = null;
        private List<ArtData> currentGallery = new List<ArtData>();

        private Vector2 scrollPosLeft = Vector2.zero;
        private Vector2 scrollPosRight = Vector2.zero;
        private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

        private string currentActivePath = null;
        private string searchKeyword = "";
        private int selectedTab = 0;
        private bool showAllPawns = false;
        private bool showDeadPawns = false;
        private static readonly string[] TabKeys = { "RAV_Gallery_TabHuman", "RAV_Gallery_TabAnimal", "RAV_Gallery_TabMechanoid" };
        private static readonly Color TabSelectedBg = new Color(0.3f, 0.3f, 0.2f, 0.6f);
        private static readonly Color TabSelectedText = new Color(1f, 0.85f, 0.3f);

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public Window_Gallery() { this.doCloseX = true; this.doCloseButton = false; this.draggable = true; this.resizeable = true; }
        public override void PreOpen() { base.PreOpen(); RefreshData(); }
        public override void PostClose() { base.PostClose(); ClearCache(); }
        private void ClearCache() { foreach (var tex in textureCache.Values) if (tex != null) UnityEngine.Object.Destroy(tex); textureCache.Clear(); }

        private PawnPortraitType CurrentFilterType
        {
            get
            {
                switch (selectedTab)
                {
                    case 1: return PawnPortraitType.Animal;
                    case 2: return PawnPortraitType.Mechanoid;
                    default: return PawnPortraitType.Human;
                }
            }
        }

        private void RefreshData()
        {
            dataStore = Find.World.GetComponent<ArtDataStore>();
            allPawns.Clear();
            HashSet<int> added = new HashSet<int>();

            if (Find.CurrentMap != null)
            {
                var mapPawns = Find.CurrentMap.mapPawns;
                if (showAllPawns)
                {
                    foreach (Pawn p in mapPawns.AllPawns)
                    {
                        if (added.Add(p.thingIDNumber))
                            allPawns.Add(p);
                    }
                }
                else
                {
                    foreach (Pawn p in mapPawns.FreeColonistsAndPrisoners)
                    {
                        if (added.Add(p.thingIDNumber))
                            allPawns.Add(p);
                    }
                    foreach (Pawn p in mapPawns.AllPawns)
                    {
                        if (PawnPortraitTypeHelper.GetPawnType(p) != PawnPortraitType.Human &&
                            (p.Faction == Faction.OfPlayer || p.Faction == null) &&
                            added.Add(p.thingIDNumber))
                            allPawns.Add(p);
                    }
                }
            }

            if (showAllPawns)
            {
                foreach (Pawn p in Find.World.worldPawns.AllPawnsAlive)
                {
                    if (added.Add(p.thingIDNumber))
                        allPawns.Add(p);
                }
            }

            if (showDeadPawns)
            {
                foreach (Pawn dp in DeadPawnFinder.GetDeadColonists())
                {
                    if (added.Add(dp.thingIDNumber))
                        allPawns.Add(dp);
                }
            }

            allPawns.Sort((a, b) =>
            {
                string na = a.Name != null ? a.Name.ToStringShort : a.def.label;
                string nb = b.Name != null ? b.Name.ToStringShort : b.def.label;
                return na.CompareTo(nb);
            });

            UpdateDisplayList();

            if (selectedPawn == null && displayPawns.Count > 0) SelectPawn(displayPawns[0]);
            else if (selectedPawn != null && !allPawns.Any(p => p.thingIDNumber == selectedPawn.thingIDNumber)) selectedPawn = displayPawns.Count > 0 ? displayPawns[0] : null;

            RefreshCurrentGalleryList();
        }

        private void UpdateDisplayList()
        {
            displayPawns.Clear();
            var filterType = CurrentFilterType;
            var filtered = allPawns.Where(p => PawnPortraitTypeHelper.GetPawnType(p) == filterType);

            if (string.IsNullOrEmpty(searchKeyword))
            {
                displayPawns.AddRange(filtered);
            }
            else
            {
                string key = searchKeyword.ToLower();
                displayPawns.AddRange(filtered.Where(p =>
                {
                    string name = p.Name != null ? p.Name.ToStringShort.ToLower() : p.def.label.ToLower();
                    return name.Contains(key);
                }));
            }
        }

        private void SelectPawn(Pawn p)
        {
            selectedPawn = p;
            RefreshCurrentGalleryList();
        }

        private void RefreshCurrentGalleryList()
        {
            if (selectedPawn != null && dataStore != null)
            {
                currentGallery = dataStore.GetArtForPawn(selectedPawn);
                currentActivePath = dataStore.GetActivePortraitPath(selectedPawn);
            }
            else
            {
                currentGallery = new List<ArtData>();
                currentActivePath = null;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (selectedPawn != null && dataStore != null)
            {
                int actualCount = dataStore.GetArtForPawn(selectedPawn).Count;
                if (actualCount != currentGallery.Count) RefreshCurrentGalleryList();
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, 300, 30), "RAV_Gallery_Title".Translate());
            Text.Font = GameFont.Small;

            float topY = 30f;
            float tabWidth = inRect.width / TabKeys.Length;
            for (int i = 0; i < TabKeys.Length; i++)
            {
                Rect tabRect = new Rect(i * tabWidth, topY, tabWidth, 28f);
                bool isSelected = (i == selectedTab);

                if (isSelected)
                {
                    Widgets.DrawBoxSolid(tabRect, TabSelectedBg);
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(tabRect);
                }

                if (Widgets.ButtonInvisible(tabRect))
                {
                    if (selectedTab != i)
                    {
                        selectedTab = i;
                        selectedPawn = null;
                        UpdateDisplayList();
                        if (displayPawns.Count > 0) SelectPawn(displayPawns[0]);
                    }
                }

                GUI.color = isSelected ? TabSelectedText : Color.white;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(tabRect, TabKeys[i].Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            topY += 30f;

            float btnHeight = 28f;
            float rightX = inRect.width;

            Rect scanRect = new Rect(rightX - 180, topY, 180, btnHeight);
            rightX -= 185;
            if (Widgets.ButtonText(scanRect, "RAV_Gallery_ScanRecover".Translate()))
            {
                int count = ArtFileSystem.ScanAndReconcile(dataStore);
                if (count > 0) { Messages.Message("RAV_Gallery_Recovered".Translate(count), MessageTypeDefOf.TaskCompletion); RefreshData(); }
                else Messages.Message("RAV_Gallery_NoLost".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            Rect folderRect = new Rect(rightX - 170, topY, 170, btnHeight);
            rightX -= 175;
            if (Widgets.ButtonText(folderRect, "RAV_Btn_OpenGallery".Translate())) Application.OpenURL(ArtFileSystem.GetActualSaveDir());

            string filterLabel = showAllPawns ? "RAV_Gallery_ShowColony".Translate() : "RAV_Gallery_ShowAll".Translate();
            Rect filterRect = new Rect(rightX - 160, topY, 160, btnHeight);
            rightX -= 165;
            if (Widgets.ButtonText(filterRect, filterLabel))
            {
                showAllPawns = !showAllPawns;
                RefreshData();
            }

            string deadLabel = showDeadPawns ? "RAV_Gallery_OnlyAlive".Translate() : "RAV_Gallery_ShowDead".Translate();
            Rect deadRect = new Rect(rightX - 130, topY, 130, btnHeight);
            rightX -= 135;
            if (Widgets.ButtonText(deadRect, deadLabel))
            {
                showDeadPawns = !showDeadPawns;
                RefreshData();
            }

            topY += 34f;

            float margin = 10f;
            float leftWidth = 200f;
            float contentHeight = inRect.height - topY;
            Rect rectLeft = new Rect(0, topY, leftWidth, contentHeight);
            DrawPawnList(rectLeft);

            Rect rectRight = new Rect(leftWidth + margin, topY, inRect.width - leftWidth - margin, contentHeight);
            DrawGalleryGrid(rectRight);
        }

        private void DrawPawnList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5);

            Rect searchRect = new Rect(inner.x, inner.y, inner.width, 24f);
            string oldSearch = searchKeyword;
            searchKeyword = Widgets.TextField(searchRect, searchKeyword);
            if (searchKeyword != oldSearch) UpdateDisplayList();

            float listY = inner.y + 30f;
            float listHeight = inner.height - 30f;
            Rect listRect = new Rect(inner.x, listY, inner.width, listHeight);

            Rect viewRect = new Rect(0, 0, listRect.width - 16, displayPawns.Count * 35);
            Widgets.BeginScrollView(listRect, ref scrollPosLeft, viewRect);

            float y = 0;
            foreach (Pawn p in displayPawns)
            {
                Rect rowRect = new Rect(0, y, viewRect.width, 30);
                bool isSelected = (selectedPawn == p);
                if (isSelected) Widgets.DrawHighlightSelected(rowRect);
                else Widgets.DrawHighlightIfMouseover(rowRect);

                if (Widgets.ButtonInvisible(rowRect)) SelectPawn(p);

                Rect nameRect = new Rect(5, y, viewRect.width, 30);
                Text.Anchor = TextAnchor.MiddleLeft;

                if (isSelected) GUI.color = Color.yellow;
                string displayName = (p.Dead ? "† " : "") + (p.Name != null ? p.Name.ToStringShort : p.def.label);
                if (p.Dead) GUI.color = new Color(1f, 0.5f, 0.5f);
                Widgets.Label(nameRect, displayName);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.UpperLeft;
                y += 35;
            }
            Widgets.EndScrollView();
        }

        private void DrawGalleryGrid(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            float topBarHeight = 40f;
            Rect topBarRect = new Rect(rect.x + 5, rect.y + 5, rect.width - 10, 30);

            if (selectedPawn != null)
            {
                string pawnLabel = selectedPawn.Name != null ? selectedPawn.Name.ToStringShort : selectedPawn.def.label;
                string btnLabel = "RAV_Gallery_CreateNew".Translate(pawnLabel);
                if (Widgets.ButtonText(topBarRect, btnLabel)) Find.WindowStack.Add(new Window_ArtCreator(selectedPawn));
            }
            else
            {
                Widgets.Label(topBarRect, "RAV_Gallery_SelectPawn".Translate());
            }

            Rect gridRect = new Rect(rect.x, rect.y + topBarHeight, rect.width, rect.height - topBarHeight);
            if (currentGallery == null || currentGallery.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(gridRect, "RAV_Gallery_NoArt".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float cellWidth = 160f;
            float cellHeight = 262f;
            int columns = Mathf.FloorToInt((gridRect.width - 20) / cellWidth);
            if (columns < 1) columns = 1;
            int rows = Mathf.CeilToInt((float)currentGallery.Count / columns);
            Rect viewRect = new Rect(0, 0, gridRect.width - 16, rows * cellHeight);

            Widgets.BeginScrollView(gridRect, ref scrollPosRight, viewRect);
            for (int i = 0; i < currentGallery.Count; i++)
            {
                ArtData art = currentGallery[i];
                int col = i % columns;
                int row = i / columns;
                Rect cellRect = new Rect(col * cellWidth + 5, row * cellHeight + 5, 150, 250);
                DrawArtCell(cellRect, art);
            }
            Widgets.EndScrollView();
        }

        private void DrawArtCell(Rect rect, ArtData art)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f));
            Widgets.DrawHighlightIfMouseover(rect);

            Texture2D tex = null;
            if (textureCache.ContainsKey(art.relativePath)) tex = textureCache[art.relativePath];
            else { tex = ArtFileSystem.LoadTextureFromDisk(art.relativePath); if (tex != null) textureCache[art.relativePath] = tex; }

            Rect imgRect = new Rect(rect.x + 5, rect.y + 5, 140, 140);
            bool isActive = (art.relativePath == currentActivePath);

            if (isActive)
            {
                Widgets.DrawBoxSolidWithOutline(rect, new Color(0, 0, 0, 0), new Color(1f, 0.8f, 0f), 2);
                GUI.color = new Color(1f, 0.8f, 0f);
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(rect.x, rect.y - 2, 150, 20), "★ " + "RAV_Gallery_Active".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            if (art.isMemorial)
            {
                GUI.color = new Color(0.9f, 0.5f, 0.5f);
                Text.Anchor = TextAnchor.UpperCenter;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rect.x, rect.y + 16, 150, 20), "RAV_ArtCreator_Memorial".Translate());
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            if (tex != null)
            {
                GUI.DrawTexture(imgRect, tex, ScaleMode.ScaleToFit);
                if (Widgets.ButtonInvisible(imgRect)) Find.WindowStack.Add(new Dialog_ShowImage(tex));
            }
            else Widgets.Label(imgRect, "RAV_Gallery_FileLost".Translate());

            float btnY = rect.y + 150;

            if (isActive)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + 5, btnY, 140, 24), "RAV_Gallery_InUse".Translate());
                GUI.color = Color.white;
            }
            else
            {
                if (Widgets.ButtonText(new Rect(rect.x + 5, btnY, 140, 24), "RAV_Gallery_SetActive".Translate()))
                {
                    dataStore.SetActivePortrait(selectedPawn.ThingID, art.relativePath);
                    currentActivePath = art.relativePath;
                    Messages.Message("RAV_Gallery_PortraitUpdated".Translate(), MessageTypeDefOf.TaskCompletion);
                }
            }

            Rect deleteRect = new Rect(rect.x + 5, btnY + 30, 140, 24);
            if (Widgets.ButtonText(deleteRect, "RAV_Gallery_Delete".Translate(), true, true, true)) DeleteArt(art);

            if (PawnPortraitTypeHelper.CanAIGenerate(selectedPawn))
            {
                Rect stateRect = new Rect(rect.x + 5, btnY + 58, 140, 22);
                if (Widgets.ButtonText(stateRect, "RAV_Gallery_SetState".Translate())) OpenStateMenu(art);
            }
        }

        private void OpenStateMenu(ArtData art)
        {
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (PortraitState st in System.Enum.GetValues(typeof(PortraitState)))
            {
                PortraitState state = st;
                bool assigned = dataStore.GetStatesFor(selectedPawn.ThingID, art.relativePath).Contains(state);
                string label = (assigned ? "● " : "") + PortraitStateHelper.GetStateLabelKey(state).Translate();
                opts.Add(new FloatMenuOption(label, () =>
                {
                    dataStore.SetStatePortrait(selectedPawn.ThingID, state, art.relativePath);
                    Messages.Message("RAV_Gallery_PortraitUpdated".Translate(), MessageTypeDefOf.TaskCompletion);
                }));
            }
            opts.Add(new FloatMenuOption("RAV_State_ClearAll".Translate(), () => dataStore.ClearAllStatesFor(selectedPawn.ThingID)));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void DeleteArt(ArtData art)
        {
            if (textureCache.ContainsKey(art.relativePath)) { UnityEngine.Object.Destroy(textureCache[art.relativePath]); textureCache.Remove(art.relativePath); }
            string fullPath = Path.Combine(ArtFileSystem.GetActualSaveDir(), art.relativePath);
            if (File.Exists(fullPath)) { try { File.Delete(fullPath); } catch { } }

            Find.World.GetComponent<ArtDataStore>().RemoveArt(art);
            RefreshData();
        }
    }
}
