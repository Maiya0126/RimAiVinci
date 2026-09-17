using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAiVinci
{
    public static class SDWebUIClient
    {
        private static readonly HttpClient client = new HttpClient();

        static SDWebUIClient()
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        }

        public static void GenerateImageAsync(string prompt, Action<Texture2D> callback)
        {
            string url = GetBaseUrl();
            string overrideSettings = GetOverrideModel();
            string negativePrompt = "bad quality, blurry, deformed, ugly, worst quality";

            string json = overrideSettings != null
                ? $"{{\"prompt\":\"{Escape(prompt)}\",\"negative_prompt\":\"{Escape(negativePrompt)}\",\"width\":1024,\"height\":1024,\"steps\":25,\"cfg_scale\":7,\"sampler_name\":\"Euler a\",\"seed\":{UnityEngine.Random.Range(0, 99999999)},\"override_settings\":{{\"sd_model_checkpoint\":\"{Escape(overrideSettings)}\"}}}}"
                : $"{{\"prompt\":\"{Escape(prompt)}\",\"negative_prompt\":\"{Escape(negativePrompt)}\",\"width\":1024,\"height\":1024,\"steps\":25,\"cfg_scale\":7,\"sampler_name\":\"Euler a\",\"seed\":{UnityEngine.Random.Range(0, 99999999)}}}";

            string endpoint = url.TrimEnd('/') + "/sdapi/v1/txt2img";
            SendRequest(endpoint, json, callback);
        }

        public static void GenerateImageToImageAsync(string prompt, Texture2D referenceImage, float strength, Action<Texture2D> callback)
        {
            if (referenceImage == null) { callback?.Invoke(null); return; }

            string url = GetBaseUrl();
            string overrideSettings = GetOverrideModel();
            string negativePrompt = "bad quality, blurry, deformed, ugly, worst quality";
            byte[] imageBytes = referenceImage.EncodeToPNG();
            string base64Image = Convert.ToBase64String(imageBytes);

            string json = overrideSettings != null
                ? $"{{\"prompt\":\"{Escape(prompt)}\",\"negative_prompt\":\"{Escape(negativePrompt)}\",\"init_images\":[\"data:image/png;base64,{base64Image}\"],\"denoising_strength\":{strength.ToString("F2")},\"width\":1024,\"height\":1024,\"steps\":25,\"cfg_scale\":7,\"sampler_name\":\"Euler a\",\"seed\":{UnityEngine.Random.Range(0, 99999999)},\"override_settings\":{{\"sd_model_checkpoint\":\"{Escape(overrideSettings)}\"}}}}"
                : $"{{\"prompt\":\"{Escape(prompt)}\",\"negative_prompt\":\"{Escape(negativePrompt)}\",\"init_images\":[\"data:image/png;base64,{base64Image}\"],\"denoising_strength\":{strength.ToString("F2")},\"width\":1024,\"height\":1024,\"steps\":25,\"cfg_scale\":7,\"sampler_name\":\"Euler a\",\"seed\":{UnityEngine.Random.Range(0, 99999999)}}}";

            string endpoint = url.TrimEnd('/') + "/sdapi/v1/img2img";
            SendRequest(endpoint, json, callback);
        }

        public static void TestConnection(Action<string, bool> resultCallback)
        {
            string url = GetBaseUrl();
            if (string.IsNullOrEmpty(url))
            {
                resultCallback("RAV_Test_NoUrl".Translate(), false);
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    string endpoint = url.TrimEnd('/') + "/sdapi/v1/sd-models";
                    HttpResponseMessage response = await client.GetAsync(endpoint);
                    string body = await response.Content.ReadAsStringAsync();

                    LongEventHandler.QueueLongEvent(() =>
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            resultCallback("RAV_Test_Success".Translate(), true);
                        }
                        else
                        {
                            resultCallback("RAV_Test_HttpError".Translate((int)response.StatusCode, body.Length > 300 ? body.Substring(0, 300) : body), false);
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

        public static void FetchModelList(Action<List<string>, string> callback)
        {
            string url = GetBaseUrl();
            Task.Run(async () =>
            {
                List<string> models = null;
                string error = null;
                try
                {
                    HttpResponseMessage resp = await client.GetAsync(url + "/sdapi/v1/sd-models");
                    string body = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode) error = "HTTP " + (int)resp.StatusCode;
                    else models = ExtractModelTitles(body);
                }
                catch (Exception ex) { error = ex.Message; }
                LongEventHandler.QueueLongEvent(() => callback?.Invoke(models, error), "RimAiVinci_FetchModels", false, null);
            });
        }

        private static List<string> ExtractModelTitles(string json)
        {
            List<string> result = new List<string>();
            try
            {
                int pos = 0;
                while (true)
                {
                    int tIdx = json.IndexOf("\"title\":", pos);
                    if (tIdx == -1) break;
                    int colon = tIdx + "\"title\":".Length;
                    while (colon < json.Length && json[colon] == ' ') colon++;
                    if (colon >= json.Length || json[colon] != '"') { pos = tIdx + 8; continue; }
                    int end = json.IndexOf('"', colon + 1);
                    if (end == -1) break;
                    result.Add(json.Substring(colon + 1, end - colon - 1));
                    pos = end + 1;
                }
            }
            catch { }
            return result;
        }

        private static void SendRequest(string endpoint, string jsonBody, Action<Texture2D> callback)
        {
            Task.Run(async () =>
            {
                try
                {
                    var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(endpoint, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        string base64 = ExtractBase64FromJson(responseBody);
                        if (!string.IsNullOrEmpty(base64))
                        {
                            byte[] imageBytes = Convert.FromBase64String(base64);
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                Texture2D tex = new Texture2D(2, 2);
                                tex.LoadImage(imageBytes);
                                tex.name = "AI_SDWebUI_" + DateTime.Now.Ticks;
                                callback?.Invoke(tex);
                            }, "RimAiVinci_ProcessingAIImage", false, null);
                        }
                        else
                        {
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                Log.Error($"[Rim AiVinci] SD-WebUI:无法提取图片数据: {responseBody.Substring(0, Math.Min(500, responseBody.Length))}");
                                Messages.Message("RAV_SDWebUI_NoImageData".Translate(), MessageTypeDefOf.RejectInput);
                                callback?.Invoke(null);
                            }, "RimAiVinci_AIError", false, null);
                        }
                    }
                    else
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Log.Error($"[Rim AiVinci] SD-WebUI 请求失败: {response.StatusCode}\n{responseBody}");
                            Messages.Message("RAV_SDWebUI_GenFail".Translate(((int)response.StatusCode).ToString()), MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                    }
                }
                catch (Exception ex)
                {
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        Log.Error($"[Rim AiVinci] SD-WebUI 网络错误: {ex.Message}");
                        Messages.Message("RAV_SDWebUI_NetError".Translate(), MessageTypeDefOf.RejectInput);
                        callback?.Invoke(null);
                    }, "RimAiVinci_AIError", false, null);
                }
            });
        }

        private static string GetBaseUrl()
        {
            string url = RimAiVinciMod.settings.apiUrl;
            if (string.IsNullOrEmpty(url)) url = "http://127.0.0.1:7860";
            return url.TrimEnd('/');
        }

        private static string GetOverrideModel()
        {
            string model = RimAiVinciMod.settings.modelName;
            return string.IsNullOrEmpty(model) ? null : model;
        }

        private static string ExtractBase64FromJson(string json)
        {
            try
            {
                string key = "\"images\":[\"";
                int start = json.IndexOf(key);
                if (start == -1) return null;
                start += key.Length;
                int end = json.IndexOf("\"", start);
                if (end == -1) return null;
                string val = json.Substring(start, end - start);
                if (val.StartsWith("data:image")) val = val.Substring(val.IndexOf(',') + 1);
                return val;
            }
            catch { return null; }
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
        }
    }
}
