using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAiVinci
{
    public static class ArtFileSystem
    {
        private static string BasePath => Path.Combine(GenFilePaths.SaveDataFolderPath, "RimAiVinci/Gallery");
        private static string ImportPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "RimAiVinci/Import");

        public static string GetActualSaveDir()
        {
            if (!Directory.Exists(BasePath)) Directory.CreateDirectory(BasePath);
            return BasePath;
        }

        public static string GetImportDir()
        {
            if (!Directory.Exists(ImportPath)) Directory.CreateDirectory(ImportPath);
            return ImportPath;
        }

        private static string GetPawnFolder(Pawn pawn)
        {
            string cleanName = Sanitize(pawn.Name.ToStringShort);
            string race = Sanitize(pawn.def.label);
            string gender = pawn.gender.ToString();
            string age = pawn.ageTracker.AgeBiologicalYears.ToString();
            string saveName = Sanitize(Find.World?.info?.name ?? "UnknownSave");
            string id = pawn.ThingID;

            // 格式：Maiya_Human_Female_18_Save1_Human123
            string folderName = $"{cleanName}_{race}_{gender}_{age}_{saveName}_{id}";
            return Path.Combine(BasePath, folderName);
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input)) return "Unknown";
            foreach (char c in Path.GetInvalidFileNameChars()) input = input.Replace(c, '_');
            return input.Replace(" ", "_");
        }

        public static string SaveTextureToDisk(Texture2D tex, Pawn pawn, string prompt)
        {
            if (tex == null) return null;
            try
            {
                string folderPath = GetPawnFolder(pawn);
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string fileName = $"{DateTime.Now.Ticks}.png";
                string fullPath = Path.Combine(folderPath, fileName);
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());

                return Path.Combine(new DirectoryInfo(folderPath).Name, fileName);
            }
            catch (Exception ex) { Log.Error($"[Rim AiVinci] Save Error: {ex.Message}"); return null; }
        }

        public static Texture2D LoadTextureFromDisk(string relativeOrFullPath)
        {
            try
            {
                string fullPath = relativeOrFullPath;
                if (!Path.IsPathRooted(fullPath)) fullPath = Path.Combine(BasePath, relativeOrFullPath);
                if (!File.Exists(fullPath)) return null;
                byte[] bytes = File.ReadAllBytes(fullPath);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                return tex;
            }
            catch { return null; }
        }

        // ✨ 修复：智能扫描找回逻辑
        public static int ScanAndReconcile(ArtDataStore store)
        {
            if (store == null) return 0;
            int recoveredCount = 0;
            if (!Directory.Exists(BasePath)) return 0;

            HashSet<string> knownPaths = new HashSet<string>();
            foreach (var list in store.GetAllArts()) foreach (var art in list) knownPaths.Add(art.relativePath);

            string[] directories = Directory.GetDirectories(BasePath);

            // 获取当前所有地图的所有小人，建立缓存
            List<Pawn> allPawns = new List<Pawn>();
            if (Current.Game != null)
            {
                foreach (var map in Current.Game.Maps) allPawns.AddRange(map.mapPawns.AllPawns);
                allPawns.AddRange(Find.World.worldPawns.AllPawnsAlive);
            }

            foreach (string folderPath in directories)
            {
                string folderName = new DirectoryInfo(folderPath).Name;
                string matchedPawnID = "Unknown";

                // 尝试匹配：文件夹名是否以某个小人的 ThingID 结尾？
                // 例如 "xxx_Milira_Race40292" EndsWith "Milira_Race40292" -> 匹配成功
                Pawn match = allPawns.FirstOrDefault(p => folderName.EndsWith(p.ThingID));
                if (match != null)
                {
                    matchedPawnID = match.ThingID;
                }
                else
                {
                    // 兜底策略：如果实在匹配不到活人，尝试按最后一个下划线分割
                    int lastIdx = folderName.LastIndexOf('_');
                    if (lastIdx != -1 && lastIdx < folderName.Length - 1)
                        matchedPawnID = folderName.Substring(lastIdx + 1);
                }

                string[] files = Directory.GetFiles(folderPath, "*.png");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string relPath = Path.Combine(folderName, fileName).Replace('\\', '/');

                    if (!knownPaths.Contains(relPath) && !knownPaths.Contains(relPath.Replace('/', '\\')))
                    {
                        ArtData recovered = new ArtData
                        {
                            artID = Guid.NewGuid().ToString(),
                            pawnID = matchedPawnID,
                            relativePath = relPath,
                            prompt = "本地找回 (Recovered)",
                            timestamp = File.GetCreationTime(file).Ticks,
                            authorName = "Unknown"
                        };
                        store.AddArt(recovered);
                        recoveredCount++;
                    }
                }
            }
            return recoveredCount;
        }

        public static List<FileInfo> GetImportFiles()
        {
            string dir = GetImportDir();
            DirectoryInfo info = new DirectoryInfo(dir);
            return info.GetFiles("*.*").Where(f => f.Name.EndsWith(".png") || f.Name.EndsWith(".jpg")).OrderByDescending(f => f.CreationTime).ToList();
        }
    }
}