using System.Collections.Generic;
using Verse;

namespace RimAiVinci
{
    public static class RaceDescriptionMapper
    {
        // 核心字典：纯粹的解剖学与服饰特征，剔除强制画风词，保留防变异结构词
        private static readonly Dictionary<string, string> raceDescriptions = new Dictionary<string, string>()
        {
            // === 萌螈族 (MoeLotl) ===
            { "moelotl", "cute anime character,human face,(axolotl humanoid:1.3), (three pairs of external gills:1.4), axolotl tail, thick tail, eastern fantasy clothing, hanfu elements, round face, silly expression" },
            { "萌螈", "cute anime character,human face,(axolotl humanoid:1.3), (three pairs of external gills:1.4), axolotl tail, thick tail, eastern fantasy clothing, hanfu elements, round face, silly expression" },
 
            // === 米莉拉 (Milira) ===
            { "milira", "beautiful young woman, normal human proportions, human face, (large pure white feathered angel wings growing from lower back:1.5), lumbar wings, waist wings, (floating intricate futuristic geometric halo diagonally above left side of head:1.4), glowing halo, elegant, divine aura" },
            { "milira class", "beautiful young woman, normal human proportions, human face, (large pure white feathered angel wings growing from lower back:1.5), lumbar wings, waist wings, (floating intricate futuristic geometric halo diagonally above left side of head:1.4), glowing halo, elegant, divine aura" },
            { "米莉拉", "beautiful young woman, normal human proportions, human face, (large pure white feathered angel wings growing from lower back:1.5), lumbar wings, waist wings, (floating intricate futuristic geometric halo diagonally above left side of head:1.4), glowing halo, elegant, divine aura" },
            { "天翼", "beautiful young woman, normal human proportions, human face, (large pure white feathered angel wings growing from lower back:1.5), lumbar wings, waist wings, (floating intricate futuristic geometric halo diagonally above left side of head:1.4), glowing halo, elegant, divine aura" },
            { "天空精灵", "beautiful young woman, normal human proportions, human face, (large pure white feathered angel wings growing from lower back:1.5), lumbar wings, waist wings, (floating intricate futuristic geometric halo diagonally above left side of head:1.4), glowing halo, elegant, divine aura" },
            
            // === 鼠族 (Ratkin) ===
            { "ratkin", "cute anime character, (large round mouse ears on top of head:1.4), human face, normal skin, (long thin mouse tail:1.2), short stature, petite, kawaii style" },
            { "ratkin_su", "cute anime character, (large round mouse ears on top of head:1.4), human face, normal skin, (long thin mouse tail:1.2), short stature, petite, kawaii style" },
            { "鼠族", "cute anime character, (large round mouse ears on top of head:1.4), human face, normal skin, (long thin mouse tail:1.2), short stature, petite, kawaii style" },
            
            // === 悠兰 (Yuran) ===
            { "yuran", "cute anime character,kemonomimi, human face, long white rabbit ears, (round face:1.3), tiny nose, red eyes, pale skin, short stature, petite, japanese traditional clothing, miko" },
            { "悠兰", "cute anime character,kemonomimi, human face, long white rabbit ears, (round face:1.3), tiny nose, red eyes, pale skin, short stature, petite, japanese traditional clothing, miko" },

            // === 龙人 (Dragonian) ===
            { "dragonian", "dragon horns, silver-white scales, thick dragon tail, pale skin, human face, extremely beautiful, innocent expression" },
            { "龙人", "dragon horns, silver-white scales, thick dragon tail, pale skin, human face, extremely beautiful, innocent expression" },

            // === 沃芬 (Wolfen) ===
            { "wofen", "kemonomimi, fluffy wolf ears, wolf tail, human face, cyberpunk aesthetic, glowing eyes in the dark, tactical atmosphere" },
            { "wolfen", "kemonomimi, fluffy wolf ears, wolf tail, human face, cyberpunk aesthetic, glowing eyes in the dark, tactical atmosphere" },
            { "沃芬", "kemonomimi, fluffy wolf ears, wolf tail, human face, cyberpunk aesthetic, glowing eyes in the dark, tactical atmosphere" },

            // === 魅狐 (Kurin) ===
            { "kurin", "kemonomimi, fox ears, three fox tails, multiple tails, human face, sci-fi aesthetic, smart expression" },
            { "魅狐", "kemonomimi, fox ears, three fox tails, multiple tails, human face, sci-fi aesthetic, smart expression" },
            { "奎琳", "kemonomimi, fox ears, three fox tails, multiple tails, human face, sci-fi aesthetic, smart expression" },

            // === 绮罗 (Kiro) ===
            { "kiro", "kemonomimi, nekomimi, fluffy cat ears on head, cat tail, human face, round face, casual clothing, lazy but friendly expression" },
            { "绮罗", "kemonomimi, nekomimi, fluffy cat ears on head, cat tail, human face, round face, casual clothing, lazy but friendly expression" },
            
            // === 烟烬 (Yanjin) ===
            { "yanjin", "human face, (fin-like appendages on head matching hair color:1.3), (multiple thick glossy black bio-organic tentacles emerging from back:1.5), special tactical armor, sci-fi survivor aesthetic, wasteland survivor, mysterious atmosphere" },
            { "烟烬", "human face, (fin-like appendages on head matching hair color:1.3), (multiple thick glossy black bio-organic tentacles emerging from back:1.5), special tactical armor, sci-fi survivor aesthetic, wasteland survivor, mysterious atmosphere" },

            // === 古龙族 (Elder Dragon) ===
            { "古龙", "human face, silver-white hair, snow-white skin, golden eyes, thick black dragon tail, dragon wings, dark silver dragon horns, extremely beautiful" },

            // === 美狐 (Miho) ===
            { "miho", "kemonomimi, human face, fluffy fox ears, soft flexible fluffy fox tail, extremely attractive, lazy aura" },
            { "美狐", "kemonomimi, human face, fluffy fox ears, soft flexible fluffy fox tail, extremely attractive, lazy aura" },

            // === 蒂尼玛尔 (Tinymar) ===
            { "tinymar", "cute anime character,extremely short stature, petite, child-like proportions, round face, large eyes, tiny mouth, innocent expression, cheerful" },
            { "蒂尼玛尔", "cute anime character,extremely short stature, petite, child-like proportions, round face, large eyes, tiny mouth, innocent expression, cheerful" },
            { "蒂尼玛", "cute anime character,extremely short stature, petite, child-like proportions, round face, large eyes, tiny mouth, innocent expression, cheerful" },
            { "小人族", "cute anime character,extremely short stature, petite, child-like proportions, round face, large eyes, tiny mouth, innocent expression, cheerful" },
            { "半身人", "cute anime character,extremely short stature, petite, child-like proportions, round face, large eyes, tiny mouth, innocent expression, cheerful" },

            // === 安缇 (Anty) ===
            { "anty", "(black ant antennae on head:1.3), sci-fi heavy armor, futuristic armored humanoid, human face, normal human skin" },
            { "安缇", "(black ant antennae on head:1.3), sci-fi heavy armor, futuristic armored humanoid, human face, normal human skin" },
            { "蚂蚁娘", "(black ant antennae on head:1.3), sci-fi heavy armor, futuristic armored humanoid, human face, normal human skin" },

            // === 维维 (Vivi) ===
            { "vivi", "cute anime character,human face, extremely short stature, petite, child-like proportions, (bee antennae on head:1.3), (translucent insect wings on back:1.2), yellow and black themed clothing" },
            { "维维", "cute anime character,human face, extremely short stature, petite, child-like proportions, (bee antennae on head:1.3), (translucent insect wings on back:1.2), yellow and black themed clothing" },
            { "蜜蜂娘", "human face, extremely short stature, petite, child-like proportions, (bee antennae on head:1.3), (translucent insect wings on back:1.2), yellow and black themed clothing" },

            // === 珉巧 (Mincho) ===
            { "mincho", "cute anime character,human face, slime humanoid, monster girl, (hair made of pastel slime:1.3), (lower body melting into slime puddle:1.4), semi-transparent liquid body, sweet dessert theme" },
            { "珉巧", "cute anime character,human face, slime humanoid, monster girl, (hair made of pastel slime:1.3), (lower body melting into slime puddle:1.4), semi-transparent liquid body, sweet dessert theme" },
            { "薄荷巧克力史莱姆", "human face, slime humanoid, monster girl, (hair made of pastel slime:1.3), (lower body melting into slime puddle:1.4), semi-transparent liquid body, sweet dessert theme" },

            // === 莫约 (Moyo) ===
            { "moyo", "cute anime character,human face, pale blue skin, (glowing blue eyes:1.2), bioluminescent markings, deep sea cyberpunk aesthetic, form-fitting sci-fi suit, thick thighs" },
            { "莫约", "cute anime character,human face, pale blue skin, (glowing blue eyes:1.2), bioluminescent markings, deep sea cyberpunk aesthetic, form-fitting sci-fi suit, thick thighs" },
            { "深海蛞蝓", "human face, pale blue skin, (glowing blue eyes:1.2), bioluminescent markings, deep sea cyberpunk aesthetic, form-fitting sci-fi suit, thick thighs" },

            // === 帕尼尔 (Paniel) ===
            { "paniel", "cute anime character,steampunk automaton, (mechanical dog ears headgear:1.3), Victorian military uniform, human face, normal skin, elegant" },
            { "帕尼尔", "cute anime character,steampunk automaton, (mechanical dog ears headgear:1.3), Victorian military uniform, human face, normal skin, elegant" },
            { "自动机兵", "cute anime character,steampunk automaton, (mechanical dog ears headgear:1.3), Victorian military uniform, human face, normal skin, elegant" },

            // === 其他原版/老种族 ===
            { "kijin", "kemonomimi, human face, cat ears, cat tail, lazy expression, gentle eyes" },
            { "revia", "kemonomimi, human face, fox ears, huge fluffy fox tail, beautiful face, bloodthirsty expression" },
            { "maru", "cute anime character,kemonomimi, human face, snow leopard ears, snow leopard tail, spots on tail, snowy background" },
            { "epona", "cute anime character,kemonomimi, human face, horse ears, horse tail, steampunk aesthetic, brass accessories" },
            { "mugirl", "cute anime character,kemonomimi, human face, cow ears, cow horns, cow tail, tall, voluptuous body, thick thighs, curvy" },
            { "impid", "human face, glowing golden eyes, ethereal tentacles on body, small fins on head, semi-transparent ghost limbs, sci-fi horror aesthetic" },
            { "yaelong", "cute anime character,human face, chinese dragon horns, dragon tail, imperial style, eastern dragon features" },
            { "chaos spirit", "human face, halo over head, white feathers, surrounded by floating bone constructs, black flames aura, dark fantasy style" },
            { "punisher", "human face, pale skin, blue-black demon wings, large demon horns, purple-black halo, poker face" },
            { "false god", "human face, child body, rainbow halo, asymmetrical wings, three black wings on left, three white wings on right, divine aura" },
            { "kurila", "human face, golden halo, single orange horn on forehead, silver mechanical ears, ten floating flame funnels behind back forming wings" },
            { "solark", "human face, three blue dragon horns, dragon wings, thick dragon tail, blue fire aura, black antimatter halo" },
            { "sin spirit", "human face, ghost, huge golden halo with hanging chains, floating black weapons, blue cold flames, rainbow halo with black hole" },
            { "idian", "human face, mechanical parts, cyborg, glowing energy wings, floating bits" },
            { "luciferium", "human face, cyborg, industrial robot parts, heavy weaponry integrated, nano-machines cloud, worn-out metal texture" },
            { "rabbie", "cute anime character,kemonomimi, human face, moon rabbit, long rabbit ears, space soldier aura, starry sky background" }
        };

        public static string GetRaceVisuals(Pawn pawn)
        {
            if (pawn == null || pawn.def == null) return "";

            // 防崩核心逻辑：先判空，再转小写，防止 NullReferenceException
            string defName = pawn.def.defName;
            if (!string.IsNullOrEmpty(defName))
            {
                if (TryFindKeyword(defName.ToLower(), out string desc1)) return desc1;
            }

            string label = pawn.def.label;
            if (!string.IsNullOrEmpty(label))
            {
                if (TryFindKeyword(label.ToLower(), out string desc2)) return desc2;
            }

            // 如果字典没找到，安全返回空字符串
            return "";
        }

        private static bool TryFindKeyword(string key, out string result)
        {
            result = "";
            // 兜底安全检查
            if (string.IsNullOrEmpty(key)) return false;

            foreach (var kvp in raceDescriptions)
            {
                if (key.Contains(kvp.Key))
                {
                    result = kvp.Value;
                    return true;
                }
            }
            return false;
        }
    }
}