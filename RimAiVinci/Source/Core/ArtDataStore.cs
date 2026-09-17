using System.Collections.Generic;
using Verse;
using RimWorld.Planet;

namespace RimAiVinci
{
    public class ArtDataStore : WorldComponent
    {
        // 存储画廊数据
        private Dictionary<string, ArtDataListWrapper> artRegistry = new Dictionary<string, ArtDataListWrapper>();

        // ✨ 新增：存储"当前激活的立绘"
        // Key: PawnID, Value: 图片相对路径
        private Dictionary<string, string> activePortraits = new Dictionary<string, string>();

        // ✨ 新增：状态立绘映射
        // Key: "PawnID|stateInt", Value: 图片相对路径
        private Dictionary<string, string> statePortraits = new Dictionary<string, string>();

        private List<string> workingKeys = new List<string>();
        private List<ArtDataListWrapper> workingValues = new List<ArtDataListWrapper>();

        // 辅助 Scribe
        private List<string> activeKeys = new List<string>();
        private List<string> activeValues = new List<string>();
        private List<string> stateKeys = new List<string>();
        private List<string> stateValues = new List<string>();

        public ArtDataStore(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref artRegistry, "artRegistry", LookMode.Value, LookMode.Deep, ref workingKeys, ref workingValues);

            // ✨ 保存激活的立绘设置
            Scribe_Collections.Look(ref activePortraits, "activePortraits", LookMode.Value, LookMode.Value, ref activeKeys, ref activeValues);

            // ✨ 保存状态立绘映射
            Scribe_Collections.Look(ref statePortraits, "statePortraits", LookMode.Value, LookMode.Value, ref stateKeys, ref stateValues);

            if (artRegistry == null) artRegistry = new Dictionary<string, ArtDataListWrapper>();
            if (activePortraits == null) activePortraits = new Dictionary<string, string>();
            if (statePortraits == null) statePortraits = new Dictionary<string, string>();
        }

        public void AddArt(ArtData data)
        {
            if (!artRegistry.ContainsKey(data.pawnID)) artRegistry[data.pawnID] = new ArtDataListWrapper();
            artRegistry[data.pawnID].list.Add(data);

            // 如果是该小人的第一张图，自动设为默认立绘
            if (!activePortraits.ContainsKey(data.pawnID))
            {
                SetActivePortrait(data.pawnID, data.relativePath);
            }
        }

        public void RemoveArt(ArtData data)
        {
            if (artRegistry.ContainsKey(data.pawnID))
            {
                artRegistry[data.pawnID].list.Remove(data);
                // 如果删除的是当前立绘，移除激活状态
                if (activePortraits.ContainsKey(data.pawnID) && activePortraits[data.pawnID] == data.relativePath)
                {
                    activePortraits.Remove(data.pawnID);
                }
                // 清理指向该图的状态映射
                List<string> toRemove = new List<string>();
                foreach (var kv in statePortraits)
                {
                    if (kv.Key.StartsWith(data.pawnID + "|") && kv.Value == data.relativePath)
                        toRemove.Add(kv.Key);
                }
                foreach (var k in toRemove) statePortraits.Remove(k);
            }
        }

        // ✨ 设置当前立绘
        public void SetActivePortrait(string pawnID, string relativePath)
        {
            activePortraits[pawnID] = relativePath;
        }

        // ✨ 获取当前立绘路径
        public string GetActivePortraitPath(Pawn p)
        {
            if (p == null) return null;
            if (activePortraits.TryGetValue(p.ThingID, out string path))
            {
                return path;
            }
            return null; // 没有激活的立绘
        }

        public List<ArtData> GetArtForPawn(Pawn p)
        {
            if (p == null) return new List<ArtData>();
            string id = p.ThingID;
            if (artRegistry.ContainsKey(id)) return artRegistry[id].list;
            return new List<ArtData>();
        }

        private static string StateKey(string pawnID, PortraitState st)
        {
            return pawnID + "|" + (int)st;
        }

        public void SetStatePortrait(string pawnID, PortraitState st, string relativePath)
        {
            statePortraits[StateKey(pawnID, st)] = relativePath;
        }

        public void ClearAllStatesFor(string pawnID)
        {
            List<string> toRemove = new List<string>();
            foreach (var kv in statePortraits)
            {
                if (kv.Key.StartsWith(pawnID + "|")) toRemove.Add(kv.Key);
            }
            foreach (var k in toRemove) statePortraits.Remove(k);
        }

        public string GetStatePortraitPath(Pawn p, PortraitState st)
        {
            if (p == null) return null;
            string v;
            if (statePortraits.TryGetValue(StateKey(p.ThingID, st), out v)) return v;
            return null;
        }

        public List<PortraitState> GetStatesFor(string pawnID, string relativePath)
        {
            List<PortraitState> result = new List<PortraitState>();
            foreach (var kv in statePortraits)
            {
                if (kv.Value == relativePath && kv.Key.StartsWith(pawnID + "|"))
                {
                    int idx;
                    if (int.TryParse(kv.Key.Substring(pawnID.Length + 1), out idx))
                        result.Add((PortraitState)idx);
                }
            }
            return result;
        }

        public IEnumerable<List<ArtData>> GetAllArts()
        {
            foreach (var wrapper in artRegistry.Values) yield return wrapper.list;
        }
    }

    public class ArtDataListWrapper : IExposable
    {
        public List<ArtData> list = new List<ArtData>();
        public void ExposeData() { Scribe_Collections.Look(ref list, "arts", LookMode.Deep); }
    }
}