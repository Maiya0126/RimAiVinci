using Verse;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;

namespace RimAiVinci
{
    public enum PawnPortraitType { Human, Animal, Mechanoid }

    public enum PortraitState { Normal = 0, Happy = 1, Injured = 2, MentalBreak = 3, Sleeping = 4, Drunk = 5, Sick = 6, Hungry = 7, Tired = 8, Combat = 9 }

    public static class PortraitStateHelper
    {
        private const float StateCacheDuration = 0.25f;

        private static HediffDef alcoholHighDef;
        private static bool alcoholLookupDone;
        private static readonly Dictionary<string, PortraitState> stateCache = new Dictionary<string, PortraitState>();
        private static readonly Dictionary<string, float> stateCacheTime = new Dictionary<string, float>();

        public static PortraitState GetCurrentState(Pawn p)
        {
            if (p == null) return PortraitState.Normal;

            float now = Time.realtimeSinceStartup;
            string id = p.ThingID;
            float cachedAt;
            PortraitState cachedState;
            if (stateCacheTime.TryGetValue(id, out cachedAt) && now - cachedAt < StateCacheDuration
                && stateCache.TryGetValue(id, out cachedState))
            {
                return cachedState;
            }

            PortraitState st = ComputeCurrentState(p);
            stateCache[id] = st;
            stateCacheTime[id] = now;
            return st;
        }

        private static PortraitState ComputeCurrentState(Pawn p)
        {
            try
            {
                if (p.InMentalState) return PortraitState.MentalBreak;
                if (p.health != null && p.health.Downed) return PortraitState.Injured;
                if (IsInCombat(p)) return PortraitState.Combat;

                if (!alcoholLookupDone)
                {
                    alcoholHighDef = DefDatabase<HediffDef>.GetNamedSilentFail("AlcoholHigh");
                    alcoholLookupDone = true;
                }
                if (alcoholHighDef != null && p.health != null && p.health.hediffSet != null && p.health.hediffSet.HasHediff(alcoholHighDef))
                    return PortraitState.Drunk;

                if (p.health != null && p.health.hediffSet != null && p.health.hediffSet.HasImmunizableNotImmuneHediff())
                    return PortraitState.Sick;

                if (p.CurJobDef == JobDefOf.LayDown) return PortraitState.Sleeping;

                if (p.needs != null)
                {
                    if (p.needs.food != null && p.needs.food.CurLevel < 0.15f) return PortraitState.Hungry;
                    if (p.needs.rest != null && p.needs.rest.CurLevel < 0.15f) return PortraitState.Tired;
                    if (p.needs.mood != null && p.needs.mood.CurLevel >= 0.9f) return PortraitState.Happy;
                }
            }
            catch { }
            return PortraitState.Normal;
        }

        private static bool IsInCombat(Pawn p)
        {
            int mode = RimAiVinciMod.settings != null ? RimAiVinciMod.settings.combatPortraitMode : 1;
            if (mode <= 0) return false;

            try
            {
                if (p.Drafted && p.mindState != null && p.mindState.enemyTarget != null) return true;
                if (PawnUtility.IsFighting(p)) return true;
                if (mode >= 2 && p.Drafted) return true;
            }
            catch { }
            return false;
        }

        // 设置项变更后立即生效，避免最长 0.25 秒的旧缓存
        public static void ClearStateCache()
        {
            stateCache.Clear();
            stateCacheTime.Clear();
        }

        public static string GetStateLabelKey(PortraitState st)
        {
            switch (st)
            {
                case PortraitState.Happy: return "RAV_State_Happy";
                case PortraitState.Injured: return "RAV_State_Injured";
                case PortraitState.MentalBreak: return "RAV_State_Break";
                case PortraitState.Sleeping: return "RAV_State_Sleeping";
                case PortraitState.Drunk: return "RAV_State_Drunk";
                case PortraitState.Sick: return "RAV_State_Sick";
                case PortraitState.Hungry: return "RAV_State_Hungry";
                case PortraitState.Tired: return "RAV_State_Tired";
                case PortraitState.Combat: return "RAV_State_Combat";
                default: return "RAV_State_Normal";
            }
        }

        public static string GetStatePromptFragment(PortraitState st)
        {
            switch (st)
            {
                case PortraitState.Happy: return "joyful expression, big smile, laughing happily, beaming with joy";
                case PortraitState.Injured: return "badly injured, bandages wrapped around body, bruises, exhausted pained expression, wounded";
                case PortraitState.MentalBreak: return "crying hysterically, wild panicked expression, tears streaming down face, emotionally broken";
                case PortraitState.Sleeping: return "sleeping peacefully, eyes closed, relaxed calm expression";
                case PortraitState.Drunk: return "drunk, flushed red face, dizzy silly expression, holding a beer bottle, tipsy";
                case PortraitState.Sick: return "sick, pale skin, feverish weak expression, cold sweat, ill and shivering";
                case PortraitState.Hungry: return "starving, weak from hunger, desperate hungry expression, hollow cheeks";
                case PortraitState.Tired: return "exhausted, dark circles under eyes, yawning, sleepy tired expression";
                case PortraitState.Combat: return "combat stance, fierce determined expression, battle-ready, tense atmosphere, gritted teeth, dynamic pose";
                default: return "";
            }
        }
    }

    public static class PawnPortraitTypeHelper
    {
        public static PawnPortraitType GetPawnType(Pawn pawn)
        {
            if (pawn == null) return PawnPortraitType.Human;
            if (pawn.RaceProps == null) return PawnPortraitType.Human;
            if (pawn.RaceProps.IsMechanoid) return PawnPortraitType.Mechanoid;
            if (pawn.RaceProps.Animal) return PawnPortraitType.Animal;
            if (pawn.story == null) return PawnPortraitType.Animal;
            return PawnPortraitType.Human;
        }

        public static bool CanAIGenerate(Pawn pawn)
        {
            return GetPawnType(pawn) == PawnPortraitType.Human;
        }

        public static string GetSubFolder(PawnPortraitType type)
        {
            switch (type)
            {
                case PawnPortraitType.Animal: return "Animal";
                case PawnPortraitType.Mechanoid: return "Mechanoid";
                default: return "Human";
            }
        }
    }

    public class ArtData : IExposable
    {
        public string artID;
        public string pawnID;
        public string relativePath;
        public string prompt;
        public long timestamp;
        public string authorName;
        public string title;
        public PawnPortraitType pawnType = PawnPortraitType.Human;
        public bool isMemorial;

        public ArtData() { }

        public ArtData(string artId, string pId, string path, string pText, string pName)
        {
            this.artID = artId;
            this.pawnID = pId;
            this.relativePath = path;
            this.prompt = pText;
            this.authorName = pName;
            this.timestamp = System.DateTime.Now.Ticks;
            this.title = "Untitled";
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref artID, "artID");
            Scribe_Values.Look(ref pawnID, "pawnID");
            Scribe_Values.Look(ref relativePath, "relativePath");
            Scribe_Values.Look(ref prompt, "prompt");
            Scribe_Values.Look(ref timestamp, "timestamp");
            Scribe_Values.Look(ref authorName, "authorName");
            Scribe_Values.Look(ref title, "title", "Untitled");
            Scribe_Values.Look(ref pawnType, "pawnType", PawnPortraitType.Human);
            Scribe_Values.Look(ref isMemorial, "isMemorial", false);
        }
    }
}
