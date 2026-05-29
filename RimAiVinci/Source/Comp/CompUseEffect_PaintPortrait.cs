using Verse;
using RimWorld;
using System.Collections.Generic;

namespace RimAiVinci
{
    public class CompUseEffect_PaintPortrait : CompUseEffect
    {
        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);

            var store = Find.World.GetComponent<ArtDataStore>();
            string activePath = store.GetActivePortraitPath(usedBy);

            if (string.IsNullOrEmpty(activePath))
            {
                Messages.Message("RAV_Frame_NoPortrait".Translate(usedBy.Name != null ? usedBy.Name.ToStringShort : usedBy.def.label), MessageTypeDefOf.RejectInput);
                return;
            }

            ThingDef frameDef = DefDatabase<ThingDef>.GetNamedSilentFail("RimDigital_PhotoFrame");
            if (frameDef != null)
            {
                Thing frameThing = ThingMaker.MakeThing(frameDef);
                Building_PhotoFrame frameBuilding = frameThing as Building_PhotoFrame;
                if (frameBuilding != null)
                {
                    frameBuilding.UpdateArt(usedBy.ThingID, activePath);
                    MinifiedThing minifiedFrame = frameThing.MakeMinified();
                    GenPlace.TryPlaceThing(minifiedFrame, usedBy.Position, usedBy.Map, ThingPlaceMode.Near);

                    Messages.Message("RAV_Frame_Created".Translate(), MessageTypeDefOf.PositiveEvent);

                    // ✨✨ 新增：添加心情 "量子相框展示" ✨✨
                    if (usedBy.needs?.mood != null)
                    {
                        ThoughtDef thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("RAV_Thought_FrameCreated");
                        if (thought != null)
                        {
                            usedBy.needs.mood.thoughts.memories.TryGainMemory(thought);
                        }
                    }

                    // 消耗逻辑：只消耗 1 个
                    if (parent.stackCount > 1)
                    {
                        parent.SplitOff(1).Destroy();
                    }
                    else
                    {
                        parent.Destroy();
                    }
                }
            }
        }
    }
}