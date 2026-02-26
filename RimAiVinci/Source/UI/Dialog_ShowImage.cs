using UnityEngine;
using Verse;

namespace RimAiVinci
{
    public class Dialog_ShowImage : Window
    {
        private Texture2D image;

        // 🔴 核心修复：RimWorld 要求必须通过重写这个属性来定义初始大小
        public override Vector2 InitialSize => new Vector2(600f, 650f);

        public Dialog_ShowImage(Texture2D tex)
        {
            this.image = tex;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.draggable = true;
            this.resizeable = true;

            // ❌ 之前报错就是因为写了这行：this.initialSize = ... (已删除)
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (image != null)
            {
                // 计算图片显示区域，保持正方形比例
                Rect imgRect = new Rect(0, 0, inRect.width, inRect.width);

                // 绘制图片
                Widgets.DrawTextureFitted(imgRect, image, 1.0f);

                // 在图片下方显示尺寸信息
                Rect labelRect = new Rect(0, inRect.width + 10, inRect.width, 30);
                Widgets.Label(labelRect, $"尺寸: {image.width}x{image.height}");
            }
            else
            {
                Widgets.Label(inRect, "图像加载失败或为空。");
            }
        }
    }
}