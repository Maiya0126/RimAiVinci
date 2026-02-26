using UnityEngine;
using Verse;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RimAiVinci
{
    // 代表相册中的单张图片对象
    public class GalleryItem
    {
        public string FilePath;       // 文件的完整路径
        public string FileName;       // 文件名（比如 "PawnName_20260110.png"）
        public Texture2D Texture;     // 游戏内显示的贴图
        public string CreationTime;   // 创建时间字符串

        // 卸载贴图以释放内存（如果相册关闭时需要清理）
        public void Unload()
        {
            if (Texture != null)
            {
                UnityEngine.Object.Destroy(Texture);
                Texture = null;
            }
        }
    }

    public static class DataController
    {
        // 缓存加载进来的所有图片
        private static List<GalleryItem> cachedImages = new List<GalleryItem>();

        // 定义存档位置：在 RimWorld 的存档文件夹下创建一个专门的图片文件夹
        // 路径通常是: C:\Users\YourName\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\RimAiVinci_Gallery
        public static string SaveDirectory
        {
            get
            {
                string path = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimAiVinci_Gallery");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        /// <summary>
        /// 获取当前所有已加载的图片列表
        /// </summary>
        public static List<GalleryItem> GetImages()
        {
            if (cachedImages == null || cachedImages.Count == 0)
            {
                ReloadImages();
            }
            return cachedImages;
        }

        /// <summary>
        /// 扫描文件夹并重新加载所有图片
        /// </summary>
        public static void ReloadImages()
        {
            // 先清理旧数据，防止内存泄漏
            ClearCache();

            string path = SaveDirectory;
            if (!Directory.Exists(path)) return;

            // 获取所有 png 文件，按创建时间倒序排列（最新的在最前面）
            var files = Directory.GetFiles(path, "*.png")
                                 .OrderByDescending(f => File.GetCreationTime(f));

            foreach (string file in files)
            {
                Texture2D tex = LoadTexture(file);
                if (tex != null)
                {
                    cachedImages.Add(new GalleryItem
                    {
                        FilePath = file,
                        FileName = Path.GetFileNameWithoutExtension(file),
                        CreationTime = File.GetCreationTime(file).ToString("yyyy-MM-dd HH:mm"),
                        Texture = tex
                    });
                }
            }

            Log.Message($"[Rim AiVinci] Loaded {cachedImages.Count} images from gallery.");
        }

        /// <summary>
        /// 将硬盘上的 PNG 字节流转换为 Texture2D
        /// </summary>
        private static Texture2D LoadTexture(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            try
            {
                byte[] fileData = File.ReadAllBytes(filePath);

                // 修改1: 使用 RGBA32 格式，兼容性更好
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (tex.LoadImage(fileData))
                {
                    tex.filterMode = FilterMode.Bilinear;
                    tex.name = Path.GetFileName(filePath);

                    // 修改2: 必须调用 Apply() 才能在游戏界面正确显示！
                    tex.Apply(true, true); // updateMipmaps=true, makeNoLongerReadable=true (省内存)

                    return tex;
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Rim AiVinci] Failed to load image {filePath}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 清理缓存，释放显存
        /// </summary>
        public static void ClearCache()
        {
            if (cachedImages != null)
            {
                foreach (var item in cachedImages)
                {
                    item.Unload();
                }
                cachedImages.Clear();
            }
        }

        /// <summary>
        /// 用于测试：生成一张纯色图片保存到文件夹，验证路径是否正确
        /// </summary>
        public static void CreateDebugImage()
        {
            Texture2D tex = new Texture2D(512, 512);
            // 填充随机颜色
            Color[] cols = new Color[512 * 512];
            Color randCol = new Color(Rand.Value, Rand.Value, Rand.Value);
            for (int i = 0; i < cols.Length; i++) cols[i] = randCol;
            tex.SetPixels(cols);
            tex.Apply();

            byte[] bytes = tex.EncodeToPNG();
            string filename = $"Debug_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            File.WriteAllBytes(Path.Combine(SaveDirectory, filename), bytes);

            Log.Message($"[Rim AiVinci] Debug image saved to: {Path.Combine(SaveDirectory, filename)}");

            // 销毁临时贴图
            UnityEngine.Object.Destroy(tex);

            // 刷新显示
            ReloadImages();
        }
    }
}