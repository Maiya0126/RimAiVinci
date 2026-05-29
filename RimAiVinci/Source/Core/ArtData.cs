using Verse;
using RimWorld;

namespace RimAiVinci
{
    public enum PawnPortraitType { Human, Animal, Mechanoid }

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
        }
    }
}
