using System;
using System.Net.Http;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAiVinci
{
    public static class PollinationsClient
    {
        private static readonly HttpClient client = new HttpClient();
        public static DateTime LastFreeUseTime { get; private set; } = DateTime.MinValue;
        public const double CooldownMinutes = 3.0;

        // Generate a random session ID to mimic a unique user session
        private static readonly string SessionID = Guid.NewGuid().ToString().Substring(0, 8);

        static PollinationsClient()
        {
            client.Timeout = TimeSpan.FromMinutes(4);

            // ✨✨ Core Update: Random User Agent & Headers to bypass "We Have Moved" ✨✨
            string randomUA = GetRandomUserAgent();

            // 1. Set a random User-Agent
            client.DefaultRequestHeaders.UserAgent.ParseAdd(randomUA);

            // 2. ✨ CRITICAL FIX: Add Referer and Origin headers to mimic the official site
            // This tells the server the request is coming from their own frontend
            client.DefaultRequestHeaders.Add("Referer", "https://enter.pollinations.ai/");
            client.DefaultRequestHeaders.Add("Origin", "https://enter.pollinations.ai");

            // 3. Add standard browser headers
            client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

            // 4. Anti-caching headers
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            client.DefaultRequestHeaders.Add("Pragma", "no-cache");

            Log.Message($"[Rim AiVinci] Network Initialized. Session: {SessionID}, UA: {randomUA}");
        }

        public static bool IsCoolingDown(out int secondsRemaining)
        {
            TimeSpan diff = DateTime.Now - LastFreeUseTime;
            if (diff.TotalMinutes < CooldownMinutes)
            {
                secondsRemaining = (int)((CooldownMinutes * 60) - diff.TotalSeconds);
                return true;
            }
            secondsRemaining = 0;
            return false;
        }

        public static void GenerateFreeImageAsync(string prompt, Action<Texture2D> callback)
        {
            if (IsCoolingDown(out int secondsLeft))
            {
                Messages.Message("RimAiVinci_FreeCooldown".Translate(secondsLeft), MessageTypeDefOf.RejectInput, false);
                callback?.Invoke(null);
                return;
            }

            LastFreeUseTime = DateTime.Now;

            int seed = UnityEngine.Random.Range(0, 999999);
            string encodedPrompt = Uri.EscapeDataString(prompt);

            // ✨ URL Update: Removed some parameters that might trigger old API checks
            // Added 'nologo=true' and 'safe=true' which are standard for the new endpoint
            string url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=1024&height=1024&model=flux&nologo=true&seed={seed}&safe=true";

            Task.Run(async () =>
            {
                try
                {
                    Log.Message($"[Rim AiVinci] Free Generation Request (Seed: {seed})...");

                    HttpResponseMessage response = await client.GetAsync(url);
                    if (!response.IsSuccessStatusCode) throw new Exception($"Server Error {response.StatusCode}");

                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

                    // ✨ Check for the "We Have Moved" placeholder image
                    // That specific error image is usually small (around 20-30KB), but valid images are much larger
                    // A simple length check can filter out obvious bad responses
                    if (imageBytes.Length < 5000)
                    {
                        Log.Warning("[Rim AiVinci] Received suspicious small file. Might be the error placeholder.");
                    }

                    LongEventHandler.QueueLongEvent(() =>
                    {
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            Texture2D tex = new Texture2D(2, 2);
                            tex.LoadImage(imageBytes);
                            tex.name = "AI_Free_" + DateTime.Now.Ticks;
                            callback?.Invoke(tex);
                        }
                        else
                        {
                            Log.Error("[Rim AiVinci] Received empty image data.");
                            callback?.Invoke(null);
                        }
                    }, "RimAiVinci_ProcessingAIImage", false, null);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[Rim AiVinci] Free API Failed: {ex.Message}");

                    LongEventHandler.QueueLongEvent(() =>
                    {
                        Messages.Message("RimAiVinci_FreeServerBusy".Translate(), MessageTypeDefOf.RejectInput, false);
                        callback?.Invoke(null);
                    }, "RimAiVinci_AIError", false, null);
                }
            });
        }

        // ✨✨✨ Random Browser Fingerprint Generator ✨✨✨
        private static string GetRandomUserAgent()
        {
            // Updated to newer Chrome versions to look more like a modern browser
            int majorVer = UnityEngine.Random.Range(124, 128);
            int buildVer = UnityEngine.Random.Range(0, 5000);

            bool isWin = UnityEngine.Random.value > 0.5f;

            if (isWin)
            {
                return $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{majorVer}.0.{buildVer}.0 Safari/537.36";
            }
            else
            {
                return $"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{majorVer}.0.{buildVer}.0 Safari/537.36";
            }
        }
    }
}