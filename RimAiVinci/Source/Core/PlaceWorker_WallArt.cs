using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    public class PlaceWorker_WallArt : PlaceWorker
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            // ✨ 修复：删除了报错的 GenDraw.DrawArrowPointer
            // RimWorld 引擎会自动为可旋转的物体绘制蓝色朝向箭头，这里不需要手动写
        }

        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            // 1. 检查脚下是否有墙
            Building edifice = loc.GetEdifice(map);
            if (edifice == null)
            {
                return new AcceptanceReport("必须建造在墙壁上");
            }

            // 检查是不是实心墙 (排除栅栏或柱子)
            if (edifice.def.Fillage != FillCategory.Full)
            {
                return new AcceptanceReport("必须挂在完整的墙壁上 (不能是柱子或栅栏)");
            }

            // 2. 检查朝向
            // 挂画必须“背靠”墙壁，所以它的朝向格子 (FacingCell) 必须是空的
            IntVec3 facingCell = loc + rot.FacingCell;
            if (!facingCell.InBounds(map))
            {
                return new AcceptanceReport("朝向区域在地图外");
            }

            Building facingEdifice = facingCell.GetEdifice(map);
            if (facingEdifice != null && facingEdifice.def.Fillage == FillCategory.Full)
            {
                return new AcceptanceReport("前方视线被遮挡");
            }

            return true;
        }
    }
}