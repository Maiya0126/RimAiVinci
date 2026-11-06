using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Verse;

namespace RimAiVinci
{
    /// <summary>
    /// 玩家自定义种族描述存储：保存在存档数据目录 RimAiVinci/CustomRacePrompts.xml，
    /// 跨存档共享，工坊更新不会覆盖。
    /// 键为种族 defName（如 "ratkin"），值为玩家自定义描述。
    /// </summary>
    public static class CustomRacePromptStore
    {
        private static Dictionary<string, string> customPrompts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool loaded = false;
        private static readonly object loadLock = new object();

        private static string StorePath
        {
            get
            {
                string baseDir = ArtFileSystem.GetActualSaveDir();
                return Path.Combine(baseDir, "CustomRacePrompts.xml");
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            lock (loadLock)
            {
                if (loaded) return;
                customPrompts.Clear();
                try
                {
                    if (File.Exists(StorePath))
                    {
                        var doc = XDocument.Load(StorePath);
                        foreach (var el in doc.Root?.Elements("race") ?? Enumerable.Empty<XElement>())
                        {
                            string defName = el.Attribute("defName")?.Value;
                            string prompt = el.Value?.Trim();
                            if (!string.IsNullOrEmpty(defName) && !string.IsNullOrEmpty(prompt))
                                customPrompts[defName] = prompt;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[RimAiVinci] 自定义种族描述加载失败: " + ex.Message);
                }
                loaded = true;
            }
        }

        public static bool HasCustom(Pawn pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(pawn.def?.defName)) return false;
            EnsureLoaded();
            return customPrompts.ContainsKey(pawn.def.defName);
        }

        public static string GetCustom(Pawn pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(pawn.def?.defName)) return null;
            EnsureLoaded();
            string v;
            return customPrompts.TryGetValue(pawn.def.defName, out v) ? v : null;
        }

        public static void SetCustom(Pawn pawn, string prompt)
        {
            if (pawn == null || string.IsNullOrEmpty(pawn.def?.defName)) return;
            SetCustomByDefName(pawn.def.defName, prompt);
        }

        public static void SetCustomByDefName(string defName, string prompt)
        {
            if (string.IsNullOrEmpty(defName)) return;
            EnsureLoaded();
            prompt = prompt?.Trim() ?? "";
            if (prompt.Length == 0) customPrompts.Remove(defName);
            else customPrompts[defName] = prompt;
            Save();
        }

        public static IReadOnlyDictionary<string, string> AllCustom
        {
            get { EnsureLoaded(); return customPrompts; }
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(StorePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var doc = new XDocument(new XElement("CustomRacePrompts",
                    customPrompts.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                                 .Select(kv => new XElement("race", kv.Value,
                                     new XAttribute("defName", kv.Key)))));
                doc.Save(StorePath);
            }
            catch (Exception ex)
            {
                Log.Warning("[RimAiVinci] 自定义种族描述保存失败: " + ex.Message);
            }
        }
    }
}
