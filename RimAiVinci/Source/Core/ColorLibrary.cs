using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimAiVinci
{
    public static class ColorLibrary
    {
        public struct NamedColor
        {
            public string name;
            public Color color;
            public NamedColor(string n, float r, float g, float b) { name = n; color = new Color(r, g, b); }
        }

        // 🎨 120+ 种颜色库 (内容保持不变)
        private static readonly List<NamedColor> Library = new List<NamedColor>()
        {
            // === ⚪ 黑白灰 ===
            new NamedColor("pure white", 1f, 1f, 1f),
            new NamedColor("platinum", 0.89f, 0.89f, 0.9f),
            new NamedColor("silver", 0.75f, 0.75f, 0.75f),
            new NamedColor("light grey", 0.83f, 0.83f, 0.83f),
            new NamedColor("grey", 0.5f, 0.5f, 0.5f),
            new NamedColor("dark grey", 0.33f, 0.33f, 0.33f),
            new NamedColor("slate grey", 0.44f, 0.5f, 0.56f),
            new NamedColor("charcoal", 0.21f, 0.27f, 0.31f),
            new NamedColor("jet black", 0.1f, 0.1f, 0.1f),
            new NamedColor("obsidian", 0.05f, 0.05f, 0.05f),
            new NamedColor("ash", 0.7f, 0.75f, 0.71f),

            // === 🟤 肤色与棕色 ===
            new NamedColor("pale skin", 1f, 0.94f, 0.87f),
            new NamedColor("porcelain skin", 1f, 0.9f, 0.85f),
            new NamedColor("fair skin", 1f, 0.89f, 0.77f),
            new NamedColor("peach skin", 1f, 0.9f, 0.71f),
            new NamedColor("tan skin", 0.82f, 0.71f, 0.55f),
            new NamedColor("olive skin", 0.76f, 0.69f, 0.57f),
            new NamedColor("honey skin", 0.78f, 0.61f, 0.42f),
            new NamedColor("bronze skin", 0.8f, 0.5f, 0.2f),
            new NamedColor("brown skin", 0.65f, 0.48f, 0.36f),
            new NamedColor("dark brown skin", 0.4f, 0.26f, 0.13f),
            new NamedColor("black skin", 0.24f, 0.17f, 0.12f),
            new NamedColor("ebony skin", 0.15f, 0.1f, 0.08f),
            
            // === 🔴 红色系 ===
            new NamedColor("crimson", 0.86f, 0.08f, 0.24f),
            new NamedColor("ruby", 0.88f, 0.07f, 0.37f),
            new NamedColor("red", 1f, 0f, 0f),
            new NamedColor("scarlet", 1f, 0.14f, 0f),
            new NamedColor("burgundy", 0.5f, 0f, 0.13f),
            new NamedColor("maroon", 0.5f, 0f, 0f),
            new NamedColor("rose", 1f, 0.3f, 0.4f),
            new NamedColor("coral", 1f, 0.5f, 0.31f),
            new NamedColor("rust", 0.72f, 0.25f, 0.05f),

            // === 🟠 橙黄系 ===
            new NamedColor("gold", 1f, 0.84f, 0f),
            new NamedColor("goldenrod", 0.85f, 0.65f, 0.13f),
            new NamedColor("yellow", 1f, 1f, 0f),
            new NamedColor("amber", 1f, 0.75f, 0f),
            new NamedColor("orange", 1f, 0.65f, 0f),
            new NamedColor("copper", 0.72f, 0.45f, 0.2f),
            new NamedColor("blonde", 0.98f, 0.94f, 0.75f),
            new NamedColor("wheat", 0.96f, 0.87f, 0.7f),
            new NamedColor("cream", 1f, 0.99f, 0.82f),
            
            // === 🟢 绿色系 ===
            new NamedColor("emerald", 0.31f, 0.78f, 0.47f),
            new NamedColor("mint", 0.74f, 0.99f, 0.79f),
            new NamedColor("lime", 0f, 1f, 0f),
            new NamedColor("green", 0f, 0.5f, 0f),
            new NamedColor("forest green", 0.13f, 0.55f, 0.13f),
            new NamedColor("olive", 0.5f, 0.5f, 0f),
            new NamedColor("jade", 0f, 0.66f, 0.42f),
            new NamedColor("teal", 0f, 0.5f, 0.5f),
            new NamedColor("chartreuse", 0.5f, 1f, 0f),
            new NamedColor("sage", 0.56f, 0.74f, 0.56f),
            new NamedColor("seafoam", 0.6f, 1f, 0.8f),

            // === 🔵 蓝色系 ===
            new NamedColor("midnight blue", 0.1f, 0.1f, 0.44f),
            new NamedColor("navy blue", 0f, 0f, 0.5f),
            new NamedColor("sapphire", 0.06f, 0.32f, 0.73f),
            new NamedColor("blue", 0f, 0f, 1f),
            new NamedColor("azure", 0f, 0.5f, 1f),
            new NamedColor("sky blue", 0.53f, 0.81f, 0.92f),
            new NamedColor("cyan", 0f, 1f, 1f),
            new NamedColor("turquoise", 0.25f, 0.88f, 0.82f),
            new NamedColor("aqua", 0f, 1f, 1f),
            new NamedColor("ice blue", 0.8f, 0.9f, 1f),
            new NamedColor("periwinkle", 0.8f, 0.8f, 1f),
            new NamedColor("steel blue", 0.27f, 0.51f, 0.71f),

            // === 🟣 紫粉系 ===
            new NamedColor("amethyst", 0.6f, 0.4f, 0.8f),
            new NamedColor("purple", 0.5f, 0f, 0.5f),
            new NamedColor("violet", 0.93f, 0.51f, 0.93f),
            new NamedColor("indigo", 0.29f, 0f, 0.51f),
            new NamedColor("lavender", 0.9f, 0.9f, 0.98f),
            new NamedColor("lilac", 0.78f, 0.64f, 0.78f),
            new NamedColor("pink", 1f, 0.75f, 0.8f),
            new NamedColor("hot pink", 1f, 0.41f, 0.71f),
            new NamedColor("magenta", 1f, 0f, 1f),
            new NamedColor("fuchsia", 1f, 0f, 1f)
        };

        public static string GetBestMatch(Color input, string context = "")
        {
            string bestName = "grey";
            float minDistance = 999f;

            // ✨✨✨ 核心修复：计算亮度 (Value) ✨✨✨
            Color.RGBToHSV(input, out float h, out float s, out float v);

            foreach (var preset in Library)
            {

                float r = input.r - preset.color.r;
                float g = input.g - preset.color.g;
                float b = input.b - preset.color.b; 

                float dist = (r * r) + (g * g) + (b * b);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestName = preset.name;
                }
            }

            if (context == "skin")
            {
                if (bestName == "pure white") return "pale skin";
                if (bestName == "jet black") return "dark skin";
                if (bestName == "green") return "green skin";
                if (bestName == "blue") return "blue skin";
            }
            if (context == "hair")
            {
                if (bestName == "pure white") return "silver white hair";
            }
            if (context == "eye")
            {
                return bestName;
            }

            return bestName;
        }
    }
}