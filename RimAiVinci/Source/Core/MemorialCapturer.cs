using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;

namespace RimAiVinci
{
    public static class MemorialCapturer
    {
        public static void CaptureAndSave(Pawn pawn)
        {
            try
            {
                if (pawn == null || pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return;
                if (Find.World == null) return;

                Texture2D tex = CapturePortrait(pawn);
                if (tex == null) return;

                ArtDataStore store = Find.World.GetComponent<ArtDataStore>();
                if (store == null) return;

                string name = pawn.Name != null ? pawn.Name.ToStringShort : pawn.def.label;
                string relPath = ArtFileSystem.SaveTextureToDisk(tex, pawn, "Memorial Portrait");
                UnityEngine.Object.Destroy(tex);

                if (string.IsNullOrEmpty(relPath)) return;

                ArtData data = new ArtData
                {
                    artID = System.Guid.NewGuid().ToString(),
                    pawnID = pawn.ThingID,
                    relativePath = relPath,
                    prompt = "Memorial Portrait",
                    timestamp = System.DateTime.Now.Ticks,
                    authorName = name,
                    pawnType = PawnPortraitTypeHelper.GetPawnType(pawn),
                    isMemorial = true
                };
                store.AddArt(data);
                Messages.Message("RAV_Memorial_Captured".Translate(name), MessageTypeDefOf.PositiveEvent);
            }
            catch (System.Exception ex)
            {
                Log.Warning("[RimAiVinci] Memorial capture failed: " + ex.Message);
            }
        }

        private static Texture2D CapturePortrait(Pawn pawn)
        {
            try
            {
                RenderTexture rt = PortraitsCache.Get(pawn, new Vector2(512, 512), Rot4.South);
                RenderTexture currentActive = RenderTexture.active;
                RenderTexture.active = rt;
                Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = currentActive;
                return tex;
            }
            catch (System.Exception ex)
            {
                Log.Warning("[RimAiVinci] Memorial portrait render failed: " + ex.Message);
                return null;
            }
        }
    }

    public static class DeadPawnFinder
    {
        public static List<Pawn> GetDeadColonists()
        {
            List<Pawn> result = new List<Pawn>();
            HashSet<int> added = new HashSet<int>();

            if (Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                    {
                        Corpse c = t as Corpse;
                        if (c == null || c.InnerPawn == null) continue;
                        Pawn p = c.InnerPawn;
                        if (!IsValid(p) || !added.Add(p.thingIDNumber)) continue;
                        result.Add(p);
                    }

                    foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Grave))
                    {
                        Building_Grave grave = t as Building_Grave;
                        if (grave == null || grave.Corpse == null || grave.Corpse.InnerPawn == null) continue;
                        Pawn p = grave.Corpse.InnerPawn;
                        if (!IsValid(p) || !added.Add(p.thingIDNumber)) continue;
                        result.Add(p);
                    }
                }
            }

            try
            {
                var fld = HarmonyLib.AccessTools.Field(typeof(WorldPawns), "pawns");
                var lst = fld?.GetValue(Find.World.worldPawns) as List<Pawn>;
                if (lst != null)
                {
                    foreach (Pawn p in lst)
                    {
                        if (p == null || !p.Dead) continue;
                        if (!IsValid(p) || !added.Add(p.thingIDNumber)) continue;
                        result.Add(p);
                    }
                }
            }
            catch { }

            return result;
        }

        private static bool IsValid(Pawn p)
        {
            if (p == null || p.RaceProps == null || !p.RaceProps.Humanlike) return false;
            return p.Faction == Faction.OfPlayer || p.IsColonist;
        }
    }
}
