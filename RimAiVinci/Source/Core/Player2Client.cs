using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAiVinci
{
    public static class Player2Client
    {
        private const string GameClientID = "019e299f-c9af-73db-8111-22a6bca30760";
        private const int DefaultPort = 4315;

        private static readonly HttpClient client = new HttpClient();
        private static string p2Key = "";
        private static int apiPort = 0;
        private static bool isConnected = false;
        private static string statusMessage = "RAV_Player2_Disconnected".Translate();

        public static bool IsAvailable => isConnected;
        public static string StatusMessage => statusMessage;

        static Player2Client()
        {
            client.Timeout = TimeSpan.FromSeconds(120);
            TryConnect();
        }

        public static void TryConnect()
        {
            isConnected = false;
            statusMessage = "RAV_Player2_Disconnected".Translate();

            apiPort = ReadApiPort();
            if (apiPort <= 0)
            {
                Log.Message("[RimAiVinci] Player2: 未找到 api.port 文件，App 可能未运行。");
                return;
            }

            TryLoginAsync();
        }

        private static int ReadApiPort()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string portFile = Path.Combine(appData, "game.player2.client", "api.port");
                if (!File.Exists(portFile))
                {
                    return 0;
                }
                string content = File.ReadAllText(portFile).Trim();
                if (int.TryParse(content, out int port) && port > 0 && port <= 65535)
                {
                    Log.Message($"[RimAiVinci] Player2: 从 api.port 读取到端口 {port}");
                    return port;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAiVinci] Player2: 读取 api.port 失败: {ex.Message}");
            }
            return 0;
        }

        private static async void TryLoginAsync()
        {
            try
            {
                string url = $"http://127.0.0.1:{apiPort}/v1/login/web/{GameClientID}";
                HttpResponseMessage response = await client.PostAsync(url, null);
                if (response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync();
                    string key = ExtractSimpleJsonValue(body, "p2Key");
                    if (!string.IsNullOrEmpty(key))
                    {
                        p2Key = key;
                        isConnected = true;
                        statusMessage = "RAV_Player2_Connected".Translate();
                        Log.Message("[RimAiVinci] Player2: 登录成功，已获取 p2Key。");
                    }
                    else
                    {
                        Log.Warning($"[RimAiVinci] Player2: 登录响应中未找到 p2Key: {body}");
                    }
                }
                else
                {
                    Log.Warning($"[RimAiVinci] Player2: 登录失败 {response.StatusCode}，App 可能未登录。");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAiVinci] Player2: 连接失败: {ex.Message}");
            }
        }

        public static void GenerateImageAsync(string prompt, Action<Texture2D> callback)
        {
            if (!isConnected)
            {
                LongEventHandler.QueueLongEvent(() =>
                {
                    Messages.Message("RAV_Player2_NoApp".Translate(), MessageTypeDefOf.RejectInput);
                    callback?.Invoke(null);
                }, "RimAiVinci_AIError", false, null);
                return;
            }

            string json = BuildGenerateJson(prompt);
            SendRequest("/v1/image/generate", json, callback);
        }

        public static void EditImageAsync(string prompt, Texture2D referenceImage, Action<Texture2D> callback)
        {
            if (!isConnected)
            {
                LongEventHandler.QueueLongEvent(() =>
                {
                    Messages.Message("RAV_Player2_NoApp".Translate(), MessageTypeDefOf.RejectInput);
                    callback?.Invoke(null);
                }, "RimAiVinci_AIError", false, null);
                return;
            }

            if (referenceImage == null) { callback?.Invoke(null); return; }
            byte[] imageBytes = referenceImage.EncodeToPNG();
            if (imageBytes == null || imageBytes.Length == 0) { callback?.Invoke(null); return; }
            string base64Image = Convert.ToBase64String(imageBytes);
            string imageDataUrl = $"data:image/png;base64,{base64Image}";

            string json = BuildEditJson(prompt, imageDataUrl);
            Log.Message("[RimAiVinci] Player2: 发起图生图请求...");
            SendRequest("/v1/image/edit", json, callback);
        }

        private static string BuildGenerateJson(string prompt)
        {
            string safePrompt = prompt.Replace("\"", "\\\"").Replace("\n", " ");
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"prompt\": \"{safePrompt}\",");
            sb.Append($"\"width\": 1024,");
            sb.Append($"\"height\": 1024");
            sb.Append("}");
            return sb.ToString();
        }

        private static string BuildEditJson(string prompt, string base64Image)
        {
            string safePrompt = prompt.Replace("\"", "\\\"").Replace("\n", " ");
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"prompt\": \"{safePrompt}\",");
            sb.Append($"\"image\": \"{base64Image}\"");
            sb.Append("}");
            return sb.ToString();
        }

        private static void SendRequest(string endpoint, string jsonBody, Action<Texture2D> callback)
        {
            string url = $"http://127.0.0.1:{apiPort}{endpoint}";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            request.Headers.Add("player2-game-key", p2Key);

            Task.Run(async () =>
            {
                try
                {
                    HttpResponseMessage response = await client.SendAsync(request);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        string base64Image = ExtractBase64FromJson(responseBody, "image");
                        if (!string.IsNullOrEmpty(base64Image))
                        {
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                try
                                {
                                    byte[] imageBytes = Convert.FromBase64String(base64Image);
                                    if (imageBytes != null && imageBytes.Length > 0)
                                    {
                                        Texture2D tex = new Texture2D(2, 2);
                                        tex.LoadImage(imageBytes);
                                        tex.name = "P2_Gen_" + DateTime.Now.Ticks;
                                        callback?.Invoke(tex);
                                    }
                                    else
                                    {
                                        Log.Error("[RimAiVinci] Player2: base64 解码后图片数据为空。");
                                        callback?.Invoke(null);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error($"[RimAiVinci] Player2: base64 解码失败: {ex.Message}\n原始数据前100字符: {base64Image.Substring(0, Math.Min(100, base64Image.Length))}");
                                    callback?.Invoke(null);
                                }
                            }, "RimAiVinci_Player2Processing", false, null);
                        }
                        else
                        {
                            LongEventHandler.QueueLongEvent(() =>
                            {
                                Log.Error($"[RimAiVinci] Player2: 响应中未找到 image 字段: {responseBody.Substring(0, Math.Min(200, responseBody.Length))}");
                                callback?.Invoke(null);
                            }, "RimAiVinci_AIError", false, null);
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Messages.Message("RAV_Player2_Unauthorized".Translate(), MessageTypeDefOf.RejectInput);
                            isConnected = false;
                            statusMessage = "RAV_Player2_Disconnected".Translate();
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Messages.Message("RAV_Player2_NoCredits".Translate(), MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                    }
                    else if (response.StatusCode == (System.Net.HttpStatusCode)429)
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Messages.Message("RAV_Player2_RateLimit".Translate(), MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                    }
                    else
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Log.Error($"[RimAiVinci] Player2: 请求失败 {response.StatusCode}\n{responseBody}");
                            Messages.Message($"Player2 Error: {response.StatusCode}", MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                    }
                }
                catch (Exception ex)
                {
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        Log.Error($"[RimAiVinci] Player2: 网络异常: {ex.Message}");
                        Messages.Message("RAV_Player2_NoApp".Translate(), MessageTypeDefOf.RejectInput);
                        callback?.Invoke(null);
                    }, "RimAiVinci_AIError", false, null);
                }
            });
        }

        private static string ExtractBase64FromJson(string json, string key)
        {
            try
            {
                string searchKey = $"\"{key}\"";
                int start = json.IndexOf(searchKey);
                if (start == -1) return null;
                start += searchKey.Length;
                while (start < json.Length && json[start] != '"') start++;
                if (start >= json.Length) return null;
                start++;
                int end = FindClosingQuote(json, start);
                if (end == -1) return null;

                string rawValue = json.Substring(start, end - start);

                string cleaned = UnescapeJsonString(rawValue);

                int dataUriIdx = cleaned.IndexOf(";base64,");
                if (dataUriIdx != -1)
                {
                    cleaned = cleaned.Substring(dataUriIdx + ";base64,".Length);
                }

                StringBuilder sb = new StringBuilder(cleaned.Length);
                foreach (char c in cleaned)
                {
                    if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                        (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=')
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }
            catch { return null; }
        }

        private static int FindClosingQuote(string json, int start)
        {
            int i = start;
            while (i < json.Length)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    i += 2;
                    continue;
                }
                if (json[i] == '"') return i;
                i++;
            }
            return -1;
        }

        private static string UnescapeJsonString(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    if (next == 'n') { sb.Append('\n'); i += 2; }
                    else if (next == 'r') { sb.Append('\r'); i += 2; }
                    else if (next == 't') { sb.Append('\t'); i += 2; }
                    else if (next == '"') { sb.Append('"'); i += 2; }
                    else if (next == '\\') { sb.Append('\\'); i += 2; }
                    else if (next == '/') { sb.Append('/'); i += 2; }
                    else { sb.Append(s[i]); i++; }
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static string ExtractSimpleJsonValue(string json, string key)
        {
            try
            {
                string searchKey = $"\"{key}\"";
                int start = json.IndexOf(searchKey);
                if (start == -1) return null;
                start += searchKey.Length;
                while (start < json.Length && json[start] != '"') start++;
                if (start >= json.Length) return null;
                start++;
                int end = FindClosingQuote(json, start);
                if (end == -1) return null;
                return UnescapeJsonString(json.Substring(start, end - start));
            }
            catch { return null; }
        }
    }
}
