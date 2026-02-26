using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    public class RimAiVinciMod : Mod
    {
        public static RimAiVinciSettings settings;
        private Vector2 scrollPos;

        public RimAiVinciMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RimAiVinciSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect topRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 60f);
            Rect bottomRect = new Rect(inRect.x, inRect.height - 50f, inRect.width, 40f);
            Rect viewRect = new Rect(0, 0, inRect.width - 16f, 1200f);

            Widgets.BeginScrollView(topRect, ref scrollPos, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            try
            {
                // === 顶部提示 ===
                GUI.color = Color.yellow;
                // 使用 Translate() 获取翻译
                listing.Label("RAV_Tip_SiliconFlow".Translate());
                GUI.color = Color.white;
                listing.GapLine();

                // === 1. API 设置 ===
                listing.Label("<b>" + "RAV_Group_API".Translate() + "</b>");
                GUI.color = Color.cyan;
                listing.Label("RAV_Desc_API".Translate());
                GUI.color = Color.white;
                listing.Gap(5);

                listing.Label("RAV_Label_APIKey".Translate());
                if (settings.apiKey == null) settings.apiKey = "";
                settings.apiKey = listing.TextEntry(settings.apiKey);

                listing.Label("RAV_Label_APIUrl".Translate());
                if (settings.apiUrl == null) settings.apiUrl = "";
                settings.apiUrl = listing.TextEntry(settings.apiUrl);
                listing.GapLine();

                // === 2. 模型设置 ===
                listing.Label("<b>" + "RAV_Group_Model".Translate() + "</b>");

                GUI.color = Color.gray;
                listing.Label("RAV_Desc_Txt2Img".Translate());
                GUI.color = Color.white;
                if (settings.modelName == null) settings.modelName = "";
                settings.modelName = listing.TextEntryLabeled("RAV_Label_Txt2Img".Translate(), settings.modelName);
                listing.Gap(5);

                GUI.color = Color.gray;
                listing.Label("RAV_Desc_Img2Img".Translate());
                GUI.color = Color.white;
                if (settings.modelNameI2I == null) settings.modelNameI2I = "";
                settings.modelNameI2I = listing.TextEntryLabeled("RAV_Label_Img2Img".Translate(), settings.modelNameI2I);

                listing.Gap(5);
                if (listing.ButtonText("RAV_Btn_Reset".Translate())) settings.ResetToDefault();
                listing.GapLine();

                // === 3. 强度设置 ===
                // 使用 Translate(args) 进行带参数的翻译
                listing.Label("<b>" + "RAV_Label_Strength".Translate(settings.i2iStrength.ToString("P0")) + "</b>");
                listing.Label("<color=grey>" + "RAV_Desc_Strength".Translate() + "</color>");
                settings.i2iStrength = listing.Slider(settings.i2iStrength, 0.1f, 1.0f);
                listing.GapLine();

                // === 4. UI 显示设置 ===
                listing.Label("<b>" + "RAV_Group_UI".Translate() + "</b>");
                listing.CheckboxLabeled("RAV_Check_ShowPortrait".Translate(), ref settings.showPortraitInInspectPane);

                // 立绘缩放滑动条
                if (settings.showPortraitInInspectPane)
                {
                    listing.Label("RAV_Label_PortraitScale".Translate(settings.portraitScale.ToString("P0")));
                    settings.portraitScale = listing.Slider(settings.portraitScale, 0.2f, 2.0f);
                }

                // 底部 Tab 开关
                bool oldTabSetting = settings.showBottomTab;
                listing.CheckboxLabeled("RAV_Check_ShowTab".Translate(), ref settings.showBottomTab);
                if (oldTabSetting != settings.showBottomTab)
                {
                    MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("RAV_GalleryTab");
                    if (def != null) def.buttonVisible = settings.showBottomTab;
                }
            }
            finally
            {
                listing.End();
                Widgets.EndScrollView();
            }

            // 5. 底部固定按钮
            float btnW = bottomRect.width / 2 - 10f;
            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, btnW, 30f), "RAV_Btn_OpenGallery".Translate()))
            {
                Application.OpenURL(ArtFileSystem.GetActualSaveDir());
            }
            if (Widgets.ButtonText(new Rect(bottomRect.x + btnW + 20f, bottomRect.y, btnW, 30f), "RAV_Btn_OpenFolder".Translate()))
            {
                Application.OpenURL(ArtFileSystem.GetImportDir());
            }

            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() => "Rim AiVinci";
    }
}