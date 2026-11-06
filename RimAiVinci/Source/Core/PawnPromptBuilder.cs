using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
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
        public bool readBackground = true;
        public bool readRace = true;
        public bool readHealth = true;
        public bool readQuality = true;
        public bool readSkinColor = true;

        /// <summary>自然语言模式（Qwen/Flux/T5/LLM 系编码器）：跳过 (x:权重) 语法</summary>
        public bool naturalLanguageMode = false;
    }

    /// <summary>
    /// 生成期上下文：传递种族防变异负面词到各 API 客户端。
    /// 生成请求发出后自动失效（OneShot 语义）。
    /// </summary>
    public static class PromptContext
    {
        public static string RaceNegative;

        /// <summary>设置本次生成的种族负面词（创作窗在发起生成前调用）</summary>
        public static void Begin(Pawn pawn)
        {
            RaceNegative = null;
            try
            {
                string custom = CustomRacePromptStore.GetCustom(pawn);
                if (!string.IsNullOrEmpty(custom))
                {
                    // 自定义描述：不追加内置负面词（玩家全权接管）
                    RaceNegative = null;
                    return;
                }
                RaceNegative = RaceDescriptionMapper.GetRaceNegative(pawn);
            }
            catch { }
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

            // 1. 风格前缀（自然语言模式下不加权重语法）
            if (opt.style != ArtStyle.Custom)
            {
                if (opt.isImg2ImgMode)
                {
                    string breakStructure = opt.naturalLanguageMode
                        ? "humanoid body structure with detailed arms, hands and legs, not pixel art, not game sprite, "
                        : "(humanoid body structure:1.5), (detailed arms and hands:1.4), (detailed legs:1.4), (NOT pixel art:1.5), (NOT game sprite:1.5), ";
                    switch (opt.style)
                    {
                        case ArtStyle.Standard: sb.Append($"masterpiece, best quality, mobile game illustration, detailed cel shading, {breakStructure} dynamic pose, "); break;
                        case ArtStyle.Realistic: sb.Append($"photorealistic, 8k uhd, cosplay photography, real life{(opt.naturalLanguageMode ? ", " : ", (hyper realistic:1.4), ")}, {breakStructure}{(opt.naturalLanguageMode ? "" : "(skin texture:1.3), ")}cinematic lighting, "); break;
                        case ArtStyle.Miyazaki: sb.Append($"studio ghibli style, hayao miyazaki, 1990s anime style, hand painted watercolor, {breakStructure} soft colors, "); break;
                        case ArtStyle.Chibi: sb.Append($"chibi, nendoroid figure, 3d render style, blind box toy{(opt.naturalLanguageMode ? ", " : ", (cute body with arms and legs:1.4), ")}big head, standing on base, "); break;
                    }
                }
                else
                {
                    switch (opt.style)
                    {
                        case ArtStyle.Standard: sb.Append("masterpiece, best quality, rimworld art style, digital art, detailed painting, character concept art, "); break;
                        case ArtStyle.Realistic: sb.Append("photorealistic, 8k, unreal engine 5, detailed texture, sci-fi concept art, dramatic lighting, "); break;
                        case ArtStyle.Miyazaki: sb.Append("studio ghibli style, anime style, vibrant colors, picturesque, hand drawn, "); break;
                        case ArtStyle.Chibi: sb.Append("chibi, cute, big head, kawaii, sticker art, thick outline, "); break;
                    }
                }
            }
            else
            {
                sb.Append("masterpiece, best quality, ");
            }

            if (opt.forceFullBody)
            {
                if (opt.isImg2ImgMode && opt.style == ArtStyle.Realistic) sb.Append("full body photograph, wide angle shot, showing shoes, ");
                else sb.Append("full body, standing, ");
            }

            // 2. 种族与描述（修复：用英文 defName，避免中文 label 污染提示词）
            string gender = pawn.gender == Gender.None ? "" : pawn.gender.ToString();

            if (opt.readRace)
            {
                // 优先级：玩家自定义 > 内置词库（按语法模式选形式）> 动态种族描述
                string customVisuals = CustomRacePromptStore.GetCustom(pawn);
                if (!string.IsNullOrEmpty(customVisuals))
                {
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
                        string racePrefix = (raceDefName == "Human") ? "human" : $"(RimWorld {raceDefName} race:1.2)";
                        sb.Append($"{gender}, {racePrefix}, {dynamicDesc}, ");
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
            if (opt.readBodyType) sb.Append($"{GetBodyDescription(pawn)}, ");

            if (opt.readHair)
            {
                string colorName = ColorLibrary.GetBestMatch(pawn.story.HairColor, "hair");
                string hairStyleName = pawn.story.hairDef?.label ?? "hair";
                if (opt.naturalLanguageMode)
                    sb.Append($"{colorName} hair styled as {hairStyleName}, ");
                else
                    sb.Append($"({colorName}:1.3) {hairStyleName}, ");
            }

            // 4. 服装与其他
            if (opt.readApparel) { string a = GetApparelDescription(pawn, opt.readQuality); if (a != "") sb.Append($"{a}, "); }
            if (opt.readHealth) { string h = GetHealthDescription(pawn); if (h != "") sb.Append($"{h}, "); }
            if (opt.readTraits) foreach (var t in pawn.story.traits.allTraits) sb.Append($"{TranslateTraitToEnglish(t)}, ");

            if (!string.IsNullOrEmpty(safeExtra)) sb.Append($"{safeExtra}, ");

            if (opt.readBackground && pawn.story.Adulthood != null)
            {
                float bgWeight = opt.forceFullBody ? 0.6f : 0.8f;
                string jobTitle = pawn.story.Adulthood.title.Replace("(", "").Replace(")", "");
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
            sb.Append("Create a high-quality 2D character sprite based on this prototype.\nCore Requirements:\n- Art Style: RimWorld art style, high quality anime style, flat color, clean lines.\n");

            string race = pawn.def.label;
            string gender = pawn.gender.ToString();
            string age = GetAgeDescription(pawn);
            string body = GetBodyDescription(pawn);
            string skin = ColorLibrary.GetBestMatch(pawn.story.SkinColor, "skin");
            sb.Append($"- Race & Vibe: {race} ({gender}). Appearance is {age}, {body}, {skin}. ");

            if (opt.readSkinColor) { string eyeColor = GetEyeColorDescription(pawn); if (!string.IsNullOrEmpty(eyeColor)) sb.Append($"- Eyes: {eyeColor}\n"); }
            if (opt.readTraits && pawn.story.traits.allTraits.Count > 0) { sb.Append("Reflect traits: "); foreach (var t in pawn.story.traits.allTraits) sb.Append($"{TranslateTraitToEnglish(t)}, "); sb.Append("\n"); } else sb.Append("\n");

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
            if (opt.readHair) { string hairColor = ColorLibrary.GetBestMatch(pawn.story.HairColor, "hair"); string hairStyle = pawn.story.hairDef?.label ?? "hair"; sb.Append($"- Hair: {hairColor} {hairStyle}\n"); }
            if (opt.readApparel) { string apparel = GetApparelDescription(pawn, false); if (!string.IsNullOrEmpty(apparel)) sb.Append($"- Clothing: {apparel}\n"); }

            sb.Append("Composition & Camera:\n- Shot: Full body shot.\n- Background: Simple white.\nAction:\n- Pose: Standing.\n");

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

        private static string GetApparelDescription(Pawn p, bool readQuality)
        {
            if (p.apparel == null || p.apparel.WornApparel.Count == 0)
            {
                if (p.story.traits.HasTrait(TraitDefOf.Nudist)) return "nude, naked";
                return "wearing simple clothes";
            }
            List<string> clothes = new List<string>();
            foreach (var item in p.apparel.WornApparel)
            {
                string defLabel = item.def.label.ToLower();
                string colorName = ColorLibrary.GetBestMatch(item.DrawColor);
                clothes.Add($"{colorName} {defLabel}");
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
        private static string GetHealthDescription(Pawn p) { return ""; }
        private static string TranslateTraitToEnglish(Trait t) { return t.Label.Replace("，", ","); }
    }
}
