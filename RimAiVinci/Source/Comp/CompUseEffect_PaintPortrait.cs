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
                Messages.Message($"【失败】{usedBy.Name.ToStringShort} 未设置展示立绘！", MessageTypeDefOf.RejectInput);
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

                    Messages.Message("具现化成功！", MessageTypeDefOf.PositiveEvent);

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