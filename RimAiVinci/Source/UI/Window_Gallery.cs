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
        private List<Pawn> allColonists = new List<Pawn>(); // 所有的殖民者
        private List<Pawn> displayColonists = new List<Pawn>(); // ✨ 用于显示的（过滤后的）殖民者列表

        private Pawn selectedPawn = null;
        private List<ArtData> currentGallery = new List<ArtData>();

        private Vector2 scrollPosLeft = Vector2.zero;
        private Vector2 scrollPosRight = Vector2.zero;
        private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();

        // 缓存当前的激活路径，避免每帧去查字典
        private string currentActivePath = null;

        // ✨✨✨ 新增：搜索关键词 ✨✨✨
        private string searchKeyword = "";

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public Window_Gallery() { this.doCloseX = true; this.doCloseButton = false; this.draggable = true; this.resizeable = true; }
        public override void PreOpen() { base.PreOpen(); RefreshData(); }
        public override void PostClose() { base.PostClose(); ClearCache(); }
        private void ClearCache() { foreach (var tex in textureCache.Values) if (tex != null) UnityEngine.Object.Destroy(tex); textureCache.Clear(); }

        private void RefreshData()
        {
            dataStore = Find.World.GetComponent<ArtDataStore>();
            allColonists.Clear();
            if (Find.CurrentMap != null)
            {
                // ✨ 优化：按名字排序，找人更方便
                allColonists.AddRange(Find.CurrentMap.mapPawns.FreeColonists.OrderBy(p => p.Name.ToStringShort));
            }

            // 初始化显示列表（未过滤状态）
            UpdateDisplayList();

            // 选中逻辑
            if (selectedPawn == null && displayColonists.Count > 0) SelectPawn(displayColonists[0]);
            else if (selectedPawn != null && !allColonists.Contains(selectedPawn)) selectedPawn = displayColonists.Count > 0 ? displayColonists[0] : null;

            RefreshCurrentGalleryList();
        }

        // ✨✨✨ 新增：根据搜索词更新显示列表 ✨✨✨
        private void UpdateDisplayList()
        {
            displayColonists.Clear();
            if (string.IsNullOrEmpty(searchKeyword))
            {
                displayColonists.AddRange(allColonists);
            }
            else
            {
                string key = searchKeyword.ToLower();
                // 模糊搜索：名字包含关键词即可
                displayColonists.AddRange(allColonists.Where(p => p.Name.ToStringShort.ToLower().Contains(key)));
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
            Widgets.Label(new Rect(0, 0, 300, 30), "AiVinci 艺术画廊");
            Text.Font = GameFont.Small;

            float btnHeight = 30f; float scanBtnWidth = 180f; float folderBtnWidth = 160f; float gap = 10f;
            Rect scanRect = new Rect(inRect.width - scanBtnWidth, 0, scanBtnWidth, btnHeight);
            if (Widgets.ButtonText(scanRect, "🔄 扫描找回丢失画像"))
            {
                int count = ArtFileSystem.ScanAndReconcile(dataStore);
                if (count > 0) { Messages.Message($"找回了 {count} 张画像。", MessageTypeDefOf.TaskCompletion); RefreshData(); }
                else Messages.Message("本地未发现丢失的画像记录。", MessageTypeDefOf.NeutralEvent);
            }

            Rect folderRect = new Rect(inRect.width - scanBtnWidth - gap - folderBtnWidth, 0, folderBtnWidth, btnHeight);
            if (Widgets.ButtonText(folderRect, "📂 打开珍藏目录")) Application.OpenURL(ArtFileSystem.GetActualSaveDir());

            float margin = 10f; float leftWidth = 200f;
            Rect rectLeft = new Rect(0, 40, leftWidth, inRect.height - 50);
            DrawPawnList(rectLeft); // 绘制左侧列表

            Rect rectRight = new Rect(leftWidth + margin, 40, inRect.width - leftWidth - margin, inRect.height - 50);
            DrawGalleryGrid(rectRight); // 绘制右侧画廊
        }

        private void DrawPawnList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(5);

            // ✨✨✨ 1. 绘制搜索框 ✨✨✨
            Rect searchRect = new Rect(inner.x, inner.y, inner.width, 24f);
            string oldSearch = searchKeyword;
            searchKeyword = Widgets.TextField(searchRect, searchKeyword);
            // 如果搜索词变了，刷新列表
            if (searchKeyword != oldSearch) UpdateDisplayList();

            // ✨✨✨ 2. 绘制列表 (下移，避开搜索框) ✨✨✨
            float listY = inner.y + 30f;
            float listHeight = inner.height - 30f;
            Rect listRect = new Rect(inner.x, listY, inner.width, listHeight);

            Rect viewRect = new Rect(0, 0, listRect.width - 16, displayColonists.Count * 35);
            Widgets.BeginScrollView(listRect, ref scrollPosLeft, viewRect);

            float y = 0;
            // 注意：这里遍历的是 displayColonists (过滤后的列表)
            foreach (Pawn p in displayColonists)
            {
                Rect rowRect = new Rect(0, y, viewRect.width, 30);
                if (selectedPawn == p) Widgets.DrawHighlightSelected(rowRect);
                else Widgets.DrawHighlightIfMouseover(rowRect);

                if (Widgets.ButtonInvisible(rowRect)) SelectPawn(p);

                Rect nameRect = new Rect(5, y, viewRect.width, 30);
                Text.Anchor = TextAnchor.MiddleLeft;

                // 给名字加个颜色区分 (选中时高亮)
                if (selectedPawn == p) GUI.color = Color.yellow;
                Widgets.Label(nameRect, p.Name.ToStringShort);
                GUI.color = Color.white;

                Text.Anchor = TextAnchor.UpperLeft;
                y += 35;
            }
            Widgets.EndScrollView();
        }

        private void DrawGalleryGrid(Rect rect)
        {
            Widgets.DrawMenuSection(rect); float topBarHeight = 40f; Rect topBarRect = new Rect(rect.x + 5, rect.y + 5, rect.width - 10, 30);
            if (selectedPawn != null) { if (Widgets.ButtonText(topBarRect, $"🎨 为 {selectedPawn.Name.ToStringShort} 创作新画像 (Create New)")) Find.WindowStack.Add(new Window_ArtCreator(selectedPawn)); }
            else Widgets.Label(topBarRect, "请先在左侧选择一位殖民者...");

            Rect gridRect = new Rect(rect.x, rect.y + topBarHeight, rect.width, rect.height - topBarHeight);
            if (currentGallery == null || currentGallery.Count == 0) { Text.Anchor = TextAnchor.MiddleCenter; Widgets.Label(gridRect, "暂无画像，点击上方按钮开始创作！"); Text.Anchor = TextAnchor.UpperLeft; return; }

            float cellWidth = 160f;
            float cellHeight = 230f;
            int columns = Mathf.FloorToInt((gridRect.width - 20) / cellWidth); if (columns < 1) columns = 1;
            int rows = Mathf.CeilToInt((float)currentGallery.Count / columns);
            Rect viewRect = new Rect(0, 0, gridRect.width - 16, rows * cellHeight);

            Widgets.BeginScrollView(gridRect, ref scrollPosRight, viewRect);
            for (int i = 0; i < currentGallery.Count; i++)
            {
                ArtData art = currentGallery[i]; int col = i % columns; int row = i / columns;
                Rect cellRect = new Rect(col * cellWidth + 5, row * cellHeight + 5, 150, 220);
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
                Widgets.Label(new Rect(rect.x, rect.y - 2, 150, 20), "★ 展示中 (Active)");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            if (tex != null)
            {
                GUI.DrawTexture(imgRect, tex, ScaleMode.ScaleToFit);
                if (Widgets.ButtonInvisible(imgRect)) Find.WindowStack.Add(new Dialog_ShowImage(tex));
            }
            else Widgets.Label(imgRect, "文件丢失");

            float btnY = rect.y + 150;

            if (isActive)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x + 5, btnY, 140, 24), "当前使用中");
                GUI.color = Color.white;
            }
            else
            {
                if (Widgets.ButtonText(new Rect(rect.x + 5, btnY, 140, 24), "设为展示立绘"))
                {
                    dataStore.SetActivePortrait(selectedPawn.ThingID, art.relativePath);
                    currentActivePath = art.relativePath;
                    Messages.Message("已更新展示画像！", MessageTypeDefOf.TaskCompletion);
                }
            }

            Rect deleteRect = new Rect(rect.x + 5, btnY + 30, 140, 24);
            if (Widgets.ButtonText(deleteRect, "删除", true, true, true)) DeleteArt(art);
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