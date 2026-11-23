using Verse;

namespace RimAiVinci
{
    // 立绘排版数据：按小人保存，覆盖全局默认值
    public class PortraitLayout : IExposable
    {
        // 偏移量以“立绘显示高度”为 1 的相对单位，避免不同分辨率/UI 缩放下位置跑偏
        public float offsetX = 0f;
        public float offsetY = 0f;
        public float scale = 1f;

        public void CopyFrom(PortraitLayout other)
        {
            if (other == null) return;
            offsetX = other.offsetX;
            offsetY = other.offsetY;
            scale = other.scale;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref offsetX, "offsetX", 0f);
            Scribe_Values.Look(ref offsetY, "offsetY", 0f);
            Scribe_Values.Look(ref scale, "scale", 1f);
        }
    }
}
