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
            var pType = PawnPortraitTypeHelper.GetPawnType(pawn);
            string subFolder = PawnPortraitTypeHelper.GetSubFolder(pType);

            string cleanName = Sanitize(pawn.Name != null ? pawn.Name.ToStringShort : pawn.def.label);
            string race = Sanitize(pawn.def.label);
            string gender = pawn.gender.ToString();
            string age = pawn.ageTracker.AgeBiologicalYears.ToString();
            string saveName = Sanitize(Find.World?.info?.name ?? "UnknownSave");
            string id = pawn.ThingID;

            string folderName = $"{cleanName}_{race}_{gender}_{age}_{saveName}_{id}";
            return Path.Combine(BasePath, subFolder, folderName);
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
                var pType = PawnPortraitTypeHelper.GetPawnType(pawn);
                string subFolder = PawnPortraitTypeHelper.GetSubFolder(pType);
                string folderPath = GetPawnFolder(pawn);
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                string fileName = $"{DateTime.Now.Ticks}.png";
                string fullPath = Path.Combine(folderPath, fileName);
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());

                return Path.Combine(subFolder, new DirectoryInfo(folderPath).Name, fileName);
            }
            catch (Exception ex) { Log.Error($"[Rim AiVinci] Save Error: {ex.Message}"); return null; }
        }

        public static Texture2D LoadTextureFromDisk(string relativeOrFullPath)
        {
            try
            {
                string fullPath = relativeOrFullPath;
                if (!Path.IsPathRooted(fullPath)) fullPath = Path.Combine(BasePath, relativeOrFullPath);
                if (!File.Exists(fullPath))
                {
                    foreach (string sub in new[] { "Human", "Animal", "Mechanoid" })
                    {
                        string altPath = Path.Combine(BasePath, sub, relativeOrFullPath);
                        if (File.Exists(altPath)) { fullPath = altPath; break; }
                    }
                }
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

            string[] directories = Directory.GetDirectories(BasePath, "*", SearchOption.AllDirectories);

            List<Pawn> allPawns = new List<Pawn>();
            if (Current.Game != null)
            {
                foreach (var map in Current.Game.Maps) allPawns.AddRange(map.mapPawns.AllPawns);
                allPawns.AddRange(Find.World.worldPawns.AllPawnsAlive);
            }

            foreach (string folderPath in directories)
            {
                if (!Directory.GetFiles(folderPath, "*.png").Any()) continue;

                string folderName = new DirectoryInfo(folderPath).Name;
                string matchedPawnID = "Unknown";

                Pawn match = allPawns.FirstOrDefault(p => folderName.EndsWith(p.ThingID));
                if (match != null)
                {
                    matchedPawnID = match.ThingID;
                }
                else
                {
                    int lastIdx = folderName.LastIndexOf('_');
                    if (lastIdx != -1 && lastIdx < folderName.Length - 1)
                        matchedPawnID = folderName.Substring(lastIdx + 1);
                }

                string relDir = folderPath.Substring(BasePath.Length).TrimStart(Path.DirectorySeparatorChar);

                string[] files = Directory.GetFiles(folderPath, "*.png");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string relPath = Path.Combine(relDir, fileName).Replace('\\', '/');

                    if (!knownPaths.Contains(relPath) && !knownPaths.Contains(relPath.Replace('/', '\\')))
                    {
                        var pType = matchedPawnID != "Unknown"
                            ? allPawns.FirstOrDefault(p => p.ThingID == matchedPawnID) is Pawn mp
                                ? PawnPortraitTypeHelper.GetPawnType(mp)
                                : PawnPortraitType.Human
                            : PawnPortraitType.Human;

                        ArtData recovered = new ArtData
                        {
                            artID = Guid.NewGuid().ToString(),
                            pawnID = matchedPawnID,
                            relativePath = relPath,
                            prompt = "本地找回 (Recovered)",
                            timestamp = File.GetCreationTime(file).Ticks,
                            authorName = "Unknown",
                            pawnType = pType
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