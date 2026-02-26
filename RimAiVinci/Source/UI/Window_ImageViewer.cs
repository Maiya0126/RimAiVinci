using UnityEngine;
using Verse;

namespace RimAiVinci
{
    public class Window_ImageViewer : Window
    {
        private GalleryItem item;

        public Window_ImageViewer(GalleryItem item)
        {
            this.item = item;

            // 关键修改1: 禁用系统自带的"点外部关闭"，我们自己接管点击逻辑
            this.closeOnClickedOutside = false;

            this.doCloseButton = false;
            this.doCloseX = false;

            // 不绘制标准窗口背景，我们要自己画半透明黑底
            this.drawShadow = false;

            // 设为最高层级
            this.layer = WindowLayer.Super;

            // 防止查看图片时误触地图上的东西
            this.preventCameraMotion = true;
        }

        // 关键修改2: 强制窗口大小等于屏幕大小，从而拦截所有点击
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

        public override void DoWindowContents(Rect inRect)
        {
            if (item == null || item.Texture == null)
            {
                Close();
                return;
            }

            // 1. 绘制全屏半透明黑色背景 (像看电影一样压暗背景)
            Widgets.DrawBoxSolid(inRect, new Color(0f, 0f, 0f, 0.75f));

            // 2. 计算图片显示区域 (保持比例，四周留空)
            float padding = 60f; // 边距留大一点
            float maxWidth = inRect.width - (padding * 2);
            float maxHeight = inRect.height - (padding * 2);

            Texture2D tex = item.Texture;
            float imageAspect = (float)tex.width / tex.height;

            // 基础宽高
            float drawWidth = maxWidth;
            float drawHeight = maxWidth / imageAspect;

            // 如果高度超标，则按高度缩放
            if (drawHeight > maxHeight)
            {
                drawHeight = maxHeight;
                drawWidth = drawHeight * imageAspect;
            }

            // 居中计算
            Rect imageRect = new Rect(0, 0, drawWidth, drawHeight);
            imageRect.center = inRect.center;

            // 3. 处理点击逻辑 (这部分必须在绘制按钮之前判断)
            // 如果鼠标点击了，并且没有点在图片范围内
            // 注意：因为我们还要在图片上画关闭按钮，这里先暂存点击事件
            bool clickedOutside = false;
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.ContextClick)
            {
                if (!imageRect.Contains(Event.current.mousePosition))
                {
                    clickedOutside = true;
                    Event.current.Use(); // 消耗掉这次点击，防止穿透
                }
            }

            // 4. 绘制图片
            GUI.DrawTexture(imageRect, tex, ScaleMode.ScaleToFit);

            // 绘制图片边框 (可选，增加精致感)
            // 正确写法：先改颜色，画框，再改回白色
            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            Widgets.DrawBox(imageRect, 1);
            GUI.color = prevColor;

            // 5. 绘制底部文件名信息
            float infoHeight = 30f;
            Rect infoRect = new Rect(imageRect.x, imageRect.yMax + 5f, imageRect.width, infoHeight);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            Widgets.Label(infoRect, $"{item.FileName}  |  {item.CreationTime}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // 6. 绘制关闭按钮 (放在图片右上角)
            // 按钮大小
            float btnSize = 24f;
            // 稍微突出一点点或者在角内
            Rect closeBtnRect = new Rect(imageRect.xMax - btnSize - 5f, imageRect.y + 5f, btnSize, btnSize);

            // 使用游戏自带的白色关闭叉叉图标
            if (Widgets.ButtonImage(closeBtnRect, TexButton.CloseXBig))
            {
                Close();
                return; // 既然关了就不用执行后面的逻辑了
            }

            // 7. 执行点外部关闭逻辑
            if (clickedOutside)
            {
                Close();
            }
        }
    }
}