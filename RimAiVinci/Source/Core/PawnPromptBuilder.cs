using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    public enum ArtStyle { Standard, Realistic, Miyazaki, Chibi, CharacterSheet, Custom }

    public class PromptOptions
    {
        public ArtStyle style = ArtStyle.Standard;
        public bool isImg2ImgMode = false;
        public string extraPrompt = "";

        public bool forceFullBody = false;
        public bool useCinematicLighting = false;

        public bool readAge = true;
        public bool readBodyType = true;
        public bool readApparel = true;
        public bool readHair = true;
        public bool readTraits = true;
        public bool readBackground = false;
        public bool readRace = true;
        public bool readHealth = true;
        public bool readQuality = true;
        public bool readSkinColor = true;
        public bool readRimTalkPersona = false;

        /// <summary>自然语言模式（Qwen/Flux/T5/LLM 系编码器）：跳过 (x:权重) 语法</summary>
        public bool naturalLanguageMode = false;

        /// <summary>
        /// 切换文生图/图生图模式时，自动应用最适合该模式的默认勾选项：
        /// 图生图底图已包含真实服装、颜色、发型和肤色，默认关闭这些文字项避免与底图冲突。
        /// </summary>
        public void ApplyModeDefaults(bool img2Img)
        {
            isImg2ImgMode = img2Img;
            if (style == ArtStyle.Custom) return;

            if (img2Img)
            {
                readRace = true;
                readAge = true;
                readTraits = true;
                readHealth = false;
                readApparel = false;
                readQuality = false;
                readSkinColor = false;
                readHair = false;
                readBackground = false;
            }
            else
            {
                readRace = true;
                readAge = true;
                readTraits = true;
                readHealth = true;
                readApparel = true;
                readQuality = true;
                readSkinColor = true;
                readHair = true;
                readBackground = false;
            }
        }
    }

    /// <summary>
    /// 生成期上下文：传递种族防变异负面词到各 API 客户端。
    /// 生成请求发出后自动失效（OneShot 语义）。
    /// </summary>
    public static class PromptContext
    {
        public static string RaceNegative;

        /// <summary>设置本次生成的种族负面词与防像素/防无手脚棋子负面词（创作窗在发起生成前调用）</summary>
        public static void Begin(Pawn pawn)
        {
            const string antiSpriteNeg = "pixel art, game sprite, armless, legless, floating torso, pawn token";
            RaceNegative = antiSpriteNeg;
            try
            {
                string custom = CustomRacePromptStore.GetCustom(pawn);
                if (!string.IsNullOrEmpty(custom))
                {
                    return;
                }
                string raceNeg = RaceDescriptionMapper.GetRaceNegative(pawn);
                if (!string.IsNullOrEmpty(raceNeg))
                {
                    RaceNegative = antiSpriteNeg + ", " + raceNeg;
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// RimTalk 软联动读取器：无硬依赖读取角色在 RimTalk 中的人格（Personality）设定。
    /// </summary>
    public static class RimTalkPersonaReader
    {
        private static bool? _rimTalkInstalled;
        private static MethodInfo _getPersonalityMethod;
        private static bool _methodSearched;

        public static bool IsRimTalkInstalled
        {
            get
            {
                if (_rimTalkInstalled.HasValue) return _rimTalkInstalled.Value;
                try
                {
                    _rimTalkInstalled = DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_PersonaData") != null;
                }
                catch
                {
                    _rimTalkInstalled = false;
                }
                return _rimTalkInstalled.Value;
            }
        }

        public static string GetPersonaText(Pawn pawn)
        {
            if (pawn == null || !IsRimTalkInstalled) return "";
            try
            {
                // 1. 优先直接从 Pawn 已有的 RimTalk_PersonaData Hediff 反射读取 Personality 字段
                HediffDef personaDef = DefDatabase<HediffDef>.GetNamedSilentFail("RimTalk_PersonaData");
                if (personaDef != null && pawn.health?.hediffSet != null)
                {
                    Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(personaDef, false);
                    if (hediff != null)
                    {
                        FieldInfo fi = hediff.GetType().GetField("Personality", BindingFlags.Public | BindingFlags.Instance);
                        if (fi != null)
                        {
                            string val = fi.GetValue(hediff) as string;
                            if (!string.IsNullOrWhiteSpace(val)) return CleanPersonaText(val);
                        }
                    }
                }

                // 2. 若小人尚未触发过初始化，尝试调用 RimTalk.Data.PersonaService.GetPersonality(pawn)
                if (!_methodSearched)
                {
                    _methodSearched = true;
                    foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (asm.GetName().Name == "RimTalk")
                        {
                            Type svcType = asm.GetType("RimTalk.Data.PersonaService");
                            if (svcType != null)
                            {
                                _getPersonalityMethod = svcType.GetMethod("GetPersonality", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(Pawn) }, null);
                            }
                            break;
                        }
                    }
                }

                if (_getPersonalityMethod != null)
                {
                    string val = _getPersonalityMethod.Invoke(null, new object[] { pawn }) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return CleanPersonaText(val);
                }
            }
            catch { }
            return "";
        }

        private static string CleanPersonaText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string clean = raw.Replace("\r", " ").Replace("\n", " ").Trim();
            clean = Regex.Replace(clean, @"\s+", " ");
            // RimTalk 人格通常在 300~500 字左右，保留最高 600 字符并在句号边界温和收尾，避免腰斩设定
            const int maxLen = 600;
            if (clean.Length > maxLen)
            {
                clean = clean.Substring(0, maxLen);
                int lastStop = Math.Max(
                    Math.Max(clean.LastIndexOf('。'), clean.LastIndexOf('.')),
                    Math.Max(clean.LastIndexOf('；'), clean.LastIndexOf(';')));
                if (lastStop > maxLen / 2)
                {
                    clean = clean.Substring(0, lastStop + 1);
                }
            }
            return clean.Trim();
        }
    }

    public static class PawnPromptBuilder
    {
        private static readonly string[] VisualKeywords = new string[]
        {
            "ear", "tail", "wing", "horn", "skin", "hair", "fur", "eye", "face",
            "body", "scale", "feather", "glowing", "arm", "leg", "humanoid", "antenna",
            "feature", "appearance", "look"
        };

        public static string BuildPromptFromPawn(Pawn pawn, PromptOptions opt)
        {
            string safeExtra = SanitizePrompt(opt.extraPrompt);
            if (pawn == null) return "A mysterious figure";

            if (pawn.story == null)
            {
                string raceLabel = pawn.def?.label ?? "creature";
                string pawnGender = pawn.gender != Gender.None ? pawn.gender.ToString() : "";
                string age = GetAgeDescription(pawn);
                return $"{safeExtra} {pawnGender} {raceLabel}, {age}, RimWorld style, digital art";
            }

            if (opt.style == ArtStyle.CharacterSheet)
                return BuildCharacterSheetPrompt(pawn, opt);

            StringBuilder sb = new StringBuilder();

            // 1. 风格前缀（自然语言模式下不加权重语法；禁止在正向提示词写 NOT pixel art 等反作用词）
            if (opt.style != ArtStyle.Custom)
            {
                if (opt.isImg2ImgMode)
                {
                    string breakStructure = opt.naturalLanguageMode
                        ? "complete humanoid body structure with detailed arms, hands and legs, natural proportions, "
                        : "(complete humanoid body structure:1.4), (detailed arms and hands:1.3), (detailed legs:1.3), ";
                    switch (opt.style)
                    {
                        case ArtStyle.Standard:
                            sb.Append($"masterpiece, best quality, anime character illustration, detailed cel shading, clean lineart, vibrant colors, expressive eyes, {breakStructure}dynamic pose, ");
                            break;
                        case ArtStyle.Realistic:
                            sb.Append($"photorealistic, 8k uhd, gritty sci-fi frontier portrait, realistic skin pores and fabric texture, weathered details{(opt.naturalLanguageMode ? ", " : ", (hyper realistic:1.3), ")}{breakStructure}dramatic shadows, ");
                            break;
                        case ArtStyle.Miyazaki:
                            sb.Append($"studio ghibli style, hayao miyazaki, 1990s cel animation, hand-painted watercolor and gouache texture, warm nostalgic palette, gentle expressive features, {breakStructure}soft colors, ");
                            break;
                        case ArtStyle.Chibi:
                            sb.Append($"chibi style, super deformed, 2 heads tall, cute adorable proportions, big expressive sparkling eyes, clean thick lineart, soft vibrant colors{(opt.naturalLanguageMode ? ", complete chibi body with arms and legs, " : ", (cute chibi body with arms and legs:1.3), ")}");
                            break;
                    }
                }
                else
                {
                    switch (opt.style)
                    {
                        case ArtStyle.Standard:
                            sb.Append("masterpiece, best quality, anime character illustration, detailed cel shading, clean lineart, vibrant colors, expressive eyes, character concept art, ");
                            break;
                        case ArtStyle.Realistic:
                            sb.Append("photorealistic, 8k uhd, gritty sci-fi frontier portrait, realistic skin pores and fabric texture, weathered details, sci-fi concept art, dramatic shadows, ");
                            break;
                        case ArtStyle.Miyazaki:
                            sb.Append("studio ghibli style, hayao miyazaki, 1990s cel animation, hand-painted watercolor and gouache texture, warm nostalgic palette, gentle expressive features, picturesque, ");
                            break;
                        case ArtStyle.Chibi:
                            sb.Append("chibi style, super deformed, 2 heads tall, cute adorable proportions, big expressive sparkling eyes, clean thick lineart, soft vibrant colors, kawaii illustration, ");
                            break;
                    }
                }
            }
            else
            {
                sb.Append("masterpiece, best quality, ");
            }

            if (opt.forceFullBody)
            {
                if (opt.style == ArtStyle.Realistic)
                    sb.Append("full body portrait, head to toe, standing, showing footwear, ");
                else if (opt.naturalLanguageMode)
                    sb.Append("full body standing portrait from head to toe, ");
                else
                    sb.Append("(full body:1.3), standing, head to toe, ");
            }

            if (opt.useCinematicLighting)
            {
                if (opt.naturalLanguageMode)
                    sb.Append("cinematic volumetric lighting, dramatic rim light, atmospheric depth, ");
                else
                    sb.Append("(cinematic lighting:1.25), volumetric light, dramatic rim lighting, ");
            }

            // 2. 种族与描述（修复：用英文 defName，避免中文 label 污染提示词）
            string gender = pawn.gender == Gender.None ? "" : pawn.gender.ToString();

            if (opt.readRace)
            {
                // 优先级：玩家自定义 > 内置词库（按语法模式选形式）> 动态种族描述
                string customVisuals = CustomRacePromptStore.GetCustom(pawn);
                if (!string.IsNullOrEmpty(customVisuals))
                {
                    if (opt.naturalLanguageMode)
                        sb.Append($"{gender}, {customVisuals}, ");
                    else
                        sb.Append($"{gender}, ({customVisuals}:1.2), ");
                    string dynamicDesc = GetDynamicRaceDescription(pawn);
                    if (!string.IsNullOrEmpty(dynamicDesc)) sb.Append($"{dynamicDesc}, ");
                }
                else
                {
                    string builtInVisuals = RaceDescriptionMapper.GetRaceVisuals(pawn, opt.naturalLanguageMode);
                    string dynamicDesc = GetDynamicRaceDescription(pawn);
                    string raceDefName = pawn.def.defName;

                    if (!string.IsNullOrEmpty(builtInVisuals))
                    {
                        if (opt.naturalLanguageMode)
                        {
                            sb.Append($"{gender}, {builtInVisuals}, ");
                        }
                        else
                        {
                            string racePrefix = (raceDefName == "Human") ? "human" : $"(RimWorld {raceDefName} race:1.2)";
                            sb.Append($"{gender}, {racePrefix}, ({builtInVisuals}:1.3), ");
                        }
                        if (!string.IsNullOrEmpty(dynamicDesc)) sb.Append($"{dynamicDesc}, ");
                    }
                    else
                    {
                        string racePrefix = (raceDefName == "Human") ? "human" : (opt.naturalLanguageMode ? $"{raceDefName} race" : $"(RimWorld {raceDefName} race:1.2)");
                        sb.Append($"{gender}, {racePrefix}, ");
                        if (!string.IsNullOrEmpty(dynamicDesc)) sb.Append($"{dynamicDesc}, ");
                    }
                }
            }
            else { sb.Append($"{gender}, "); }

            // 3. 基础特征 (肤色 + 眼睛)
            if (opt.readSkinColor)
            {
                sb.Append($"{ColorLibrary.GetBestMatch(pawn.story.SkinColor, "skin")}, ");

                string eyeColor = GetEyeColorDescription(pawn);
                if (!string.IsNullOrEmpty(eyeColor)) sb.Append($"{eyeColor}, ");
            }

            if (opt.readAge) sb.Append($"{GetAgeDescription(pawn)}, ");
            if (opt.readBodyType)
            {
                string bodyDesc = GetBodyDescription(pawn);
                if (!string.IsNullOrEmpty(bodyDesc)) sb.Append($"{bodyDesc}, ");
            }

            if (opt.readHair)
            {
                string colorName = ColorLibrary.GetBestMatch(pawn.story.HairColor, "hair");
                string hairStyleName = GetHairStyleDescription(pawn.story.hairDef);
                if (opt.naturalLanguageMode)
                    sb.Append($"{colorName} hair styled as {hairStyleName}, ");
                else
                    sb.Append($"({colorName}:1.3) {hairStyleName}, ");
            }

            // 4. 服装与其他
            if (opt.readApparel)
            {
                string a = GetApparelDescription(pawn, opt.readQuality, opt.isImg2ImgMode);
                if (!string.IsNullOrEmpty(a)) sb.Append($"{a}, ");
            }
            if (opt.readHealth)
            {
                string h = GetHealthDescription(pawn);
                if (!string.IsNullOrEmpty(h)) sb.Append($"{h}, ");
            }
            if (opt.readTraits && pawn.story.traits != null)
            {
                foreach (var t in pawn.story.traits.allTraits)
                {
                    string traitDesc = TranslateTraitToEnglish(t);
                    if (!string.IsNullOrEmpty(traitDesc)) sb.Append($"{traitDesc}, ");
                }
            }

            if (opt.readRimTalkPersona)
            {
                string persona = SanitizePrompt(RimTalkPersonaReader.GetPersonaText(pawn));
                if (!string.IsNullOrEmpty(persona))
                {
                    sb.Append($"character personality and demeanor: ({persona}), ");
                }
            }

            if (!string.IsNullOrEmpty(safeExtra)) sb.Append($"{safeExtra}, ");

            if (opt.readBackground && pawn.story.Adulthood != null)
            {
                float bgWeight = opt.forceFullBody ? 0.6f : 0.8f;
                string jobTitle = GetBackstoryTitleDescription(pawn.story.Adulthood);
                if (opt.naturalLanguageMode)
                    sb.Append($"background theme of a {jobTitle}, ");
                else
                    sb.Append($"(background theme: {jobTitle}:{bgWeight}), ");
            }

            return sb.ToString();
        }

        private static string BuildCharacterSheetPrompt(Pawn pawn, PromptOptions opt)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("character reference sheet, anime character design concept art, complete humanoid body with arms, hands and legs.\nCore Requirements:\n- Art Style: high quality anime character sheet, clean lineart, flat cel shading, vibrant accurate colors.\n");

            string race = pawn.def.defName;
            string gender = pawn.gender.ToString();
            string age = GetAgeDescription(pawn);
            string body = GetBodyDescription(pawn);
            string skin = ColorLibrary.GetBestMatch(pawn.story.SkinColor, "skin");
            sb.Append($"- Race & Vibe: {race} ({gender}). Appearance is {age}, {body}, {skin}. ");

            if (opt.readSkinColor) { string eyeColor = GetEyeColorDescription(pawn); if (!string.IsNullOrEmpty(eyeColor)) sb.Append($"- Eyes: {eyeColor}\n"); }
            if (opt.readTraits && pawn.story.traits != null && pawn.story.traits.allTraits.Count > 0)
            {
                sb.Append("Reflect traits: ");
                foreach (var t in pawn.story.traits.allTraits) sb.Append($"{TranslateTraitToEnglish(t)}, ");
                sb.Append("\n");
            }
            else sb.Append("\n");

            if (opt.readRimTalkPersona)
            {
                string persona = SanitizePrompt(RimTalkPersonaReader.GetPersonaText(pawn));
                if (!string.IsNullOrEmpty(persona)) sb.Append($"- Personality & Demeanor: {persona}\n");
            }

            if (opt.readRace)
            {
                sb.Append("Key Constraints:\n");
                string customVisuals = CustomRacePromptStore.GetCustom(pawn);
                if (!string.IsNullOrEmpty(customVisuals))
                {
                    sb.Append($"- Custom Visual Tags: {customVisuals}\n");
                }
                else
                {
                    string manualVisuals = RaceDescriptionMapper.GetRaceVisuals(pawn, true);
                    string dynamicDesc = GetDynamicRaceDescription(pawn);
                    if (!string.IsNullOrEmpty(manualVisuals)) sb.Append($"- Visual Tags: {manualVisuals}\n");
                    if (!string.IsNullOrEmpty(dynamicDesc)) sb.Append($"- Lore Description: {dynamicDesc}\n");
                }
            }
            if (opt.readHair) { string hairColor = ColorLibrary.GetBestMatch(pawn.story.HairColor, "hair"); string hairStyle = GetHairStyleDescription(pawn.story.hairDef); sb.Append($"- Hair: {hairColor} {hairStyle}\n"); }
            if (opt.readApparel) { string apparel = GetApparelDescription(pawn, false, opt.isImg2ImgMode); if (!string.IsNullOrEmpty(apparel)) sb.Append($"- Clothing: {apparel}\n"); }

            sb.Append("Composition & Camera:\n- Shot: Full body standing portrait from head to toe.\n- Background: Simple clean white background.\n");
            if (opt.useCinematicLighting)
            {
                sb.Append("- Lighting: Cinematic volumetric lighting with subtle rim light.\n");
            }
            sb.Append("Action:\n- Pose: Natural standing reference pose.\n");

            string safeExtra = SanitizePrompt(opt.extraPrompt);
            if (!string.IsNullOrEmpty(safeExtra)) sb.Append($"- Extra Details: {safeExtra}\n");

            return sb.ToString();
        }

        // =========================================================
        // 🛠️ 辅助方法
        // =========================================================

        private static string SanitizePrompt(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string output = input.Replace("，", ",").Replace("。", ".").Replace("（", "(").Replace("）", ")");
            output = output.Replace("\r", " ").Replace("\n", " ");
            output = Regex.Replace(output, @"\s+", " ");
            return output.Trim();
        }

        private static bool ContainsNonAscii(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] > 127) return true;
            }
            return false;
        }

        private static string HumanizeDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "";
            string s = Regex.Replace(defName, @"^(Apparel|Hair|Trait|Backstory)_+", "", RegexOptions.IgnoreCase);
            s = s.Replace("_", " ").Replace("-", " ");
            s = Regex.Replace(s, @"([a-z])([A-Z])", "$1 $2");
            s = Regex.Replace(s, @"\s+", " ").Trim().ToLower();
            return s;
        }

        private static string GetHairStyleDescription(HairDef hairDef)
        {
            if (hairDef == null) return "hair";
            string label = hairDef.label ?? "";
            if (!ContainsNonAscii(label) && !string.IsNullOrWhiteSpace(label))
                return label.ToLower() + " hair";

            string fromDef = HumanizeDefName(hairDef.defName);
            if (!string.IsNullOrWhiteSpace(fromDef))
                return $"{fromDef} hairstyle ({label})";

            return label;
        }

        private static string GetBackstoryTitleDescription(BackstoryDef bs)
        {
            if (bs == null) return "";
            string title = (bs.title ?? "").Replace("(", "").Replace(")", "");
            if (!ContainsNonAscii(title) && !string.IsNullOrWhiteSpace(title))
                return title;
            string fromDef = HumanizeDefName(bs.defName);
            if (!string.IsNullOrWhiteSpace(fromDef))
                return $"{fromDef} ({title})";
            return title;
        }

        private static string GetEyeColorDescription(Pawn pawn)
        {
            // 1. 优先从基因读取 (Biotech)
            if (pawn.genes != null)
            {
                string leftEyeColor = null;
                string rightEyeColor = null;
                string generalEyeColor = null;

                foreach (var gene in pawn.genes.GenesListForReading)
                {
                    if (gene.def.renderNodeProperties == null) continue;
                    foreach (var node in gene.def.renderNodeProperties)
                    {
                        if (node.texPath == null || !node.color.HasValue) continue;
                        string path = node.texPath.ToLower();
                        string colorName = ColorLibrary.GetBestMatch(node.color.Value, "eye");
                        if (path.Contains("eye") || path.Contains("pupil"))
                        {
                            if (path.Contains("left")) leftEyeColor = colorName;
                            else if (path.Contains("right")) rightEyeColor = colorName;
                            else generalEyeColor = colorName;
                        }
                    }
                }
                if (leftEyeColor != null && rightEyeColor != null)
                {
                    if (leftEyeColor == rightEyeColor) return $"{leftEyeColor} eyes";
                    return $"heterochromia, {leftEyeColor} left eye, {rightEyeColor} right eye";
                }
                if (generalEyeColor != null) return $"{generalEyeColor} eyes";
            }

            // 2. 兜底逻辑：根据发色生成眼睛描述
            Color hair = pawn.story.HairColor;
            if (hair.r < 0.3f && hair.g < 0.2f) return "brown eyes";
            if (hair.r > 0.8f && hair.g > 0.8f && hair.b < 0.7f) return "blue eyes";
            if (hair.r > 0.8f && hair.g < 0.5f) return "green eyes";
            if (hair.r > 0.8f && hair.g > 0.8f && hair.b > 0.8f) return "grey eyes";

            return "eyes with distinct iris";
        }

        /// <summary>
        /// 判断服装的 DrawColor 是否代表真实的整衣染色。
        /// DrawColor = (1,1,1) / (255,255,255) 在 RimWorld 渲染管线中是着色器中性乘数（不染色、保持贴图原色），
        /// 绝不能描述为 "pure white"；图生图模式下蒙版服装（useWornGraphicMask / CutoutComplex）也不额外加颜色词。
        /// </summary>
        private static bool ShouldUseApparelDrawColor(Apparel item, bool isImg2ImgMode)
        {
            if (item == null) return false;

            Color c = item.DrawColor;

            // 1. 白色 (1,1,1) 是贴图原色直通乘数（不染色），不能当成纯白衣服
            if (c.r >= 0.97f && c.g >= 0.97f && c.b >= 0.97f)
                return false;

            // 2. 无材质、且 CompColorable 未激活染色时，若 graphicData 为默认白则忽略
            CompColorable comp = item.GetComp<CompColorable>();
            bool hasActiveCompColor = comp != null && comp.Active;
            if (item.Stuff == null && !hasActiveCompColor && item.StyleDef?.color == null)
            {
                Color gdColor = item.def.graphicData != null ? item.def.graphicData.color : Color.white;
                if (gdColor.r >= 0.97f && gdColor.g >= 0.97f && gdColor.b >= 0.97f)
                    return false;
            }

            // 3. 图生图模式下，若服装使用蒙版着色（useWornGraphicMask 或 CutoutComplex），
            //    DrawColor 仅染局部细节（甚至空白蒙版完全不染），参考图已包含真实颜色，不额外加颜色前缀
            if (isImg2ImgMode && item.def.apparel != null)
            {
                if (item.def.apparel.useWornGraphicMask)
                    return false;
                if (item.def.graphicData?.shaderType != null &&
                    item.def.graphicData.shaderType.defName == "CutoutComplex")
                    return false;
            }

            return true;
        }

        private static string FormatApparelName(ThingDef def)
        {
            if (def == null) return "clothes";
            string label = (def.label ?? "").ToLower();
            if (!ContainsNonAscii(label) && !string.IsNullOrWhiteSpace(label))
                return label;

            string enRoot = HumanizeDefName(def.defName);
            if (!string.IsNullOrWhiteSpace(enRoot) && !string.Equals(enRoot, label, StringComparison.OrdinalIgnoreCase))
                return $"{label} ({enRoot})";

            return label;
        }

        private static string GetApparelDescription(Pawn p, bool readQuality, bool isImg2ImgMode = false)
        {
            if (p.apparel == null || p.apparel.WornApparel.Count == 0)
            {
                if (p.story.traits != null && p.story.traits.HasTrait(TraitDefOf.Nudist)) return "nude, naked";
                return "wearing simple clothes";
            }
            List<string> clothes = new List<string>();
            foreach (var item in p.apparel.WornApparel)
            {
                string itemName = FormatApparelName(item.def);
                if (ShouldUseApparelDrawColor(item, isImg2ImgMode))
                {
                    string colorName = ColorLibrary.GetBestMatch(item.DrawColor);
                    clothes.Add($"{colorName} {itemName}");
                }
                else
                {
                    clothes.Add(itemName);
                }
            }
            string baseDesc = "wearing " + string.Join(", ", clothes);
            if (readQuality)
            {
                float minHp = 1f;
                foreach (var item in p.apparel.WornApparel) { float hp = (float)item.HitPoints / item.MaxHitPoints; if (hp < minHp) minHp = hp; }
                if (minHp < 0.3f) return $"tattered clothes, torn fabric, {baseDesc}";
                if (minHp < 0.6f) return $"worn out clothes, dirty, {baseDesc}";
            }
            return baseDesc;
        }

        private static string GetDynamicRaceDescription(Pawn pawn)
        {
            StringBuilder descBuilder = new StringBuilder();
            if (pawn.def.description != null) { string clean = CheckAndClean(pawn.def.description); if (!string.IsNullOrEmpty(clean)) descBuilder.Append(clean).Append(" "); }
            if (pawn.genes != null && pawn.genes.Xenotype != null && pawn.genes.Xenotype != XenotypeDefOf.Baseliner) { if (pawn.genes.Xenotype.label != pawn.def.label) { string clean = CheckAndClean(pawn.genes.Xenotype.description); if (!string.IsNullOrEmpty(clean)) descBuilder.Append(clean).Append(" "); } }
            return descBuilder.ToString().Trim();
        }

        private static string CheckAndClean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string lowerText = text.ToLower();
            if (!VisualKeywords.Any(k => lowerText.Contains(k))) return "";
            string clean = Regex.Replace(text, "<.*?>", " ");
            clean = clean.Replace("\n", " ").Replace("\r", " ");
            clean = Regex.Replace(clean, @"\s+", " ");
            if (clean.Length > 200) { clean = clean.Substring(0, 200); int lastDot = clean.LastIndexOf('.'); if (lastDot > 0) clean = clean.Substring(0, lastDot + 1); }
            return clean;
        }

        private static string GetAgeDescription(Pawn p) { float lifeExpectancy = p.def.race.lifeExpectancy; float age = p.ageTracker.AgeBiologicalYearsFloat; float progress = age / lifeExpectancy; if (progress < 0.15f) return "child, young face"; if (progress < 0.25f) return "teenager, young"; if (progress < 0.65f) return "adult, mature"; if (progress < 0.85f) return "middle aged, mature face"; return "elderly, wrinkled face, old"; }
        private static string GetBodyDescription(Pawn p) { if (p.story.bodyType == BodyTypeDefOf.Thin) return "slender body, thin"; if (p.story.bodyType == BodyTypeDefOf.Fat) return "chubby, overweight, round face"; if (p.story.bodyType == BodyTypeDefOf.Hulk) return "muscular, broad shoulders, strong build"; return ""; }

        private static string GetHealthDescription(Pawn p)
        {
            if (p?.health?.hediffSet == null) return "";
            List<string> tags = new List<string>();
            bool hasBandages = false;
            bool hasBleeding = false;
            bool hasFaceScar = false;
            bool hasBionicEye = false;
            bool hasProstheticLimb = false;

            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h == null) continue;
                string partName = h.Part?.def?.defName?.ToLower() ?? "";

                if (h is Hediff_Injury inj)
                {
                    if (inj.IsPermanent())
                    {
                        if (partName.Contains("eye") || partName.Contains("head") || partName.Contains("face") || partName.Contains("nose") || partName.Contains("jaw"))
                            hasFaceScar = true;
                    }
                    else
                    {
                        if (inj.IsTended()) hasBandages = true;
                        if (inj.Bleeding) hasBleeding = true;
                    }
                }
                else if (h is Hediff_AddedPart)
                {
                    string hDef = h.def?.defName?.ToLower() ?? "";
                    if (partName.Contains("eye") && (hDef.Contains("bionic") || hDef.Contains("archotech")))
                        hasBionicEye = true;
                    else if (partName.Contains("arm") || partName.Contains("shoulder") || partName.Contains("leg") || partName.Contains("hand"))
                        hasProstheticLimb = true;
                }
            }

            if (hasBleeding) tags.Add("fresh battle wounds");
            if (hasBandages) tags.Add("bandages");
            if (hasFaceScar) tags.Add("facial scar");
            if (hasBionicEye) tags.Add("glowing bionic eye");
            if (hasProstheticLimb) tags.Add("mechanical prosthetic limb");

            return string.Join(", ", tags);
        }

        private static string TranslateTraitToEnglish(Trait t)
        {
            if (t?.def == null) return "";
            string defName = t.def.defName;
            int deg = t.Degree;

            switch (defName)
            {
                case "Bloodlust": return "fierce menacing expression";
                case "Kind": return "gentle warm smile";
                case "Psychopath": return "cold emotionless gaze";
                case "Cannibal": return "eerie unsettling smile";
                case "Brawler": return "confident battle-ready posture";
                case "Masochist": return "flushed feverish expression";
                case "Wimp": return "timid nervous expression";
                case "Nimble": return "agile poised stance";
                case "Tough": return "sturdy resilient demeanor";
                case "Ascetic": return "humble stoic demeanor";
                case "Greedy":
                case "Jealous": return "haughty proud expression";
                case "Abrasive":
                case "AnnoyingVoice": return "smug defiant grin";
                case "CreepyBreathing": return "gloomy mysterious aura";
                case "Pyromaniac": return "wild excited eyes";
                case "Nudist": return "carefree uninhibited expression";
                case "Gay":
                case "Bisexual":
                case "Asexual": return ""; // 性取向不直接影响外貌立绘，避免产生多余噪声标签
                case "NaturalMood":
                    if (deg > 0) return "cheerful bright smile";
                    if (deg < 0) return "melancholic gloomy expression";
                    break;
                case "Nerves":
                    if (deg > 0) return "calm composed gaze";
                    if (deg < 0) return "anxious tense expression";
                    break;
                case "Beauty":
                    if (deg > 0) return "stunningly attractive face";
                    if (deg < 0) return "rough weathered face";
                    break;
                case "Industriousness":
                    if (deg > 0) return "focused diligent expression";
                    if (deg < 0) return "lazy relaxed expression";
                    break;
            }

            string label = (t.Label ?? "").Replace("，", ",");
            if (!ContainsNonAscii(label)) return label;

            string enTrait = HumanizeDefName(defName);
            if (!string.IsNullOrWhiteSpace(enTrait))
                return $"{enTrait} demeanor ({label})";

            return label;
        }
    }
}
