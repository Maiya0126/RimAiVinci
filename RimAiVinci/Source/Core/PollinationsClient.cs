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

        public static bool HasUserKey
        {
            get
            {
                var s = RimAiVinciMod.settings;
                return s != null && s.providerIndex == ApiProviders.Pollinations && !string.IsNullOrEmpty(s.apiKey);
            }
        }

        static PollinationsClient()
        {
            client.Timeout = TimeSpan.FromMinutes(4);
            ApplyFreeHeaders();
            Log.Message("[Rim AiVinci] Pollinations Initialized.");
        }

        private static void ApplyFreeHeaders()
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(GetRandomUserAgent());
            client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        private static void ApplyAuthHeaders(string apiKey)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(GetRandomUserAgent());
            client.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Referer", "https://enter.pollinations.ai/");
            client.DefaultRequestHeaders.Add("Origin", "https://enter.pollinations.ai");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        public static bool IsCoolingDown(out int secondsRemaining)
        {
            if (HasUserKey)
            {
                secondsRemaining = 0;
                return false;
            }
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

            Task.Run(async () =>
            {
                try
                {
                    if (!HasUserKey) await Task.Delay(UnityEngine.Random.Range(300, 2000));

                    bool useNewEndpoint = HasUserKey;
                    string userKey = useNewEndpoint ? RimAiVinciMod.settings.apiKey : "";

                    if (useNewEndpoint)
                        ApplyAuthHeaders(userKey);
                    else
                        ApplyFreeHeaders();

                    string url = useNewEndpoint
                        ? $"https://gen.pollinations.ai/image/{encodedPrompt}?width=1024&height=1024&model=flux&nologo=true&seed={seed}&safe=true&key={userKey}"
                        : $"https://image.pollinations.ai/prompt/{encodedPrompt}?width=1024&height=1024&model=flux&nologo=true&seed={seed}&safe=true";

                    Log.Message($"[Rim AiVinci] Pollinations Request (Seed: {seed}, Endpoint: {(useNewEndpoint ? "gen+key" : "image/free")})...");

                    byte[] imageBytes = await FetchWithRetryAsync(url);

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
                    Log.Warning($"[Rim AiVinci] Pollinations Failed: {ex.Message}");
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        if (ex.Message.Contains("401"))
                            Messages.Message("RAV_Pollinations_Unauthorized".Translate(), MessageTypeDefOf.RejectInput, false);
                        else if (ex.Message.Contains("402"))
                            Messages.Message("RAV_Pollinations_NoCredits".Translate(), MessageTypeDefOf.RejectInput, false);
                        else
                            Messages.Message("RimAiVinci_FreeServerBusy".Translate(), MessageTypeDefOf.RejectInput, false);
                        callback?.Invoke(null);
                    }, "RimAiVinci_AIError", false, null);
                }
            });
        }

        private static async Task<byte[]> FetchWithRetryAsync(string url, int maxRetries = 2)
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                    if (data != null && data.Length > 5000) return data;
                    if (attempt < maxRetries)
                    {
                        if (HasUserKey) ApplyAuthHeaders(RimAiVinciMod.settings.apiKey);
                        else ApplyFreeHeaders();
                        await Task.Delay(UnityEngine.Random.Range(2000, 5000));
                        continue;
                    }
                    return data;
                }
                if ((int)response.StatusCode == 429 || response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    if (attempt < maxRetries)
                    {
                        if (HasUserKey) ApplyAuthHeaders(RimAiVinciMod.settings.apiKey);
                        else ApplyFreeHeaders();
                        await Task.Delay((attempt + 1) * 5000);
                        continue;
                    }
                    throw new Exception($"Rate limited after {maxRetries + 1} attempts");
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new Exception("401 Unauthorized");
                if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                    throw new Exception("402 PaymentRequired");
                throw new Exception($"Server Error {response.StatusCode}");
            }
            return null;
        }

        private static string GetRandomUserAgent()
        {
            int majorVer = UnityEngine.Random.Range(124, 131);
            int buildVer = UnityEngine.Random.Range(0, 5000);
            float roll = UnityEngine.Random.value;
            if (roll < 0.5f)
                return $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{majorVer}.0.{buildVer}.0 Safari/537.36";
            if (roll < 0.75f)
                return $"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{majorVer}.0.{buildVer}.0 Safari/537.36";
            if (roll < 0.88f)
                return $"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:{majorVer}.0) Gecko/20100101 Firefox/{majorVer}.0";
            return $"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";
        }
    }
}
