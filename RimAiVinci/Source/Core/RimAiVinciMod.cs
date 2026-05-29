using UnityEngine;
using Verse;
using RimWorld;
using System;
using System.Collections.Generic;

namespace RimAiVinci
{
    public class RimAiVinciMod : Mod
    {
        public static RimAiVinciSettings settings;
        private Vector2 scrollPos;
        private string testResult = "";
        private bool testSuccess = false;
        private bool isTesting = false;

        public RimAiVinciMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RimAiVinciSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float topBtnHeight = 36f;
            float topBtnW = (inRect.width - 10f) / 2f;
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y, topBtnW, topBtnHeight), "RAV_Btn_OpenGallery".Translate()))
            {
                Application.OpenURL(ArtFileSystem.GetActualSaveDir());
            }
            if (Widgets.ButtonText(new Rect(inRect.x + topBtnW + 10f, inRect.y, topBtnW, topBtnHeight), "RAV_Btn_OpenFolder".Translate()))
            {
                Application.OpenURL(ArtFileSystem.GetImportDir());
            }

            float scrollY = inRect.y + topBtnHeight + 4f;
            float scrollHeight = inRect.height - topBtnHeight - 4f;
            Rect topRect = new Rect(inRect.x, scrollY, inRect.width, scrollHeight);
            Rect viewRect = new Rect(0, 0, inRect.width - 16f, 1600f);

            Widgets.BeginScrollView(topRect, ref scrollPos, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            try
            {
                // === Player2 状态 ===
                listing.Label("<b>" + "RAV_Group_Player2".Translate() + "</b>");
                if (Player2Client.IsAvailable)
                {
                    GUI.color = Color.green;
                    listing.Label("RAV_Player2_Connected".Translate());
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = Color.red;
                    listing.Label("RAV_Player2_Disconnected".Translate());
                    GUI.color = Color.gray;
                    listing.Label("RAV_Player2_InstallTip".Translate());
                    GUI.color = Color.white;
                }
                if (listing.ButtonText("RAV_Player2_RetryConnect".Translate()))
                {
                    Player2Client.TryConnect();
                }
                listing.GapLine();

                // === 生图 API 设置 ===
                listing.Label("<b>" + "RAV_Group_ProAPI".Translate() + "</b>");
                GUI.color = Color.cyan;
                listing.Label("RAV_Desc_ProAPI".Translate());
                GUI.color = Color.white;
                listing.Gap(5);

                // 供应商下拉框
                Rect dropdownRect = listing.GetRect(30f);
                Widgets.Label(new Rect(dropdownRect.x, dropdownRect.y, 120f, 30f), "RAV_Label_Provider".Translate());
                Rect btnRect = new Rect(dropdownRect.x + 125f, dropdownRect.y, dropdownRect.width - 125f, 30f);
                string currentLabel = ApiProviders.TranslationKeys[settings.providerIndex].Translate();
                if (Widgets.ButtonText(btnRect, currentLabel))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    for (int i = 0; i < ApiProviders.Count; i++)
                    {
                        int idx = i;
                        options.Add(new FloatMenuOption(ApiProviders.TranslationKeys[idx].Translate(), () =>
                        {
                            int prev = settings.providerIndex;
                            settings.providerIndex = idx;
                                if (prev != idx)
                                {
                                    ApiProviders.ApplyProviderDefaults(idx);
                                    settings.apiKey = "";
                                    testResult = "";
                                    testSuccess = false;
                                }
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                listing.Gap(5);

                // 文档提示
                if (settings.providerIndex >= 0 && settings.providerIndex < ApiProviders.Count)
                {
                    GUI.color = Color.yellow;
                    listing.Label(ApiProviders.DocTips[settings.providerIndex].Translate());
                    GUI.color = Color.white;
                }
                listing.GapLine();

                // API Key
                if (!ApiProviders.IsLocalProvider(settings.providerIndex))
                {
                    listing.Label("RAV_Label_APIKey".Translate());
                    if (settings.apiKey == null) settings.apiKey = "";
                    settings.apiKey = listing.TextEntry(settings.apiKey);
                    listing.Gap(5);
                }

                // API URL
                listing.Label("RAV_Label_APIUrl".Translate());
                if (settings.apiUrl == null) settings.apiUrl = "";
                settings.apiUrl = listing.TextEntry(settings.apiUrl);
                listing.Gap(5);

                // 模型
                if (settings.providerIndex == ApiProviders.ComfyUI)
                {
                    Rect wfRect = listing.GetRect(30f);
                    Widgets.Label(new Rect(wfRect.x, wfRect.y, 150f, 30f), "RAV_Label_ComfyWorkflow".Translate());
                    Rect wfBtnRect = new Rect(wfRect.x + 155f, wfRect.y, wfRect.width - 155f, 30f);
                    string wfLabel = settings.comfyWorkflowMode == 1 ? "RAV_Comfy_Flux".Translate() : "RAV_Comfy_Standard".Translate();
                    if (Widgets.ButtonText(wfBtnRect, wfLabel))
                    {
                        List<FloatMenuOption> wfOpts = new List<FloatMenuOption>();
                        wfOpts.Add(new FloatMenuOption("RAV_Comfy_Standard".Translate(), () => { settings.comfyWorkflowMode = 0; }));
                        wfOpts.Add(new FloatMenuOption("RAV_Comfy_Flux".Translate(), () => { settings.comfyWorkflowMode = 1; }));
                        Find.WindowStack.Add(new FloatMenu(wfOpts));
                    }
                    listing.Gap(5);

                    if (settings.comfyWorkflowMode == 1)
                    {
                        listing.Label("RAV_Label_ComfyUNET".Translate());
                        if (settings.modelName == null) settings.modelName = "";
                        settings.modelName = listing.TextEntry(settings.modelName);
                        listing.Gap(3);

                        listing.Label("RAV_Label_ComfyCLIP".Translate());
                        if (settings.comfyFluxClipName == null) settings.comfyFluxClipName = "";
                        settings.comfyFluxClipName = listing.TextEntry(settings.comfyFluxClipName);
                        listing.Gap(3);

                        listing.Label("RAV_Label_ComfyVAE".Translate());
                        if (settings.comfyFluxVaeName == null) settings.comfyFluxVaeName = "";
                        settings.comfyFluxVaeName = listing.TextEntry(settings.comfyFluxVaeName);
                        listing.Gap(3);
                    }
                    else
                    {
                        listing.Label("RAV_Label_CheckpointName".Translate());
                        if (settings.modelName == null) settings.modelName = "";
                        settings.modelName = listing.TextEntry(settings.modelName);
                        listing.Gap(5);
                    }
                }
                else if (settings.providerIndex == ApiProviders.SDWebUI)
                {
                    listing.Label("RAV_Label_CheckpointName".Translate());
                    if (settings.modelName == null) settings.modelName = "";
                    settings.modelName = listing.TextEntry(settings.modelName);
                    listing.Gap(5);
                }
                else
                {
                    listing.Label("RAV_Label_Txt2ImgModel".Translate());
                    if (settings.modelName == null) settings.modelName = "";
                    settings.modelName = listing.TextEntry(settings.modelName);
                    listing.Gap(5);

                    listing.Label("RAV_Label_Img2ImgModel".Translate());
                    if (settings.modelNameI2I == null) settings.modelNameI2I = "";
                    settings.modelNameI2I = listing.TextEntry(settings.modelNameI2I);
                }

                // 测试连接
                listing.Gap(5);
                if (isTesting)
                {
                    GUI.color = Color.gray;
                    listing.Label("RAV_Test_Running".Translate());
                    GUI.color = Color.white;
                }
                else
                {
                    if (listing.ButtonText("RAV_Btn_TestConnection".Translate()))
                    {
                        isTesting = true;
                        testResult = "";
                        testSuccess = false;
                        Action<string, bool> onTestDone = (result, ok) =>
                        {
                            testResult = result;
                            testSuccess = ok;
                            isTesting = false;
                        };
                        if (settings.providerIndex == ApiProviders.ComfyUI)
                            ComfyUIClient.TestConnection(onTestDone);
                        else if (settings.providerIndex == ApiProviders.SDWebUI)
                            SDWebUIClient.TestConnection(onTestDone);
                        else
                            SiliconClient.TestConnection(onTestDone);
                    }
                }

                if (!string.IsNullOrEmpty(testResult))
                {
                    GUI.color = testSuccess ? Color.green : Color.red;
                    listing.Label(testResult);
                    GUI.color = Color.white;
                }

                listing.Gap(5);
                if (listing.ButtonText("RAV_Btn_Reset".Translate())) settings.ResetToDefault();
                listing.GapLine();

                // === 图生图强度 ===
                listing.Label("<b>" + "RAV_Label_Strength".Translate(settings.i2iStrength.ToString("P0")) + "</b>");
                listing.Label("<color=grey>" + "RAV_Desc_Strength".Translate() + "</color>");
                settings.i2iStrength = listing.Slider(settings.i2iStrength, 0.1f, 1.0f);
                listing.GapLine();

                // === UI 显示 ===
                listing.Label("<b>" + "RAV_Group_UI".Translate() + "</b>");
                listing.CheckboxLabeled("RAV_Check_ShowPortrait".Translate(), ref settings.showPortraitInInspectPane);
                if (settings.showPortraitInInspectPane)
                {
                    listing.Label("RAV_Label_PortraitScale".Translate(settings.portraitScale.ToString("P0")));
                    settings.portraitScale = listing.Slider(settings.portraitScale, 0.2f, 2.0f);
                }
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

            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory() => "Rim AiVinci";
    }
}
