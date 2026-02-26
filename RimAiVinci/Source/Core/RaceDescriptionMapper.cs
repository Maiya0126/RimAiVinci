using System.Collections.Generic;
using Verse;

namespace RimAiVinci
{
    public static class RaceDescriptionMapper
    {
        // 核心字典：这里存放我们人工精调的高质量 Tag
        private static Dictionary<string, string> raceDescriptions = new Dictionary<string, string>()
        {
            { "human", "human" }, 

            // === 修正：萌螈族 (MoeLotl) ===
            { "moelotl", "moelotl, axolotl humanoid, (3 pairs of pink external gills:1.5), (6 feathered pink ears:1.4), (axolotl frills:1.4), fin ears, wet skin, silly expression, :3" },

            // === 米莉拉 (Milira) ===
            { "milira", "milira, beautiful angel girl, white feathered wings on waist, floating geometric halo" },
            { "milira class", "beautiful angel girl, white feathered wings on waist, floating geometric halo" },

            // === 鼠族 (Ratkin) ===
            { "ratkin", "cute anime character, large round mouse ears on head, human face, normal skin, long thin mouse tail, short stature, petite, kawaii style" },
            { "ratkin_su", "cute anime character, large round mouse ears on head, human face, normal skin, long thin mouse tail, short stature" },

            // === 其他种族字典 ===
            { "kijin", "kijin, cat ears, cat tail, lazy expression, gentle eyes" },
            { "kurin", "kurin, fox ears, three fox tails, multiple tails, sci-fi aesthetic, smart expression" },
            { "revia", "revia, fox ears, huge fluffy fox tail, beautiful face, bloodthirsty expression" },
            { "wofen", "wolf ears, wolf tail, cyberpunk aesthetic, glowing eyes, tactical atmosphere" },
            { "maru", "snow leopard ears, snow leopard tail, spots on tail, snowy background" },
            { "dragonian", "dragon horns, thick dragon tail, pale skin, innocent expression" },
            { "epona", "epona, horse ears, horse tail, steampunk aesthetic, brass accessories" },
            { "vivi", "vivi, bee humanoid, insect wings, bee wings, bee antenna, fairy, pixie, translucent wings, garden background" },
            { "mincho", "mincho, slime humanoid, mint green skin, chocolate brown accents, semi-transparent body, viscous liquid texture" },
            { "yuran", "rabbit ears, bunny ears, short stature, petite, shrine maiden aura" },
            { "moyo", "deep blue skin, wet skin, slime texture, sea slug antenna, sea slug tail, bioluminescence, deep sea cyberpunk background" },
            { "mugirl", "cow ears, cow horns, cow tail, tall, voluptuous body, thick thighs, curvy" },
            { "paniel", "robot, automaton, ball-jointed doll, artificial skin, mechanical parts, lop-ears (mechanical), dog ears style headsets" },
            { "impid", "glowing golden eyes, ethereal tentacles on body, small fins on head, semi-transparent ghost limbs, sci-fi horror aesthetic" },
            { "yaelong", "chinese dragon horns, dragon tail, imperial style, eastern dragon features" },
            { "chaos spirit", "halo over head, white feathers, surrounded by floating bone constructs, black flames aura, dark fantasy style" },
            { "punisher", "pale skin, blue-black demon wings, large demon horns, purple-black halo, poker face" },
            { "false god", "child body, loli, rainbow halo, asymmetrical wings, three black wings on left, three white wings on right, divine aura" },
            { "kurila", "golden halo, single orange horn on forehead, silver mechanical ears, ten floating flame funnels behind back forming wings" },
            { "solark", "three blue dragon horns, dragon wings, thick dragon tail, blue fire aura, black antimatter halo" },
            { "sin spirit", "ghost, huge golden halo with hanging chains, floating black weapons, blue cold flames, rainbow halo with black hole" },
            { "idian", "mecha musume, mechanical parts, cyborg, glowing energy wings, floating bits" },
            { "luciferium", "cyborg, industrial robot parts, heavy weaponry integrated, nano-machines cloud, worn-out metal texture" },
            { "anty", "ant humanoid, insect features, chitin skin, insect antenna, compound eyes, red skin" },
            { "rabbie", "moon rabbit, long rabbit ears, space soldier aura, starry sky background" }
        };

        public static string GetRaceVisuals(Pawn pawn)
        {
            if (pawn == null || pawn.def == null) return "";

            string defName = pawn.def.defName.ToLower();

            // 1. 尝试匹配 defName (如 alien_race_ratkin)
            if (TryFindKeyword(defName, out string desc1)) return desc1;

            // 2. 尝试匹配 label (如 Ratkin)
            string label = pawn.def.label.ToLower();
            if (TryFindKeyword(label, out string desc2)) return desc2;

            // 3. 人类特殊处理
            if (defName == "human") return "human";

            // 4. ✨ 核心修改：如果字典没找到，返回空字符串 ✨
            // 这告诉 PawnPromptBuilder：“我不认识这个种族，请你去读它的游戏描述！”
            return "";
        }

        private static bool TryFindKeyword(string key, out string result)
        {
            foreach (var kvp in raceDescriptions)
            {
                if (key.Contains(kvp.Key))
                {
                    result = kvp.Value;
                    return true;
                }
            }
            result = "";
            return false;
        }
    }
}