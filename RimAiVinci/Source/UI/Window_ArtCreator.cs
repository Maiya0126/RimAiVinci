using UnityEngine;
using Verse;
using RimWorld;
using Verse.Sound;
using System.Collections.Generic;
using System.IO;

namespace RimAiVinci
{
    public enum GenerationMode { TextToImage, ImageToImage }

    public class Window_ArtCreator : Window
    {
        private Pawn targetPawn;
        private PromptOptions options = new PromptOptions();
        private Texture2D generatedTexture = null;
        private Texture2D referenceTexture = null;
        private Texture2D uploadedTexture = null;
        private bool isGenerating = false;
        private bool hasSavedCurrent = false;
        private GenerationMode currentMode = GenerationMode.TextToImage;
        private ArtStyle lastStyle = ArtStyle.Standard;
        private PortraitState? targetState = null;

        private Vector2 scrollPosRight = Vector2.zero;
        private Rot4 currentRotation = Rot4.South;

        public override Vector2 InitialSize => new Vector2(950f, 950f);

        public Window_ArtCreator(Pawn p)
        {
            this.targetPawn = p;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.draggable = true;
            this.resizeable = false;
        }

        public Window_ArtCreator(Pawn p, PortraitState state)
        {
            this.targetPawn = p;
            this.targetState = state;
            // 状态描述词预填到额外细节，玩家可直接编辑（如自定义受伤位置）
            string fragment = PortraitStateHelper.GetStatePromptFragment(state);
            if (!string.IsNullOrEmpty(fragment)) options.extraPrompt = fragment;
            this.doCloseButton = false;
            this.doCloseX = true;
            this.draggable = true;
            this.resizeable = false;
        }

        private string BuildFinalPrompt()
        {
            return PawnPromptBuilder.BuildPromptFromPawn(targetPawn, options);
        }

        public override void PostClose()
        {
            base.PostClose();
            if (generatedTexture != null) { UnityEngine.Object.Destroy(generatedTexture); generatedTexture = null; }
            if (referenceTexture != null) { UnityEngine.Object.Destroy(referenceTexture); referenceTexture = null; }
            if (uploadedTexture != null) { UnityEngine.Object.Destroy(uploadedTexture); uploadedTexture = null; }
        }

        private bool IsUploadOnlyMode => !PawnPortraitTypeHelper.CanAIGenerate(targetPawn);
        private bool IsDeadPawn => targetPawn != null && targetPawn.Dead;

        private Texture2D LoadMemorialTexture()
        {
            var store = Find.World.GetComponent<ArtDataStore>();
            var arts = store?.GetArtForPawn(targetPawn);
            if (arts == null || arts.Count == 0) return null;
            for (int i = arts.Count - 1; i >= 0; i--)
            {
                Texture2D t = ArtFileSystem.LoadTextureFromDisk(arts[i].relativePath);
                if (t != null) return t;
            }
            return null;
        }

        public override void DoWindowContents(Rect inRect)
        {
            string pawnName = targetPawn.Name != null ? targetPawn.Name.ToStringShort : targetPawn.def.label;

            if (IsUploadOnlyMode)
            {
                DrawUploadOnlyWindow(inRect);
                return;
            }

            if (options.style != lastStyle)
            {
                if (options.style == ArtStyle.Custom)
                {
                    options.readAge = false; options.readRace = false; options.readApparel = false;
                    options.readHair = false; options.readTraits = false; options.readBackground = false;
                    options.readHealth = false; options.readQuality = false; options.readSkinColor = false;
                    options.readBodyType = false;
                }
                lastStyle = options.style;
            }

            Rect headerRect = new Rect(0, 0, inRect.width, 40);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, "RAV_ArtCreator_Title".Translate(pawnName));
            Text.Font = GameFont.Small;

            float modeBtnWidth = 150f;
            Rect btnModeTxt = new Rect(inRect.width - modeBtnWidth * 2 - 10, 0, modeBtnWidth, 30);
            Rect btnModeImg = new Rect(inRect.width - modeBtnWidth, 0, modeBtnWidth, 30);

            if (currentMode == GenerationMode.TextToImage) GUI.color = Color.green;
            if (Widgets.ButtonText(btnModeTxt, "RAV_Mode_Txt2Img".Translate()))
            {
                currentMode = GenerationMode.TextToImage;
                if (options.style == ArtStyle.CharacterSheet) options.style = ArtStyle.Standard;
            }
            GUI.color = Color.white;

            if (currentMode == GenerationMode.ImageToImage) GUI.color = Color.green;
            if (Widgets.ButtonText(btnModeImg, "RAV_Mode_Img2Img".Translate()))
            {
                currentMode = GenerationMode.ImageToImage;
                options.isImg2ImgMode = true;
            }
            else { options.isImg2ImgMode = false; }
            GUI.color = Color.white;

            float margin = 10f;
            float leftWidth = 350f;
            float bottomHeight = 60f;
            float panelHeight = inRect.height - 50 - bottomHeight;

            Rect rectLeft = new Rect(0, 50, leftWidth, panelHeight);
            Rect rectRight = new Rect(leftWidth + margin, 50, inRect.width - leftWidth - margin, panelHeight);

            DrawLeftPanel(rectLeft);
            DrawRightPanel(rectRight);
            DrawBottomButtons(inRect, rectRight.x);
        }

        private void DrawLeftPanel(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect imgRect = rect.ContractedBy(10); imgRect.height = imgRect.width;

            Texture2D displayTex = generatedTexture;
            bool showLivePreview = (displayTex == null);
            if (currentMode == GenerationMode.ImageToImage && referenceTexture != null && displayTex == null)
            {
                displayTex = referenceTexture;
                showLivePreview = false;
            }
            if (IsDeadPawn) showLivePreview = false;
            if (IsDeadPawn && displayTex == null) displayTex = LoadMemorialTexture();

            if (showLivePreview)
            {
                RenderTexture portrait = PortraitsCache.Get(targetPawn, new Vector2(512, 512), currentRotation);
                GUI.DrawTexture(imgRect, portrait, ScaleMode.ScaleToFit);

                if (!isGenerating)
                {
                    Rect btnRotLeft = new Rect(imgRect.x + 5, imgRect.y + 5, 24, 24);
                    Rect btnRotRight = new Rect(imgRect.xMax - 29, imgRect.y + 5, 24, 24);

                    if (Widgets.ButtonImage(btnRotLeft, TexUI.RotLeftTex))
                    {
                        currentRotation.Rotate(RotationDirection.Counterclockwise);
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    }
                    if (Widgets.ButtonImage(btnRotRight, TexUI.RotRightTex))
                    {
                        currentRotation.Rotate(RotationDirection.Clockwise);
                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    }
                }
            }
            else
            {
                Widgets.DrawTextureFitted(imgRect, displayTex, 1.0f);
            }

            if (isGenerating) { Widgets.DrawBoxSolid(imgRect, new Color(0, 0, 0, 0.5f)); Text.Anchor = TextAnchor.MiddleCenter; Widgets.Label(imgRect, "RAV_ArtCreator_Generating".Translate()); Text.Anchor = TextAnchor.UpperLeft; }

            float y = imgRect.yMax + 6;

            int provIdx = RimAiVinciMod.settings.providerIndex;
            bool isLocal = ApiProviders.IsLocalProvider(provIdx);
            string provName = ApiProviders.TranslationKeys[provIdx].Translate();
            float sw = rect.width - 20;

            GUI.color = new Color(0.7f, 0.85f, 1f);
            Widgets.Label(new Rect(rect.x + 10, y, sw, 18), "RAV_ArtCreator_Provider".Translate() + " " + provName);
            GUI.color = Color.white;
            y += 22;

            string currentModel = currentMode == GenerationMode.ImageToImage ? RimAiVinciMod.settings.modelNameI2I : RimAiVinciMod.settings.modelName;
            string modelLabel = isLocal ? "RAV_Label_ModelFile".Translate() : "RAV_Label_ModelName".Translate();
            if (provIdx == ApiProviders.ComfyUI && RimAiVinciMod.settings.comfyWorkflowMode == 1) modelLabel = "RAV_Label_ComfyUNET".Translate();
            Widgets.Label(new Rect(rect.x + 10, y, sw, 18), modelLabel);
            y += 18;
            string modelDisplay = currentModel;
            if (string.IsNullOrEmpty(modelDisplay)) modelDisplay = isLocal ? "" : "Flux";
            if (modelDisplay.Length > 28) modelDisplay = modelDisplay.Substring(0, 26) + "..";
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(rect.x + 10, y, sw, 18), modelDisplay);
            Text.Font = GameFont.Small;
            y += 20;

            if (currentMode == GenerationMode.ImageToImage && !isGenerating)
            {
                if (referenceTexture != null)
                {
                    if (Widgets.ButtonText(new Rect(rect.x + 10, y, rect.width - 20, 26), "RAV_ArtCreator_ResetReference".Translate()))
                    {
                        referenceTexture = null;
                        generatedTexture = null;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
                else
                {
                    if (Widgets.ButtonText(new Rect(rect.x + 10, y, rect.width - 20, 26), "RAV_ArtCreator_SetReference".Translate()))
                    {
                        if (IsDeadPawn)
                        {
                            Texture2D memorial = LoadMemorialTexture();
                            if (memorial != null)
                            {
                                SetReferenceImage(PadToSquare1024(memorial));
                                UnityEngine.Object.Destroy(memorial);
                            }
                            else
                                Messages.Message("RAV_Gallery_FileLost".Translate(), MessageTypeDefOf.RejectInput);
                        }
                        else
                        {
                            RenderTexture rt = PortraitsCache.Get(targetPawn, new Vector2(512, 512), currentRotation);
                            Texture2D rawTex = AdjustRenderTextureToTexture2D(rt);
                            SetReferenceImage(PadToSquare1024(rawTex));
                            UnityEngine.Object.Destroy(rawTex);
                        }
                        generatedTexture = null;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
                y += 30;

                if (Widgets.ButtonText(new Rect(rect.x + 10, y, rect.width - 20, 26), "RAV_ArtCreator_PastePath".Translate())) { PasteFromClipboard(); generatedTexture = null; }
                y += 30;

                if (Widgets.ButtonText(new Rect(rect.x + 10, y, (rect.width - 25) / 2, 26), "RAV_Btn_OpenFolder".Translate())) Application.OpenURL(ArtFileSystem.GetImportDir());
                if (Widgets.ButtonText(new Rect(rect.x + 15 + (rect.width - 25) / 2, y, (rect.width - 25) / 2, 26), "RAV_ArtCreator_PickFile".Translate())) OpenImportFloatMenu();
                y += 32;

                Widgets.Label(new Rect(rect.x + 10, y, sw, 18), "RAV_Label_Strength".Translate(RimAiVinciMod.settings.i2iStrength.ToString("P0")));
                y += 20;
                RimAiVinciMod.settings.i2iStrength = Widgets.HorizontalSlider(new Rect(rect.x + 10, y, sw, 18), RimAiVinciMod.settings.i2iStrength, 0.1f, 1.0f);
                y += 24;
            }
        }

        private void DrawRightPanel(Rect rect)
        {
            Rect viewRect = new Rect(0, 0, rect.width - 16, 1100f);
            Widgets.BeginScrollView(rect, ref scrollPosRight, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            if (targetState != null)
            {
                GUI.color = new Color(1f, 0.6f, 0.3f);
                listing.Label("<b>" + "RAV_State_ModeHint".Translate(PortraitStateHelper.GetStateLabelKey(targetState.Value).Translate()) + "</b>");
                GUI.color = Color.white;
            }

            listing.Label("<b>" + "RAV_ArtCreator_PresetStyles".Translate() + "</b>");
            if (listing.RadioButton("RAV_Style_Standard".Translate(), options.style == ArtStyle.Standard)) options.style = ArtStyle.Standard;
            if (listing.RadioButton("RAV_Style_Realistic".Translate(), options.style == ArtStyle.Realistic)) options.style = ArtStyle.Realistic;
            if (listing.RadioButton("RAV_Style_Miyazaki".Translate(), options.style == ArtStyle.Miyazaki)) options.style = ArtStyle.Miyazaki;
            if (listing.RadioButton("RAV_Style_Chibi".Translate(), options.style == ArtStyle.Chibi)) options.style = ArtStyle.Chibi;

            if (currentMode == GenerationMode.ImageToImage)
            {
                if (listing.RadioButton("<b>" + "RAV_Style_CharacterSheet".Translate() + "</b>", options.style == ArtStyle.CharacterSheet)) options.style = ArtStyle.CharacterSheet;
            }

            if (listing.RadioButton("<b>" + "RAV_Style_Custom".Translate() + "</b>", options.style == ArtStyle.Custom)) options.style = ArtStyle.Custom;
            listing.GapLine();

            string constraintLabel = options.style == ArtStyle.Custom
                ? "<b>" + "RAV_ArtCreator_ConstraintCustom".Translate() + "</b>"
                : "<b>" + "RAV_ArtCreator_Constraint".Translate() + "</b>";
            listing.Label(constraintLabel);
            listing.CheckboxLabeled("RAV_Constraint_FullBody".Translate(), ref options.forceFullBody);
            listing.CheckboxLabeled("RAV_Constraint_CinematicLighting".Translate(), ref options.useCinematicLighting);
            listing.GapLine();

            listing.Label("<b>" + "RAV_ArtCreator_InfoRead".Translate() + "</b>");
            Rect row1 = listing.GetRect(24);
            Widgets.CheckboxLabeled(new Rect(row1.x, row1.y, row1.width / 2, 24), "RAV_Read_Race".Translate(), ref options.readRace);
            Widgets.CheckboxLabeled(new Rect(row1.x + row1.width / 2, row1.y, row1.width / 2, 24), "RAV_Read_Age".Translate(), ref options.readAge);
            if (options.readRace) { string rd = RaceDescriptionMapper.GetRaceVisuals(targetPawn); if (!string.IsNullOrEmpty(rd) && rd != "human") { GUI.color = Color.cyan; listing.Label("    " + "RAV_Read_Detected".Translate(rd)); GUI.color = Color.white; } }

            Rect row2 = listing.GetRect(24);
            Widgets.CheckboxLabeled(new Rect(row2.x, row2.y, row2.width / 2, 24), "RAV_Read_Health".Translate(), ref options.readHealth);
            Widgets.CheckboxLabeled(new Rect(row2.x + row2.width / 2, row2.y, row2.width / 2, 24), "RAV_Read_Apparel".Translate(), ref options.readApparel);

            Rect row3 = listing.GetRect(24);
            if (options.readApparel) Widgets.CheckboxLabeled(new Rect(row3.x, row3.y, row3.width / 2, 24), "RAV_Read_Quality".Translate(), ref options.readQuality);
            Widgets.CheckboxLabeled(new Rect(row3.x + row3.width / 2, row3.y, row3.width / 2, 24), "RAV_Read_Traits".Translate(), ref options.readTraits);

            Rect row4 = listing.GetRect(24);
            Widgets.CheckboxLabeled(new Rect(row4.x, row4.y, row4.width / 2, 24), "RAV_Read_Skin".Translate(), ref options.readSkinColor);
            Widgets.CheckboxLabeled(new Rect(row4.x + row4.width / 2, row4.y, row4.width / 2, 24), "RAV_Read_Hair".Translate(), ref options.readHair);

            listing.Gap(5);
            listing.CheckboxLabeled("RAV_Read_Background".Translate(), ref options.readBackground);

            listing.GapLine();

            if (options.style == ArtStyle.Custom) { GUI.color = Color.yellow; listing.Label("<b>" + "RAV_ArtCreator_MasterPrompt".Translate() + "</b>"); GUI.color = Color.white; }
            else if (options.style == ArtStyle.CharacterSheet) { GUI.color = Color.cyan; listing.Label("<b>" + "RAV_ArtCreator_ExtraDetailCS".Translate() + "</b>"); GUI.color = Color.white; }
            else { listing.Label("<b>" + "RAV_ArtCreator_ExtraDetail".Translate() + "</b>"); }

            options.extraPrompt = Widgets.TextArea(listing.GetRect(80), options.extraPrompt);

            listing.Gap(5);
            listing.Label("<b>" + "RAV_ArtCreator_FinalPrompt".Translate() + "</b>");

            options.isImg2ImgMode = (currentMode == GenerationMode.ImageToImage);
            string preview = BuildFinalPrompt();
            Widgets.TextArea(listing.GetRect(130), preview, true);

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawBottomButtons(Rect inRect, float btnX)
        {
            float btnHeight = 40f;
            float bottomY = inRect.height - 50f;
            float totalBtnWidth = inRect.width - btnX;
            float gap = 6f;
            float btnWidth = (totalBtnWidth - gap * 2) / 3f;

            Rect btnFree = new Rect(btnX, bottomY, btnWidth, btnHeight);
            Rect btnP2 = new Rect(btnX + btnWidth + gap, bottomY, btnWidth, btnHeight);
            Rect btnPro = new Rect(btnX + (btnWidth + gap) * 2, bottomY, btnWidth, btnHeight);

            if (isGenerating)
            {
                    GUI.color = Color.gray;
                    Widgets.Label(btnFree, "RAV_ArtCreator_Generating".Translate());
                    Widgets.Label(btnP2, "RAV_ArtCreator_Generating".Translate());
                    Widgets.Label(btnPro, "RAV_ArtCreator_Generating".Translate());
                GUI.color = Color.white;
            }
            else
            {
                if (currentMode == GenerationMode.ImageToImage && !PollinationsClient.HasUserKey)
                {
                    GUI.color = Color.gray;
                    Widgets.ButtonText(btnFree, "RAV_ArtCreator_FreeNoImg2Img".Translate());
                    GUI.color = Color.white;
                }
                else
                {
                    if (PollinationsClient.HasUserKey)
                    {
                        GUI.color = new Color(0.3f, 1f, 0.6f);
                        if (Widgets.ButtonText(btnFree, "RAV_ArtCreator_PollinationsKey".Translate())) StartGeneration(true, false);
                        GUI.color = Color.white;
                    }
                    else
                    {
                        int cooldown; bool isCoolingDown = PollinationsClient.IsCoolingDown(out cooldown);
                        if (isCoolingDown) { GUI.color = Color.gray; Widgets.ButtonText(btnFree, "RAV_ArtCreator_Cooldown".Translate(cooldown)); GUI.color = Color.white; }
                        else { GUI.color = Color.cyan; if (Widgets.ButtonText(btnFree, "RAV_ArtCreator_FreeTrial".Translate())) StartGeneration(true, false); GUI.color = Color.white; }
                    }
                }

                if (!Player2Client.IsAvailable)
                {
                    GUI.color = Color.gray;
                    Widgets.ButtonText(btnP2, "RAV_Player2_BtnOffline".Translate());
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = new Color(0.4f, 0.8f, 1f);
                    string p2ModeLabel = currentMode == GenerationMode.ImageToImage
                        ? "RAV_Player2_BtnImg2Img".Translate()
                        : "RAV_Player2_BtnTxt2Img".Translate();
                    if (Widgets.ButtonText(btnP2, p2ModeLabel)) StartGeneration(false, true);
                    GUI.color = Color.white;
                }

                int provIdx = RimAiVinciMod.settings.providerIndex;
                string provLabel = ApiProviders.IsLocalProvider(provIdx) ? "RAV_Provider_Local".Translate() : "RAV_ArtCreator_ProGen".Translate();
                string currentModel = currentMode == GenerationMode.ImageToImage ? RimAiVinciMod.settings.modelNameI2I : RimAiVinciMod.settings.modelName;
                if (string.IsNullOrEmpty(currentModel)) currentModel = ApiProviders.IsLocalProvider(provIdx) ? "Local" : "Flux";
                if (currentModel.Length > 18) currentModel = currentModel.Substring(0, 16) + "..";
                if (Widgets.ButtonText(btnPro, provLabel + "\n(" + currentModel + ")")) StartGeneration(false, false);
            }

            bool canSave = generatedTexture != null || (currentMode == GenerationMode.ImageToImage && referenceTexture != null);
            if (canSave && !isGenerating)
            {
                Rect btnSave = new Rect(btnX - btnWidth - gap, bottomY, btnWidth, btnHeight);
                if (hasSavedCurrent) { GUI.color = Color.green; Widgets.ButtonText(btnSave, "RAV_ArtCreator_Saved".Translate()); GUI.color = Color.white; }
                else if (Widgets.ButtonText(btnSave, "RAV_ArtCreator_SaveToAlbum".Translate())) SaveCurrentArt();
            }
        }

        private void OpenImportFloatMenu()
        {
            var files = ArtFileSystem.GetImportFiles();
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            if (files.Count == 0)
                opts.Add(new FloatMenuOption("RAV_ArtCreator_ImportEmpty".Translate(), null));
            else
            {
                foreach (var f in files)
                {
                    string path = f.FullName;
                    opts.Add(new FloatMenuOption(f.Name, () =>
                    {
                        Texture2D tex = ArtFileSystem.LoadTextureFromDisk(path);
                        if (tex != null) SetReferenceImage(tex);
                    }));
                }
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private void PasteFromClipboard()
        {
            string path = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(path))
            {
                Find.WindowStack.Add(new Dialog_MessageBox("RAV_ArtCreator_ClipboardEmpty".Translate()));
                return;
            }
            path = path.Trim().Trim('"');
            Texture2D loaded = ArtFileSystem.LoadTextureFromDisk(path);
            if (loaded != null)
            {
                SetReferenceImage(loaded);
                Messages.Message("RAV_ArtCreator_LoadSuccess".Translate(), MessageTypeDefOf.TaskCompletion);
            }
            else
                Find.WindowStack.Add(new Dialog_MessageBox("RAV_ArtCreator_LoadFail".Translate(path)));
        }

        private void StartGeneration(bool free, bool player2 = false)
        {
            if (generatedTexture != null) { UnityEngine.Object.Destroy(generatedTexture); generatedTexture = null; }
            isGenerating = true;
            hasSavedCurrent = false;
            options.isImg2ImgMode = (currentMode == GenerationMode.ImageToImage);
            string finalPrompt = BuildFinalPrompt();

            if (free)
            {
                if (currentMode == GenerationMode.ImageToImage && PollinationsClient.HasUserKey)
                {
                    if (referenceTexture == null) { Find.WindowStack.Add(new Dialog_MessageBox("RAV_ArtCreator_NeedReference".Translate())); isGenerating = false; return; }
                    SiliconClient.GenerateImageToImageAsync(finalPrompt, referenceTexture, RimAiVinciMod.settings.i2iStrength, (tex) => { generatedTexture = tex; isGenerating = false; });
                }
                else
                {
                    PollinationsClient.GenerateFreeImageAsync(finalPrompt, (tex) => { generatedTexture = tex; isGenerating = false; });
                }
            }
            else if (player2)
            {
                if (!Player2Client.IsAvailable)
                {
                    Find.WindowStack.Add(new Dialog_MessageBox("RAV_Player2_NoApp".Translate()));
                    isGenerating = false;
                    return;
                }
                if (currentMode == GenerationMode.ImageToImage)
                {
                    if (referenceTexture == null) { Find.WindowStack.Add(new Dialog_MessageBox("RAV_ArtCreator_NeedReference".Translate())); isGenerating = false; return; }
                    Player2Client.EditImageAsync(finalPrompt, referenceTexture, (tex) => { generatedTexture = tex; isGenerating = false; });
                }
                else
                {
                    Player2Client.GenerateImageAsync(finalPrompt, (tex) => { generatedTexture = tex; isGenerating = false; });
                }
            }
            else
            {
                int provIdx = RimAiVinciMod.settings.providerIndex;

                if (provIdx == ApiProviders.ComfyUI)
                {
                    if (currentMode == GenerationMode.ImageToImage)
                    {
                        if (referenceTexture == null) { Find.WindowStack.Add(new Dialog_MessageBox("RAV_ArtCreator_NeedReference".Translate())); isGenerating = false; return; }
                        ComfyUIClient.GenerateImageToImageAsync(finalPrompt, referenceTexture, RimAiVinciMod.settings.i2iStrength, (tex) => { generatedTexture = tex; isGenerating = false; });
                    }
                    else
                    {
                        ComfyUIClient.GenerateImageAsync(finalPrompt, (tex) => { generatedTexture = tex; isGenerating = false; });
                    }
                }
                else if (provIdx == ApiProviders.SDWebUI)
                {
                    if (currentMode == GenerationMode.ImageToImage)
                    {
                        if (referenceTexture == null) { Find.WindowStack.Add(new Dialog_MessageBox("RAV_ArtCreator_NeedReference".Translate())); isGenerating = false; return; }
                        SDWebUIClient.GenerateImageToImageAsync(finalPrompt, referenceTexture, RimAiVinciMod.settings.i2iStrength, (tex) => { generatedTexture = tex; isGenerating = false; });
                    }
                    else
                    {
                        SDWebUIClient.GenerateImageAsync(finalPrompt, (tex) => { generatedTexture = tex; isGenerating = false; });
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(RimAiVinciMod.settings.apiKey)) { Find.WindowStack.Add(new Dialog_MessageBox("RAV_ArtCreator_NeedApiKey".Translate())); isGenerating = false; return; }
                    if (currentMode == GenerationMode.ImageToImage)
                    {
                        if (referenceTexture == null) { Find.WindowStack.Add(new Dialog_MessageBox("RAV_ArtCreator_NeedReference".Translate())); isGenerating = false; return; }
                        SiliconClient.GenerateImageToImageAsync(finalPrompt, referenceTexture, RimAiVinciMod.settings.i2iStrength, (tex) => { generatedTexture = tex; isGenerating = false; });
                    }
                    else
                    {
                        SiliconClient.GenerateImageAsync(finalPrompt, (tex) => { generatedTexture = tex; isGenerating = false; });
                    }
                }
            }
        }

        private void SetReferenceImage(Texture2D t) { if (referenceTexture != null && referenceTexture != t) UnityEngine.Object.Destroy(referenceTexture); referenceTexture = t; }
        private Texture2D AdjustRenderTextureToTexture2D(RenderTexture r) { RenderTexture currentActive = RenderTexture.active; RenderTexture.active = r; Texture2D tex = new Texture2D(r.width, r.height, TextureFormat.ARGB32, false); tex.ReadPixels(new Rect(0, 0, r.width, r.height), 0, 0); tex.Apply(); RenderTexture.active = currentActive; return tex; }

        private Texture2D PadToSquare1024(Texture2D source)
        {
            int targetSize = 1024;
            int srcW = source.width;
            int srcH = source.height;
            float scale = Mathf.Min((float)targetSize / srcW, (float)targetSize / srcH);
            int scaledW = Mathf.RoundToInt(srcW * scale);
            int scaledH = Mathf.RoundToInt(srcH * scale);

            Color[] srcPixels = source.GetPixels();
            Color[] dstPixels = new Color[targetSize * targetSize];

            int offsetX = (targetSize - scaledW) / 2;
            int offsetY = (targetSize - scaledH) / 2;

            for (int dy = 0; dy < scaledH; dy++)
            {
                float srcYf = (float)dy / scaledH * srcH;
                int srcY = Mathf.Min(Mathf.FloorToInt(srcYf), srcH - 1);
                for (int dx = 0; dx < scaledW; dx++)
                {
                    float srcXf = (float)dx / scaledW * srcW;
                    int srcX = Mathf.Min(Mathf.FloorToInt(srcXf), srcW - 1);
                    dstPixels[(offsetY + dy) * targetSize + (offsetX + dx)] = srcPixels[srcY * srcW + srcX];
                }
            }

            Texture2D result = new Texture2D(targetSize, targetSize, TextureFormat.ARGB32, false);
            result.SetPixels(dstPixels);
            result.Apply();
            return result;
        }

        private void SaveCurrentArt()
        {
            Texture2D texToSave = generatedTexture != null ? generatedTexture : referenceTexture;
            if (texToSave == null) return;

            options.isImg2ImgMode = (currentMode == GenerationMode.ImageToImage);
            string finalPrompt = BuildFinalPrompt();
            if (generatedTexture == null) finalPrompt = "User Uploaded";

            string relPath = ArtFileSystem.SaveTextureToDisk(texToSave, targetPawn, finalPrompt);

            if (!string.IsNullOrEmpty(relPath))
            {
                ArtData newData = new ArtData
                {
                    artID = System.Guid.NewGuid().ToString(),
                    pawnID = targetPawn.ThingID,
                    relativePath = relPath,
                    prompt = finalPrompt,
                    timestamp = System.DateTime.Now.Ticks,
                    authorName = targetPawn.Name != null ? targetPawn.Name.ToStringShort : targetPawn.def.label,
                    pawnType = PawnPortraitTypeHelper.GetPawnType(targetPawn),
                    isMemorial = IsDeadPawn
                };
                ArtDataStore store = Find.World.GetComponent<ArtDataStore>();
                store.AddArt(newData);
                if (targetState != null)
                {
                    store.SetStatePortrait(targetPawn.ThingID, targetState.Value, relPath);
                }
                hasSavedCurrent = true;
                Messages.Message("RAV_ArtCreator_SaveSuccess".Translate(), MessageTypeDefOf.TaskCompletion);

                if (targetPawn.needs?.mood != null)
                {
                    ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("RAV_Thought_PortraitGenerated");
                    if (thought != null)
                    {
                        targetPawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                    }
                }
            }
        }

        private void DrawUploadOnlyWindow(Rect inRect)
        {
            string pawnLabel = targetPawn.Name != null ? targetPawn.Name.ToStringShort : targetPawn.def.label;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 40), "RAV_UploadOnly_Title".Translate(pawnLabel));
            Text.Font = GameFont.Small;

            GUI.color = Color.cyan;
            Widgets.Label(new Rect(0, 45, inRect.width, 40), "RAV_UploadOnly_Desc".Translate());
            GUI.color = Color.white;

            float imgSize = Mathf.Min(350f, inRect.width * 0.4f);
            Rect imgRect = new Rect(20, 100, imgSize, imgSize);
            Widgets.DrawMenuSection(imgRect);

            Texture2D displayTex = uploadedTexture;
            if (displayTex == null && referenceTexture != null) displayTex = referenceTexture;

            if (displayTex != null)
            {
                Widgets.DrawTextureFitted(imgRect, displayTex, 1.0f);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(imgRect, "RAV_UploadOnly_Placeholder".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            float btnX = imgRect.xMax + 20;
            float btnW = inRect.width - btnX - 20;
            float btnY = 100;

            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, 30), "RAV_UploadOnly_PastePath".Translate()))
            {
                PasteFromClipboard();
                uploadedTexture = referenceTexture;
                generatedTexture = null;
            }
            btnY += 38;

            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, 30), "RAV_UploadOnly_PickFile".Translate()))
            {
                OpenImportFloatMenu();
                uploadedTexture = referenceTexture;
                generatedTexture = null;
            }
            btnY += 38;

            if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, 30), "RAV_Btn_OpenFolder".Translate()))
            {
                Application.OpenURL(ArtFileSystem.GetImportDir());
            }
            btnY += 50;

            if (displayTex != null)
            {
                GUI.color = Color.green;
                if (Widgets.ButtonText(new Rect(btnX, btnY, btnW, 40), "RAV_UploadOnly_Save".Translate()))
                {
                    SaveUploadedArt(displayTex);
                }
                GUI.color = Color.white;
            }
        }

        private void SaveUploadedArt(Texture2D tex)
        {
            if (tex == null) return;
            string relPath = ArtFileSystem.SaveTextureToDisk(tex, targetPawn, "User Uploaded");
            if (!string.IsNullOrEmpty(relPath))
            {
                ArtData newData = new ArtData
                {
                    artID = System.Guid.NewGuid().ToString(),
                    pawnID = targetPawn.ThingID,
                    relativePath = relPath,
                    prompt = "User Uploaded",
                    timestamp = System.DateTime.Now.Ticks,
                    authorName = targetPawn.Name != null ? targetPawn.Name.ToStringShort : targetPawn.def.label,
                    pawnType = PawnPortraitTypeHelper.GetPawnType(targetPawn)
                };
                Find.World.GetComponent<ArtDataStore>().AddArt(newData);
                hasSavedCurrent = true;
                Messages.Message("RAV_ArtCreator_SaveSuccess".Translate(), MessageTypeDefOf.TaskCompletion);

                if (targetPawn.needs?.mood != null)
                {
                    ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("RAV_Thought_PortraitGenerated");
                    if (thought != null)
                    {
                        targetPawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                    }
                }
            }
        }
    }
}
