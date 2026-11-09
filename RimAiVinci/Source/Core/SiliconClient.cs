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
        private static readonly HttpClient client = new HttpClient(new HttpClientHandler { UseCookies = false });

        static SiliconClient()
        {
            client.Timeout = TimeSpan.FromSeconds(240);
        }

        /// <summary>
        /// 规范化生图 API 端点 URL（405 修复）：
        /// 完整端点保持不变；版本目录（/v1、/v2、/v3、/v4、/v1beta、/openai、/api/paas 等）自动补 /images/generations；
        /// 仅填域名自动补 /v1/images/generations。不回写玩家原配置。
        /// </summary>
        public static string NormalizeEndpointUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
                return "https://api.siliconflow.cn/v1/images/generations";

            string url = rawUrl.Trim().TrimEnd('/');
            string lower = url.ToLowerInvariant();

            if (lower.EndsWith("/images/generations") ||
                lower.EndsWith("/images/edits") ||
                lower.EndsWith("/chat/completions") ||
                lower.EndsWith("/completions") ||
                lower.EndsWith("/messages") ||
                lower.EndsWith("/generatecontent"))
            {
                return url;
            }

            if (lower.EndsWith("/v1") || lower.EndsWith("/v2") || lower.EndsWith("/v3") ||
                lower.EndsWith("/v4") || lower.EndsWith("/v1beta") || lower.EndsWith("/openai") ||
                lower.EndsWith("/api/paas") || lower.EndsWith("/inference"))
            {
                return url + "/images/generations";
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                string path = uri.AbsolutePath.Trim('/');
                if (string.IsNullOrEmpty(path))
                {
                    return url + "/v1/images/generations";
                }
            }

            return url;
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
            string url = NormalizeEndpointUrl(RimAiVinciMod.settings.apiUrl);

            // 这段还在主线程，直接调用没问题
            if (string.IsNullOrEmpty(apiKey))
            {
                Messages.Message("RAV_ArtCreator_NeedApiKey".Translate(), MessageTypeDefOf.RejectInput);
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
                        byte[] imageBytes = null;
                        string imageUrl = ExtractUrlFromJson(responseBody);
                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            imageBytes = await client.GetByteArrayAsync(imageUrl);
                        }
                        else
                        {
                            string b64 = ExtractBase64FromJson(responseBody);
                            if (!string.IsNullOrEmpty(b64))
                            {
                                imageBytes = Convert.FromBase64String(b64);
                            }
                        }

                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                Texture2D tex = new Texture2D(2, 2);
                                tex.LoadImage(imageBytes);
                                tex.name = "AI_Gen_" + DateTime.Now.Ticks;
                                callback?.Invoke(tex);
                            }, "RimAiVinci_ProcessingAIImage", false, null);
                        }
                        else
                        {
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                Log.Error($"[Rim AiVinci] 无法从返回中提取图片 URL 或 b64_json: {responseBody}");
                                callback?.Invoke(null);
                            }, "RimAiVinci_AIError", false, null);
                        }
                    }
                    else
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Log.Error($"[Rim AiVinci] API 请求失败: {response.StatusCode}\n{responseBody}");
                            if ((int)response.StatusCode == 405)
                                Messages.Message("RAV_Error_Http405".Translate(), MessageTypeDefOf.RejectInput);
                            else
                                Messages.Message("RAV_GenFail_CheckSettings".Translate(((int)response.StatusCode).ToString()), MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                    }
                }
                catch (Exception ex)
                {
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        Log.Error("[Rim AiVinci] 网络异常: " + ex.ToString());
                        Messages.Message("RAV_NetError_CheckUrl".Translate(), MessageTypeDefOf.RejectInput);
                        callback?.Invoke(null);
                    }, "RimAiVinci_AIError", false, null);
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

        private static string ExtractBase64FromJson(string json)
        {
            try
            {
                string key = "\"b64_json\":";
                int start = json.IndexOf(key);
                if (start == -1) return null;
                start += key.Length;
                int quoteStart = json.IndexOf("\"", start);
                if (quoteStart == -1) return null;
                int quoteEnd = json.IndexOf("\"", quoteStart + 1);
                if (quoteEnd == -1) return null;
                string val = json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                if (val.StartsWith("data:image"))
                {
                    int comma = val.IndexOf(',');
                    if (comma != -1) val = val.Substring(comma + 1);
                }
                return val;
            }
            catch { return null; }
        }

        public static void TestConnection(Action<string, bool> resultCallback)
        {
            string apiKey = RimAiVinciMod.settings.apiKey;
            string rawUrl = RimAiVinciMod.settings.apiUrl;
            string model = RimAiVinciMod.settings.modelName;

            if (string.IsNullOrEmpty(apiKey))
            {
                resultCallback("RAV_Test_NoKey".Translate(), false);
                return;
            }
            if (string.IsNullOrEmpty(rawUrl))
            {
                resultCallback("RAV_Test_NoUrl".Translate(), false);
                return;
            }

            string url = NormalizeEndpointUrl(rawUrl);

            Task.Run(async () =>
            {
                try
                {
                    string safePrompt = "test connection".Replace("\"", "\\\"");
                    string json = $"{{\"model\": \"{model ?? "test"}\", \"prompt\": \"{safePrompt}\", \"image_size\": \"1024x1024\", \"num_inference_steps\": 1, \"seed\": 1}}";

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                    HttpResponseMessage response = await client.PostAsync(url, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    LongEventHandler.QueueLongEvent(() =>
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            resultCallback("RAV_Test_Success".Translate(), true);
                        }
                        else if ((int)response.StatusCode == 405)
                        {
                            resultCallback("RAV_Error_Http405".Translate(), false);
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            resultCallback("RAV_Test_AuthFail".Translate((int)response.StatusCode, responseBody.Length > 200 ? responseBody.Substring(0, 200) : responseBody), false);
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                        {
                            resultCallback("RAV_Test_NoCredits".Translate(), false);
                        }
                        else
                        {
                            resultCallback("RAV_Test_HttpError".Translate((int)response.StatusCode, responseBody.Length > 300 ? responseBody.Substring(0, 300) : responseBody), false);
                        }
                    }, "RimAiVinci_AIError", false, null);
                }
                catch (Exception ex)
                {
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        resultCallback("RAV_Test_NetError".Translate(ex.Message), false);
                    }, "RimAiVinci_AIError", false, null);
                }
            });
        }
    }
}