using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAiVinci
{
    public static class SiliconClient
    {
        private static readonly HttpClient client = new HttpClient();

        static SiliconClient()
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        }

        public static void GenerateImageAsync(string prompt, Action<Texture2D> callback)
        {
            // 模式 0 = 文生图
            string json = BuildJson(prompt, null, 0, false);
            SendRequest(json, callback);
        }

        public static void GenerateImageToImageAsync(string prompt, Texture2D referenceImage, float strength, Action<Texture2D> callback)
        {
            if (referenceImage == null) { callback?.Invoke(null); return; }
            byte[] imageBytes = referenceImage.EncodeToPNG();
            if (imageBytes == null || imageBytes.Length == 0) { callback?.Invoke(null); return; }
            string base64Image = Convert.ToBase64String(imageBytes);
            string imageDataUrl = $"data:image/png;base64,{base64Image}";

            // 模式 1 = 图生图
            string json = BuildJson(prompt, imageDataUrl, strength, true);
            Log.Message($"[Rim AiVinci] 发起图生图请求 (强度: {strength:P0})...");
            SendRequest(json, callback);
        }

        // ✨ 改动：增加 isImageToImage 参数来决定用哪个模型
        private static string BuildJson(string prompt, string base64Image, float strength, bool isImageToImage)
        {
            // 根据模式读取对应的模型设置
            string model = isImageToImage ? RimAiVinciMod.settings.modelNameI2I : RimAiVinciMod.settings.modelName;

            // 兜底默认值
            if (string.IsNullOrEmpty(model)) model = "black-forest-labs/FLUX.1-schnell";

            string safePrompt = prompt.Replace("\"", "\\\"").Replace("\n", " ");

            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"model\": \"{model}\",");
            sb.Append($"\"prompt\": \"{safePrompt}\",");
            sb.Append($"\"image_size\": \"1024x1024\",");
            sb.Append($"\"num_inference_steps\": 28,");
            sb.Append($"\"seed\": {UnityEngine.Random.Range(0, 99999999)}");

            if (isImageToImage && !string.IsNullOrEmpty(base64Image))
            {
                sb.Append($",\"image\": \"{base64Image}\"");
                sb.Append($",\"strength\": {strength}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private static void SendRequest(string jsonBody, Action<Texture2D> callback)
        {
            string apiKey = RimAiVinciMod.settings.apiKey;
            string url = RimAiVinciMod.settings.apiUrl;
            if (string.IsNullOrEmpty(url)) url = "https://api.siliconflow.cn/v1/images/generations";

            if (string.IsNullOrEmpty(apiKey))
            {
                Messages.Message("请先在 Mod 设置中填写 API Key。", MessageTypeDefOf.RejectInput);
                callback?.Invoke(null); // 失败回调
                return;
            }

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            Task.Run(async () =>
            {
                try
                {
                    HttpResponseMessage response = await client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        string imageUrl = ExtractUrlFromJson(responseBody);
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            byte[] imageBytes = await client.GetByteArrayAsync(imageUrl);
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                Texture2D tex = new Texture2D(2, 2);
                                tex.LoadImage(imageBytes);
                                tex.name = "AI_Gen_" + DateTime.Now.Ticks;
                                callback?.Invoke(tex);
                            }, "ProcessingAIImage", false, null);
                        }
                        else
                        {
                            // 即使 JSON 解析失败也要回调，防止 UI 卡死
                            Log.Error($"[Rim AiVinci] 无法从返回中提取 URL: {responseBody}");
                            LongEventHandler.QueueLongEvent(() => callback?.Invoke(null), "AIError", false, null);
                        }
                    }
                    else
                    {
                        Log.Error($"[Rim AiVinci] API 请求失败: {response.StatusCode}\n{responseBody}");
                        Messages.Message($"生成失败: {response.StatusCode}。请检查设置。", MessageTypeDefOf.RejectInput);
                        LongEventHandler.QueueLongEvent(() => callback?.Invoke(null), "AIError", false, null);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[Rim AiVinci] 网络异常: {ex.Message}");
                    Messages.Message("网络连接出错，请检查 URL 设置。", MessageTypeDefOf.RejectInput);
                    LongEventHandler.QueueLongEvent(() => callback?.Invoke(null), "AIError", false, null);
                }
            });
        }

        private static string ExtractUrlFromJson(string json)
        {
            try
            {
                string key = "\"url\":";
                int start = json.IndexOf(key);
                if (start == -1) return null;
                start += key.Length;
                int quoteStart = json.IndexOf("\"", start);
                if (quoteStart == -1) return null;
                int quoteEnd = json.IndexOf("\"", quoteStart + 1);
                if (quoteEnd == -1) return null;
                return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            }
            catch { return null; }
        }
    }
}