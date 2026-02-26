using Verse;
using HarmonyLib;
using System.Reflection;

namespace RimAiVinci
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("com.maiya.rimaivinci");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[Rim AiVinci] Harmony Patches Applied.");
        }
    }
}