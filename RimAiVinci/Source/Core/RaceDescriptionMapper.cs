using System.Collections.Generic;
using Verse;

namespace RimAiVinci
{
    public class RacePromptEntry
    {
        /// <summary>Danbooru 锚点标签形式（SD1.5/SDXL CLIP 系，可含权重语法）</summary>
        public string tagForm;
        /// <summary>自然语言描述形式（Qwen/Flux/T5/LLM 系）</summary>
        public string nlForm;
        /// <summary>该种族专属负面提示词（防变异），可选</summary>
        public string negative;

        public RacePromptEntry(string tag, string nl, string neg = null)
        {
            tagForm = tag; nlForm = nl; negative = neg;
        }
    }

    public static class RaceDescriptionMapper
    {
        // 双形式词库：tagForm 用 Danbooru 高频锚点 + 细节 + 权重；nlForm 用自然语言强化句。
        // negative 为防变异负面词，构建提示词时追加到负面提示。
        private static readonly Dictionary<string, RacePromptEntry> raceDescriptions = new Dictionary<string, RacePromptEntry>()
        {
            // === 萌螈族 (MoeLotxol) === 六角恐龙娘：头顶三对外鳃是核心特征
            { "moelotl", new RacePromptEntry(
                "1girl, animal girl, axolotl girl, kemonomimi, human girl, human face, (three pairs of pink frilly external gills on head:1.4), long thick axolotl tail, round face, cheerful silly expression, hanfu, eastern fantasy clothing",
                "a cute anime girl of the axolotl species: she looks fully human except she has exactly three pairs of soft pink frilly gills sticking out from the sides of her head like an axolotl, and a long thick pale-pink aquatic tail; round cheerful face, wearing hanfu-style eastern fantasy clothes",
                "full animal body, fish, no arms, no legs, scales covering face, full axolotl, actual axolotl") },
            { "萌螈", null }, // 别名占位：运行时回落到 moelotl 处理（见 TryFindKeyword 合并逻辑）

            // === 米莉拉 (Milira) === 腰部天使翼+悬浮光环
            { "milira", new RacePromptEntry(
                "1girl, angel, human girl, human face, normal human proportions, (large white feathered angel wings growing from lower back:1.4), waist wings, (floating futuristic geometric halo above head:1.3), glowing halo, elegant, divine aura",
                "a beautiful young woman who looks fully human, except for two large pure-white feathered angel wings growing from her lower back, and an intricate glowing futuristic geometric halo floating diagonally above her head; elegant, divine presence",
                "bird, bird wings on arms, harpy, feathers covering body") },
            { "milira class", null },
            { "米莉拉", null },
            { "天翼", null },
            { "天空精灵", null },

            // === 鼠族 (Ratkin) === 圆耳鼠娘
            { "ratkin", new RacePromptEntry(
                "1girl, mouse girl, animal girl, kemonomimi, human girl, human face, normal skin, (large round mouse ears on top of head:1.4), (long thin mouse tail:1.2), short stature, petite, kawaii, casual clothing",
                "a cute petite anime girl who looks fully human except for two large round mouse ears on top of her head and a long thin mouse tail; short stature, kawaii style",
                "full mouse, actual rat, rodent face, fur covering body, mouse snout") },
            { "ratkin_su", null },
            { "鼠族", null },

            // === 悠兰 (Yuran) === 白兔耳巫女
            { "yuran", new RacePromptEntry(
                "1girl, rabbit girl, animal girl, kemonomimi, human girl, human face, long white rabbit ears, round face, tiny nose, red eyes, pale skin, short stature, petite, miko, japanese traditional clothing",
                "a cute petite anime girl who looks fully human except for two long white rabbit ears on her head; red eyes, pale skin, round face, wearing japanese miko shrine-maiden clothing",
                "full rabbit, actual bunny, animal face, fur covering body") },
            { "悠兰", null },

            // === 龙人 (Dragonian) ===
            { "dragonian", new RacePromptEntry(
                "1girl, dragon girl, human girl, human face, dragon horns, silver-white scales on cheeks and shoulders, thick dragon tail, pale skin, extremely beautiful, innocent expression",
                "a beautiful anime girl who looks fully human except for a pair of dragon horns, patches of silver-white scales on her cheeks and shoulders, and a thick dragon tail",
                "full dragon, dragon face, dragon snout, scales covering whole body, wings on arms") },
            { "龙人", null },

            // === 沃芬 (Wolfen) === 赛博狼娘
            { "wolfen", new RacePromptEntry(
                "1girl, wolf girl, animal girl, kemonomimi, human girl, human face, fluffy wolf ears, wolf tail, cyberpunk aesthetic, tactical clothing, glowing eyes",
                "a young woman who looks fully human except for fluffy wolf ears on her head and a wolf tail; cyberpunk tactical outfit, faintly glowing eyes",
                "full wolf, werewolf, animal face, fur covering body, wolf snout") },
            { "wofen", null },
            { "沃芬", null },

            // === 魅狐 (Kurin) === 三尾狐娘
            { "kurin", new RacePromptEntry(
                "1girl, fox girl, animal girl, kemonomimi, human girl, human face, fox ears, (three fox tails:1.3), multiple tails, animal ear fluff, sci-fi clothing, smart expression",
                "a clever anime girl who looks fully human except for pointed fox ears and exactly three fluffy fox tails; sci-fi styled outfit",
                "full fox, actual fox, animal face, fur covering body, fox snout, nine tails") },
            { "魅狐", null },
            { "奎琳", null },

            // === 绮罗 (Kiro) === 懒散猫娘
            { "kiro", new RacePromptEntry(
                "1girl, cat girl, animal girl, kemonomimi, nekomimi, human girl, human face, fluffy cat ears, cat tail, round face, casual clothing, lazy relaxed expression, half-closed eyes",
                "a lazy but friendly anime cat girl: fully human except for fluffy cat ears and a long cat tail; round face, relaxed half-closed eyes, casual comfy clothes",
                "full cat, actual cat, animal face, fur covering body") },
            { "绮罗", null },

            // === 烟烬 (Yanjin) === 背部生化触手
            { "yanjin", new RacePromptEntry(
                "1girl, human girl, human face, (fin-like head fins matching hair color:1.2), (thick glossy black tentacles emerging from back:1.4), tactical armor, sci-fi survivor, wasteland atmosphere",
                "a mysterious sci-fi survivor girl: fully human face, but with fin-like appendages on her head matching her hair color, and several thick glossy black bio-organic tentacles emerging from her back; special tactical armor, wasteland vibe",
                "octopus head, tentacle face, tentacles replacing arms, monster") },
            { "烟烬", null },

            // === 古龙族 (Elder Dragon) ===
            { "古龙", new RacePromptEntry(
                "1girl, dragon girl, human girl, human face, silver-white long hair, snow-white skin, golden eyes, dark silver dragon horns, dragon wings, thick black dragon tail, extremely beautiful",
                "an extremely beautiful anime girl with silver-white long hair, snow-white skin and golden eyes; fully human face, but with dark silver dragon horns, dragon wings on her back and a thick black dragon tail",
                "full dragon, dragon face, dragon snout, elderly person") },

            // === 美狐 (Miho) ===
            { "miho", new RacePromptEntry(
                "1girl, fox girl, animal girl, kemonomimi, human girl, human face, fluffy fox ears, soft fluffy fox tail, extremely attractive, lazy aura",
                "an extremely attractive anime fox girl: fully human except for soft fluffy fox ears and one big fluffy fox tail; lazy, relaxed aura",
                "full fox, actual fox, animal face, multiple tails") },
            { "美狐", null },

            // === 蒂尼玛尔 (Tinymar) === 小人族
            { "tinymar", new RacePromptEntry(
                "1girl, tiny girl, human girl, human face, extremely short stature, petite, child-like proportions, round face, large eyes, tiny mouth, innocent expression, cheerful",
                "a cheerful tiny anime girl with child-like proportions: extremely short stature, petite build, round face with large eyes and a tiny mouth; fully human appearance",
                "actual child photo, dwarf fantasy creature, non-human") },
            { "蒂尼玛尔", null },
            { "蒂尼玛", null },
            { "小人族", null },
            { "半身人", null },

            // === 安缇 (Anty) === 蚂蚁触角装甲兵
            { "anty", new RacePromptEntry(
                "1girl, insect girl, human girl, human face, (black ant antennae on head:1.3), sci-fi heavy armor, futuristic armored suit, normal human skin",
                "a girl in sci-fi heavy power armor: fully human face and skin, but with two black ant-like antennae sticking out from her head; futuristic armored suit",
                "insect face, mandibles, compound eyes, bug monster") },
            { "安缇", null },
            { "蚂蚁娘", null },

            // === 维维 (Vivi) === 蜜蜂娘
            { "vivi", new RacePromptEntry(
                "1girl, bee girl, insect girl, human girl, human face, petite, (bee antennae on head:1.3), (translucent insect wings on back:1.2), yellow and black themed dress",
                "a petite anime bee girl: fully human face, but with two bee antennae on her head and translucent insect wings on her back; yellow-black themed cute dress",
                "full bee, actual insect, insect face, compound eyes, wasp waist monster") },
            { "维维", null },
            { "蜜蜂娘", null },

            // === 珉巧 (Mincho) === 史莱姆娘
            { "mincho", new RacePromptEntry(
                "1girl, slime girl, monster girl, human girl, human face, (hair made of pastel mint-green slime:1.3), (body made of semi-transparent slime:1.3), goo girl, sweet dessert theme",
                "a cute anime slime girl: her whole body is made of semi-transparent pastel mint-green slime including her hair, but she has a fully human face and girl proportions; sweet dessert-like appearance",
                "full slime puddle, no face, blob, formless") },
            { "珉巧", null },
            { "薄荷巧克力史莱姆", null },

            // === 莫约 (Moyo) === 深海蛞蝓
            { "moyo", new RacePromptEntry(
                "1girl, human girl, human face, pale blue skin, glowing blue eyes, bioluminescent markings on skin, form-fitting sci-fi suit, deep sea aesthetic",
                "an anime girl with pale blue skin and softly glowing blue eyes, faint bioluminescent markings on her skin; fully human proportions, wearing a form-fitting deep-sea themed sci-fi suit",
                "full slug, sea creature, no legs, fish, gills on neck") },
            { "莫约", null },
            { "深海蛞蝓", null },

            // === 帕尼尔 (Paniel) === 蒸汽朋克自动机兵
            { "paniel", new RacePromptEntry(
                "1girl, automaton girl, android, human girl, human face, (mechanical dog ears headgear:1.3), steampunk, victorian military uniform, brass accessories, elegant",
                "an elegant steampunk automaton girl: fully human face, wearing a Victorian military uniform with brass accessories, and a mechanical dog-ears headgear on her head",
                "robot face, full machine body, exposed gears on face") },
            { "帕尼尔", null },
            { "自动机兵", null },

            // === 其他原版/老种族 ===
            { "kijin", new RacePromptEntry(
                "1girl, cat girl, kemonomimi, human face, cat ears, cat tail, gentle eyes, lazy expression",
                "a gentle anime cat girl with cat ears and a cat tail, fully human face, lazy relaxed expression",
                "full cat, animal face") },
            { "revia", new RacePromptEntry(
                "1girl, fox girl, kemonomimi, human face, fox ears, huge fluffy fox tail, beautiful, bloodthirsty dangerous expression",
                "a beautiful anime fox girl with fox ears and one huge fluffy fox tail; fully human face with a bloodthirsty, dangerous expression",
                "full fox, animal face, multiple tails") },
            { "maru", new RacePromptEntry(
                "1girl, snow leopard girl, kemonomimi, human face, snow leopard ears, snow leopard tail with spots, white fur trim",
                "an anime snow leopard girl: fully human face, with spotted snow-leopard ears and a long spotted tail",
                "full leopard, animal face, fur covering body") },
            { "epona", new RacePromptEntry(
                "1girl, horse girl, kemonomimi, human face, horse ears, horse tail, steampunk aesthetic, brass accessories",
                "an anime horse girl: fully human face, with horse ears and a horse tail; steampunk outfit with brass accessories",
                "full horse, animal face, centaur, four legs") },
            { "mugirl", new RacePromptEntry(
                "1girl, cow girl, kemonomimi, human face, cow ears, small cow horns, cow tail, tall, curvy, thick thighs",
                "a tall curvy anime cow girl: fully human face, with cow ears, small rounded horns and a cow tail",
                "full cow, actual cattle, animal face, udder") },
            { "impid", new RacePromptEntry(
                "1girl, demon girl, human face, glowing golden eyes, small fins on head, ethereal flame limbs, imp",
                "an anime imp girl: human face with glowing golden eyes, small fin-like horns on her head, wisps of ethereal flame around her body",
                "full demon monster, horns on face, non-humanoid") },
            { "yaelong", new RacePromptEntry(
                "1girl, chinese dragon girl, human face, chinese dragon horns, dragon tail, imperial hanfu, eastern dragon aesthetic",
                "an anime Chinese dragon girl: fully human face, with elegant Chinese dragon horns and a long dragon tail; imperial hanfu robes",
                "full dragon, dragon face, chinese lion dance costume") },
            { "chaos spirit", new RacePromptEntry(
                "1girl, spirit girl, human face, halo, white feathers floating, bone constructs floating around, black flame aura, dark fantasy",
                "a dark-fantasy spirit girl: human face, a halo over her head, white feathers drifting around her, small floating bone constructs and black flames surrounding her",
                "skeleton face, undead monster, no face") },
            { "punisher", new RacePromptEntry(
                "1girl, demon girl, human face, pale skin, blue-black demon wings, large demon horns, purple-black halo, poker face",
                "a stern anime demon girl: human face with pale skin, large curved demon horns, blue-black demon wings and a faint purple-black halo; expressionless poker face",
                "full demon, monster face, non-humanoid") },
            { "false god", new RacePromptEntry(
                "1girl, divine girl, child body, human face, rainbow halo, asymmetrical wings, three black wings on left, three white wings on right",
                "a false-god anime girl with a child-like body: human face, a rainbow halo, and strikingly asymmetric wings - three black wings on her left side and three white wings on her right",
                "six identical wings, monster, non-humanoid") },
            { "kurila", new RacePromptEntry(
                "1girl, human face, golden halo, single orange horn on forehead, silver mechanical ears, (ten floating flame funnels behind back forming wing shape:1.3)",
                "an anime girl with a golden halo, a single small orange horn on her forehead and silver mechanical ears; behind her back, ten floating flame funnels are arranged in a wing-like formation",
                "actual flames burning face, monster, non-humanoid") },
            { "solark", new RacePromptEntry(
                "1girl, dragon girl, human face, three blue dragon horns, dragon wings, thick dragon tail, blue fire aura, black halo",
                "an anime dragon girl: human face, three blue dragon horns, dragon wings and a thick tail, wreathed in blue fire with a black halo",
                "full dragon, dragon face, non-humanoid") },
            { "sin spirit", new RacePromptEntry(
                "1girl, spirit girl, human face, huge golden halo with hanging chains, floating black weapons, blue cold flames, rainbow halo with black hole",
                "an otherworldly spirit girl: human face, a huge golden halo with chains hanging from it, floating black weapons and blue cold flames orbiting her",
                "monster, weapon face, non-humanoid") },
            { "idian", new RacePromptEntry(
                "1girl, cyborg girl, human face, mechanical body parts, glowing energy wings, floating bits",
                "an anime cyborg girl: human face with visible mechanical body parts, glowing energy wings and small floating drone bits around her",
                "full robot, robot face, non-humanoid") },
            { "luciferium", new RacePromptEntry(
                "1girl, cyborg girl, human face, industrial robot parts, heavy integrated weaponry, nano-machine cloud, worn metal texture",
                "an industrial cyborg girl: human face but heavy robot parts integrated into her body, worn metal textures, a faint cloud of nano-machines drifting around her",
                "full robot, robot face, non-humanoid") },
            { "rabbie", new RacePromptEntry(
                "1girl, rabbit girl, kemonomimi, human face, long rabbit ears, moon rabbit, space soldier uniform, starry background",
                "an anime moon-rabbit girl: fully human face, with long rabbit ears; wearing a space soldier uniform against a starry sky",
                "full rabbit, animal face, fur covering body") }
        };

        /// <summary>
        /// 获取种族描述（自动适配提示词语法模式）。返回 null 表示未收录。
        /// </summary>
        public static string GetRaceVisuals(Pawn pawn, bool naturalLanguage = false)
        {
            var entry = FindEntry(pawn);
            if (entry == null) return null;
            return naturalLanguage ? entry.nlForm : entry.tagForm;
        }

        /// <summary>
        /// 获取种族专属负面提示词。返回 null 表示无。
        /// </summary>
        public static string GetRaceNegative(Pawn pawn)
        {
            var entry = FindEntry(pawn);
            return entry?.negative;
        }

        /// <summary>
        /// 判断该种族是否已收录（含玩家自定义）。
        /// </summary>
        public static bool HasDescription(Pawn pawn)
        {
            if (CustomRacePromptStore.HasCustom(pawn)) return true;
            return FindEntry(pawn) != null;
        }

        /// <summary>
        /// 判断内置词库是否收录（不含自定义）——编辑器用来显示[内置]标记。
        /// </summary>
        public static bool HasBuiltIn(Pawn pawn)
        {
            return FindEntry(pawn) != null;
        }

        /// <summary>
        /// 编辑器按 ThingDef 查询内置收录。
        /// </summary>
        public static bool HasBuiltInForDef(ThingDef def)
        {
            return FindEntryForDef(def) != null;
        }

        /// <summary>
        /// 编辑器按 ThingDef 获取内置词条目。
        /// </summary>
        public static RacePromptEntry GetBuiltInEntryForDef(ThingDef def)
        {
            return FindEntryForDef(def);
        }

        private static RacePromptEntry FindEntryForDef(ThingDef def)
        {
            if (def == null) return null;
            if (IsCorpseDef(def.defName)) return null;
            if (!string.IsNullOrEmpty(def.defName))
            {
                if (TryFindKeyword(def.defName.ToLower(), out RacePromptEntry e1)) return e1;
            }
            if (!string.IsNullOrEmpty(def.label))
            {
                if (TryFindKeyword(def.label.ToLower(), out RacePromptEntry e2)) return e2;
            }
            return null;
        }

        /// <summary>
        /// 获取内置词条目（编辑器"恢复默认"用），返回 null 表示无内置。
        /// </summary>
        public static RacePromptEntry GetBuiltInEntry(Pawn pawn)
        {
            return FindEntry(pawn);
        }

        private static RacePromptEntry FindEntry(Pawn pawn)
        {
            if (pawn == null || pawn.def == null) return null;
            if (IsCorpseDef(pawn.def.defName)) return null;

            string defName = pawn.def.defName;
            if (!string.IsNullOrEmpty(defName))
            {
                if (TryFindKeyword(defName.ToLower(), out RacePromptEntry e1)) return e1;
            }

            string label = pawn.def.label;
            if (!string.IsNullOrEmpty(label))
            {
                if (TryFindKeyword(label.ToLower(), out RacePromptEntry e2)) return e2;
            }
            return null;
        }

        /// <summary>排除原版生成的尸体def（Corpse_XXX / UnnaturalCorpse_XXX），防止 Contains 模糊匹配误命中</summary>
        private static bool IsCorpseDef(string defName)
        {
            return !string.IsNullOrEmpty(defName) &&
                   (defName.StartsWith("Corpse_") || defName.StartsWith("UnnaturalCorpse_"));
        }

        private static readonly Dictionary<string, string> aliasToMain = new Dictionary<string, string>()
        {
            { "萌螈", "moelotl" }, { "米莉拉", "milira" }, { "milira class", "milira" }, { "天翼", "milira" }, { "天空精灵", "milira" },
            { "鼠族", "ratkin" }, { "ratkin_su", "ratkin" },
            { "悠兰", "yuran" }, { "龙人", "dragonian" }, { "wofen", "wolfen" }, { "沃芬", "wolfen" },
            { "魅狐", "kurin" }, { "奎琳", "kurin" }, { "绮罗", "kiro" }, { "烟烬", "yanjin" },
            { "美狐", "miho" }, { "蒂尼玛尔", "tinymar" }, { "蒂尼玛", "tinymar" }, { "小人族", "tinymar" }, { "半身人", "tinymar" },
            { "安缇", "anty" }, { "蚂蚁娘", "anty" }, { "维维", "vivi" }, { "蜜蜂娘", "vivi" },
            { "珉巧", "mincho" }, { "薄荷巧克力史莱姆", "mincho" }, { "莫约", "moyo" }, { "深海蛞蝓", "moyo" },
            { "帕尼尔", "paniel" }, { "自动机兵", "paniel" }
        };

        private static bool TryFindKeyword(string key, out RacePromptEntry result)
        {
            result = null;
            if (string.IsNullOrEmpty(key)) return false;

            // 中文/别名 → 主键合并
            foreach (var alias in aliasToMain)
            {
                if (key.Contains(alias.Key)) key = alias.Value;
            }

            foreach (var kvp in raceDescriptions)
            {
                if (key.Contains(kvp.Key) && kvp.Value != null)
                {
                    result = kvp.Value;
                    return true;
                }
            }
            return false;
        }
    }
}
