using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    public class RAV_Graphic_Dynamic : Graphic_Single
    {
        public override void Init(GraphicRequest req)
        {
            this.data = req.graphicData;
            this.path = req.path ?? "Generated_Dynamic_Art";
            this.color = req.color;
            this.colorTwo = req.colorTwo;
            this.drawSize = req.drawSize;
        }

        public void InitWith(Material material, Vector2 size)
        {
            this.mat = material;
            this.drawSize = size;
            this.data = new GraphicData();
            this.data.drawSize = size;
            this.color = Color.white;
            this.colorTwo = Color.white;
            this.path = "Generated_Dynamic_Art";
        }
    }

    public class Building_PhotoFrame : Building
    {
        private string targetPawnID;
        private string artPath;
        private float imageScale = 1.0f;

        // ✨ 性能优化：缓存 Texture2D，避免每帧读硬盘
        private Texture2D cachedTexture;
        private Graphic cachedGraphic;

        // 公开属性给 Gizmo 用
        public float ImageScale => imageScale;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref targetPawnID, "targetPawnID");
            Scribe_Values.Look(ref artPath, "artPath");
            Scribe_Values.Look(ref imageScale, "imageScale", 1.0f);
        }

        public void UpdateArt(string pID, string path)
        {
            this.targetPawnID = pID;
            this.artPath = path;
            this.cachedTexture = null; // 路径变了，清空贴图缓存
            this.cachedGraphic = null;
            this.RefreshDisplay();
        }

        // 供 Gizmo 调用的设置方法
        public void SetScale(float newScale)
        {
            this.imageScale = newScale;
            this.cachedGraphic = null; // 尺寸变了，只清空 Graphic，不清除 Texture
            this.RefreshDisplay();
        }

        public override Graphic Graphic
        {
            get
            {
                if (cachedGraphic != null) return cachedGraphic;
                if (string.IsNullOrEmpty(artPath)) return base.Graphic;

                // 1. 优先从内存缓存加载贴图
                if (cachedTexture == null)
                {
                    cachedTexture = ArtFileSystem.LoadTextureFromDisk(artPath);
                }

                // 2. 如果还是没有（文件丢失），回退到默认
                if (cachedTexture == null) return base.Graphic;

                // 3. 计算宽高比
                float ratio = (float)cachedTexture.width / cachedTexture.height;
                float baseHeight = 1.2f * imageScale;
                float finalWidth = baseHeight * ratio;

                Vector2 drawSize = new Vector2(finalWidth, baseHeight);

                // 4. 生成 Graphic
                Material mat = MaterialPool.MatFrom(cachedTexture, ShaderDatabase.Cutout, Color.white);
                RAV_Graphic_Dynamic newGraphic = new RAV_Graphic_Dynamic();
                newGraphic.InitWith(mat, drawSize);

                cachedGraphic = newGraphic;
                return cachedGraphic;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;

            if (!string.IsNullOrEmpty(artPath))
            {
                // ✨ 使用我们新写的滑动条 Gizmo
                yield return new Gizmo_ArtScale(this);
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            string baseStr = base.GetInspectString();
            if (!string.IsNullOrEmpty(baseStr)) sb.Append(baseStr);

            if (!string.IsNullOrEmpty(targetPawnID))
            {
                if (sb.Length > 0) sb.AppendLine();
                string name = targetPawnID;
                if (Find.CurrentMap != null)
                {
                    Pawn p = Find.CurrentMap.mapPawns.AllPawns.FirstOrDefault(x => x.ThingID == targetPawnID);
                    if (p != null) name = p.Name.ToStringShort;
                }
                sb.Append($"立绘人物: {name}");
            }
            return sb.ToString();
        }

        private void RefreshDisplay()
        {
            if (this.Map != null) this.DirtyMapMesh(this.Map);
        }
    }
}