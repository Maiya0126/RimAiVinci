using UnityEngine;
using Verse;

namespace RimAiVinci
{
    public class Dialog_ShowImage : Window
    {
        private Texture2D image;

        public override Vector2 InitialSize => new Vector2(600f, 650f);

        public Dialog_ShowImage(Texture2D tex)
        {
            this.image = tex;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.draggable = true;
            this.resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (image != null)
            {
                Rect imgRect = new Rect(0, 0, inRect.width, inRect.width);
                Widgets.DrawTextureFitted(imgRect, image, 1.0f);

                Rect labelRect = new Rect(0, inRect.width + 10, inRect.width, 30);
                Widgets.Label(labelRect, "RAV_ShowImage_Size".Translate(image.width, image.height));
            }
            else
            {
                Widgets.Label(inRect, "RAV_ShowImage_LoadFail".Translate());
            }
        }
    }
}
