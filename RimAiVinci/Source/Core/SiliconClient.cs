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

        private static string BuildJson(string prompt, string base64Image, float strength, bool isImageToImage)
        {
            string model = isImageToImage ? RimAiVinciMod.settings.modelNameI2I : RimAiVinciMod.settings.modelName;
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

            // 这段还在主线程，直接调用没问题
            if (string.IsNullOrEmpty(apiKey))
            {
                Messages.Message("请先在 Mod 设置中填写 API Key。", MessageTypeDefOf.RejectInput);
                callback?.Invoke(null); // 失败回调
                return;
            }

            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            // 开启后台线程
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

                            // 成功获取图片，回到主线程处理贴图
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                if (imageBytes != null && imageBytes.Length > 0)
                                {
                                    Texture2D tex = new Texture2D(2, 2);
                                    tex.LoadImage(imageBytes);
                                    tex.name = "AI_Gen_" + DateTime.Now.Ticks;
                                    callback?.Invoke(tex);
                                }
                                else
                                {
                                    // ✨ 修复：将后台报错和回调打包进主线程
                                    Log.Error($"[Rim AiVinci] 无法从返回中提取 URL: {responseBody}");
                                    callback?.Invoke(null);
                                }
                            }, "ProcessingAIImage", false, null);
                        }
                        else
                        {
                            // ✨ 修复：JSON 解析失败时的报错和回调打包进主线程
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                Log.Error($"[Rim AiVinci] 无法从返回中提取 URL: {responseBody}");
                                callback?.Invoke(null);
                            }, "AIError", false, null);
                        }
                    }
                    else
                    {
                        // ✨ 修复：API 请求失败（如欠费、限流）时的日志和左上角红字提示，必须在主线程执行
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Log.Error($"[Rim AiVinci] API 请求失败: {response.StatusCode}\n{responseBody}");
                            Messages.Message($"生成失败: {response.StatusCode}。请检查设置。", MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }, "AIError", false, null);
                    }
                }
                catch (Exception ex)
                {
                    // ✨ 修复：网络异常（如断网）时的提示，必须在主线程执行
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        Log.Error($"[Rim AiVinci] 网络异常: {ex.Message}");
                        Messages.Message("网络连接出错，请检查 URL 设置。", MessageTypeDefOf.RejectInput);
                        callback?.Invoke(null);
                    }, "AIError", false, null);
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