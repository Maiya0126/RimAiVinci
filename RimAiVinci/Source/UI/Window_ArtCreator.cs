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
        private bool isGenerating = false;
        private bool hasSavedCurrent = false;
        private GenerationMode currentMode = GenerationMode.TextToImage;
        private ArtStyle lastStyle = ArtStyle.Standard;

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

        public override void PostClose()
        {
            base.PostClose();
            if (generatedTexture != null) { UnityEngine.Object.Destroy(generatedTexture); generatedTexture = null; }
            if (referenceTexture != null) { UnityEngine.Object.Destroy(referenceTexture); referenceTexture = null; }
        }

        public override void DoWindowContents(Rect inRect)
        {
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
            Widgets.Label(headerRect, $"AI 创作工坊 - {targetPawn.Name.ToStringShort}");
            Text.Font = GameFont.Small;

            float modeBtnWidth = 150f;
            Rect btnModeTxt = new Rect(inRect.width - modeBtnWidth * 2 - 10, 0, modeBtnWidth, 30);
            Rect btnModeImg = new Rect(inRect.width - modeBtnWidth, 0, modeBtnWidth, 30);

            if (currentMode == GenerationMode.TextToImage) GUI.color = Color.green;
            if (Widgets.ButtonText(btnModeTxt, "文生图 (Txt2Img)"))
            {
                currentMode = GenerationMode.TextToImage;
                if (options.style == ArtStyle.CharacterSheet) options.style = ArtStyle.Standard;
            }
            GUI.color = Color.white;

            if (currentMode == GenerationMode.ImageToImage) GUI.color = Color.green;
            if (Widgets.ButtonText(btnModeImg, "图生图 (Img2Img)"))
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

            // 显示逻辑优化：如果有生成图，优先显示。否则如果有底图，显示底图。否则显示实时预览。
            bool showLivePreview = (displayTex == null);
            if (currentMode == GenerationMode.ImageToImage && referenceTexture != null && displayTex == null)
            {
                displayTex = referenceTexture;
                showLivePreview = false;
            }

            if (showLivePreview)
            {
                // 实时预览模式：可以旋转
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
                // 静态图模式（底图或生成结果）
                Widgets.DrawTextureFitted(imgRect, displayTex, 1.0f);
            }

            if (isGenerating) { Widgets.DrawBoxSolid(imgRect, new Color(0, 0, 0, 0.5f)); Text.Anchor = TextAnchor.MiddleCenter; Widgets.Label(imgRect, "AI 绘制中..."); Text.Anchor = TextAnchor.UpperLeft; }

            // 下方按钮区
            if (currentMode == GenerationMode.ImageToImage && !isGenerating)
            {
                float btnY = imgRect.yMax + 10;

                // ✨✨✨ 核心交互修改 ✨✨✨
                if (referenceTexture != null)
                {
                    // 如果已经有底图，显示“重置”按钮，点击后清空底图，回到实时预览（即可旋转）
                    if (Widgets.ButtonText(new Rect(rect.x + 10, btnY, rect.width - 20, 28), "🔄 重置底图 (重新抓取)"))
                    {
                        referenceTexture = null;
                        generatedTexture = null; // 同时清空生成结果，避免逻辑混乱
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }
                else
                {
                    // 如果没有底图，显示“设为底图”按钮
                    if (Widgets.ButtonText(new Rect(rect.x + 10, btnY, rect.width - 20, 28), "使用当前游戏立绘 (设为底图)"))
                    {
                        RenderTexture rt = PortraitsCache.Get(targetPawn, new Vector2(512, 512), currentRotation);
                        SetReferenceImage(AdjustRenderTextureToTexture2D(rt));
                        generatedTexture = null;
                        SoundDefOf.Click.PlayOneShotOnCamera();
                    }
                }

                btnY += 32;
                if (Widgets.ButtonText(new Rect(rect.x + 10, btnY, rect.width - 20, 28), "从剪贴板粘贴路径")) { PasteFromClipboard(); generatedTexture = null; }
                btnY += 32;
                if (Widgets.ButtonText(new Rect(rect.x + 10, btnY, (rect.width - 25) / 2, 28), "📂 打开文件夹")) Application.OpenURL(ArtFileSystem.GetImportDir());
                if (Widgets.ButtonText(new Rect(rect.x + 15 + (rect.width - 25) / 2, btnY, (rect.width - 25) / 2, 28), "⬇️ 选择文件")) OpenImportFloatMenu();
                btnY += 35;
                Widgets.Label(new Rect(rect.x + 10, btnY, rect.width - 20, 30), $"当前重绘幅度: {RimAiVinciMod.settings.i2iStrength:P0}");
            }
            else
            {
                var store = Find.World.GetComponent<ArtDataStore>();
                int count = store != null ? store.GetArtForPawn(targetPawn).Count : 0;
                Widgets.Label(new Rect(rect.x + 10, imgRect.yMax + 10, rect.width - 20, 30), $"已拥有画像: {count} 张");
            }
        }

        private void DrawRightPanel(Rect rect)
        {
            Rect viewRect = new Rect(0, 0, rect.width - 16, 1100f);
            Widgets.BeginScrollView(rect, ref scrollPosRight, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.Label("<b>1. 预装艺术风格:</b>");
            if (listing.RadioButton("标准风格 (Standard)", options.style == ArtStyle.Standard)) options.style = ArtStyle.Standard;
            if (listing.RadioButton("废土写实 (Realistic)", options.style == ArtStyle.Realistic)) options.style = ArtStyle.Realistic;
            if (listing.RadioButton("宫崎骏风 (Miyazaki)", options.style == ArtStyle.Miyazaki)) options.style = ArtStyle.Miyazaki;
            if (listing.RadioButton("Q版萌系 (Chibi)", options.style == ArtStyle.Chibi)) options.style = ArtStyle.Chibi;

            if (currentMode == GenerationMode.ImageToImage)
            {
                if (listing.RadioButton("<b>设定集模式 (Character Sheet)</b>", options.style == ArtStyle.CharacterSheet)) options.style = ArtStyle.CharacterSheet;
            }

            if (listing.RadioButton("<b>自定义/大师模式 (Custom)</b>", options.style == ArtStyle.Custom)) options.style = ArtStyle.Custom;
            listing.GapLine();

            string constraintLabel = options.style == ArtStyle.Custom ? "<b>2. 画面约束 (大师模式可选):</b>" : "<b>2. 画面约束:</b>";
            listing.Label(constraintLabel);
            listing.CheckboxLabeled("强制全身站立", ref options.forceFullBody);
            listing.CheckboxLabeled("统一电影光影", ref options.useCinematicLighting);
            listing.GapLine();

            listing.Label("<b>3. 信息读取:</b>");
            Rect row1 = listing.GetRect(24);
            Widgets.CheckboxLabeled(new Rect(row1.x, row1.y, row1.width / 2, 24), "读取种族 (Race)", ref options.readRace);
            Widgets.CheckboxLabeled(new Rect(row1.x + row1.width / 2, row1.y, row1.width / 2, 24), "读取年龄 (Age)", ref options.readAge);
            if (options.readRace) { string rd = RaceDescriptionMapper.GetRaceVisuals(targetPawn); if (!string.IsNullOrEmpty(rd) && rd != "human") { GUI.color = Color.cyan; listing.Label($"    ↳ 识别: {rd}"); GUI.color = Color.white; } }

            Rect row2 = listing.GetRect(24);
            Widgets.CheckboxLabeled(new Rect(row2.x, row2.y, row2.width / 2, 24), "读取伤病 (Health)", ref options.readHealth);
            Widgets.CheckboxLabeled(new Rect(row2.x + row2.width / 2, row2.y, row2.width / 2, 24), "读取服装 (Apparel)", ref options.readApparel);

            Rect row3 = listing.GetRect(24);
            if (options.readApparel) Widgets.CheckboxLabeled(new Rect(row3.x, row3.y, row3.width / 2, 24), "读取耐久 (Quality)", ref options.readQuality);
            Widgets.CheckboxLabeled(new Rect(row3.x + row3.width / 2, row3.y, row3.width / 2, 24), "读取特性 (Traits)", ref options.readTraits);

            Rect row4 = listing.GetRect(24);
            Widgets.CheckboxLabeled(new Rect(row4.x, row4.y, row4.width / 2, 24), "读取肤色 (Skin)", ref options.readSkinColor);
            Widgets.CheckboxLabeled(new Rect(row4.x + row4.width / 2, row4.y, row4.width / 2, 24), "读取发型 (Hair)", ref options.readHair);

            listing.Gap(5);
            listing.CheckboxLabeled("读取背景故事 (Background)", ref options.readBackground);

            listing.GapLine();

            if (options.style == ArtStyle.Custom) { GUI.color = Color.yellow; listing.Label("<b>4. 大师提示词 (Master Prompt):</b>"); GUI.color = Color.white; }
            else if (options.style == ArtStyle.CharacterSheet) { GUI.color = Color.cyan; listing.Label("<b>4. 额外细节 (追加描述):</b>"); GUI.color = Color.white; }
            else { listing.Label("<b>4. 额外细节 (Extra Details):</b>"); }

            options.extraPrompt = Widgets.TextArea(listing.GetRect(80), options.extraPrompt);

            listing.Gap(5);
            listing.Label("<b>最终指令预览 (Final Prompt):</b>");

            options.isImg2ImgMode = (currentMode == GenerationMode.ImageToImage);
            string preview = PawnPromptBuilder.BuildPromptFromPawn(targetPawn, options);
            Widgets.TextArea(listing.GetRect(130), preview, true);

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawBottomButtons(Rect inRect, float btnX)
        {
            float btnWidth = 170f; float btnHeight = 40f;
            float bottomY = inRect.height - 50f;

            Rect btnFree = new Rect(btnX, bottomY, btnWidth, btnHeight);
            Rect btnPro = new Rect(inRect.width - btnWidth, bottomY, btnWidth, btnHeight);

            if (isGenerating) { GUI.color = Color.gray; Widgets.Label(btnFree, "绘制中..."); Widgets.Label(btnPro, "绘制中..."); GUI.color = Color.white; }
            else
            {
                if (currentMode == GenerationMode.ImageToImage) { GUI.color = Color.gray; Widgets.ButtonText(btnFree, "免费通道\n不支持图生图"); GUI.color = Color.white; }
                else
                {
                    int cooldown; bool isCoolingDown = PollinationsClient.IsCoolingDown(out cooldown);
                    if (isCoolingDown) { GUI.color = Color.gray; Widgets.ButtonText(btnFree, $"冷却中 ({cooldown}s)"); GUI.color = Color.white; }
                    else { GUI.color = Color.cyan; if (Widgets.ButtonText(btnFree, "✨ 免费试用")) StartGeneration(true); GUI.color = Color.white; }
                }

                string currentModel = currentMode == GenerationMode.ImageToImage ? RimAiVinciMod.settings.modelNameI2I : RimAiVinciMod.settings.modelName;
                if (string.IsNullOrEmpty(currentModel)) currentModel = "Flux";
                if (currentModel.Length > 20) currentModel = currentModel.Substring(0, 18) + "..";
                if (Widgets.ButtonText(btnPro, $"Pro生图\n({currentModel})")) StartGeneration(false);
            }

            bool canSave = generatedTexture != null || (currentMode == GenerationMode.ImageToImage && referenceTexture != null);
            if (canSave && !isGenerating)
            {
                Rect btnSave = new Rect(inRect.width - btnWidth * 2 - 60, bottomY, btnWidth, btnHeight);
                if (hasSavedCurrent) { GUI.color = Color.green; Widgets.ButtonText(btnSave, "已保存"); GUI.color = Color.white; }
                else if (Widgets.ButtonText(btnSave, "保存到相册")) SaveCurrentArt();
            }
        }

        private void OpenImportFloatMenu() { var files = ArtFileSystem.GetImportFiles(); List<FloatMenuOption> opts = new List<FloatMenuOption>(); if (files.Count == 0) opts.Add(new FloatMenuOption("文件夹是空的", null)); else { foreach (var f in files) { string path = f.FullName; opts.Add(new FloatMenuOption(f.Name, () => { Texture2D tex = ArtFileSystem.LoadTextureFromDisk(path); if (tex != null) SetReferenceImage(tex); })); } } Find.WindowStack.Add(new FloatMenu(opts)); }
        private void PasteFromClipboard() { string path = GUIUtility.systemCopyBuffer; if (string.IsNullOrEmpty(path)) { Find.WindowStack.Add(new Dialog_MessageBox("剪贴板为空")); return; } path = path.Trim().Trim('"'); Texture2D loaded = ArtFileSystem.LoadTextureFromDisk(path); if (loaded != null) { SetReferenceImage(loaded); Messages.Message("加载成功", MessageTypeDefOf.TaskCompletion); } else Find.WindowStack.Add(new Dialog_MessageBox($"无法加载：{path}")); }

        private void StartGeneration(bool free)
        {
            if (generatedTexture != null) { UnityEngine.Object.Destroy(generatedTexture); generatedTexture = null; }
            isGenerating = true;
            hasSavedCurrent = false;
            options.isImg2ImgMode = (currentMode == GenerationMode.ImageToImage);
            string finalPrompt = PawnPromptBuilder.BuildPromptFromPawn(targetPawn, options);

            if (free)
            {
                PollinationsClient.GenerateFreeImageAsync(finalPrompt, (tex) => { generatedTexture = tex; isGenerating = false; });
            }
            else
            {
                if (string.IsNullOrEmpty(RimAiVinciMod.settings.apiKey)) { Find.WindowStack.Add(new Dialog_MessageBox("请设置 API Key")); isGenerating = false; return; }
                if (currentMode == GenerationMode.ImageToImage)
                {
                    if (referenceTexture == null) { Find.WindowStack.Add(new Dialog_MessageBox("请设置底图")); isGenerating = false; return; }
                    SiliconClient.GenerateImageToImageAsync(finalPrompt, referenceTexture, RimAiVinciMod.settings.i2iStrength, (tex) => { generatedTexture = tex; isGenerating = false; });
                }
                else
                {
                    SiliconClient.GenerateImageAsync(finalPrompt, (tex) => { generatedTexture = tex; isGenerating = false; });
                }
            }
        }

        private void SetReferenceImage(Texture2D t) { if (referenceTexture != null && referenceTexture != t) UnityEngine.Object.Destroy(referenceTexture); referenceTexture = t; }
        private Texture2D AdjustRenderTextureToTexture2D(RenderTexture r) { RenderTexture currentActive = RenderTexture.active; RenderTexture.active = r; Texture2D tex = new Texture2D(r.width, r.height, TextureFormat.ARGB32, false); tex.ReadPixels(new Rect(0, 0, r.width, r.height), 0, 0); tex.Apply(); RenderTexture.active = currentActive; return tex; }
        private void SaveCurrentArt()
        {
            Texture2D texToSave = generatedTexture != null ? generatedTexture : referenceTexture;
            if (texToSave == null) return;

            options.isImg2ImgMode = (currentMode == GenerationMode.ImageToImage);
            string finalPrompt = PawnPromptBuilder.BuildPromptFromPawn(targetPawn, options);
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
                    authorName = targetPawn.Name.ToStringShort
                };
                Find.World.GetComponent<ArtDataStore>().AddArt(newData);
                hasSavedCurrent = true;
                Messages.Message("保存成功", MessageTypeDefOf.TaskCompletion);

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