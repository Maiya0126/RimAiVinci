using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;
using System.IO;

namespace RimAiVinci
{
    public static class ComfyUIClient
    {
        private static readonly HttpClient client = new HttpClient();

        static ComfyUIClient()
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        }

        public static void GenerateImageAsync(string prompt, Action<Texture2D> callback)
        {
            string url = GetBaseUrl();
            string ckptName = GetModelName();
            int seed = UnityEngine.Random.Range(0, 99999999);

            string workflow;
            if (RimAiVinciMod.settings.comfyWorkflowMode == 1)
                workflow = BuildFluxTxt2ImgWorkflow(prompt, ckptName, seed);
            else
                workflow = BuildTxt2ImgWorkflow(prompt, ckptName, seed);

            string endpoint = url + "/prompt";
            SubmitWorkflow(endpoint, workflow, url, callback);
        }

        public static void GenerateImageToImageAsync(string prompt, Texture2D referenceImage, float strength, Action<Texture2D> callback)
        {
            if (referenceImage == null) { callback?.Invoke(null); return; }

            string url = GetBaseUrl();
            string ckptName = GetModelName();
            int seed = UnityEngine.Random.Range(0, 99999999);

            byte[] pngBytes = referenceImage.EncodeToPNG();
            string uploadEndpoint = url + "/upload/image";

            Task.Run(async () =>
            {
                try
                {
                    string uploadedFilename = await UploadImageAsync(uploadEndpoint, pngBytes, "rimaivinci_input.png");
                    if (string.IsNullOrEmpty(uploadedFilename))
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Messages.Message("RAV_ComfyUI_UploadFail".Translate(), MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                        return;
                    }

                    string workflow;
                    if (RimAiVinciMod.settings.comfyWorkflowMode == 1)
                        workflow = BuildFluxImg2ImgWorkflow(prompt, ckptName, seed, strength, uploadedFilename);
                    else
                        workflow = BuildImg2ImgWorkflow(prompt, ckptName, seed, strength, uploadedFilename);

                    string promptEndpoint = url + "/prompt";
                    SubmitWorkflow(promptEndpoint, workflow, url, callback);
                }
                catch (Exception ex)
                {
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        Log.Error("[RimAiVinci] ComfyUI upload error: " + ex.Message);
                        Messages.Message("RAV_ComfyUI_NetError".Translate(), MessageTypeDefOf.RejectInput);
                        callback?.Invoke(null);
                    }, "RimAiVinci_AIError", false, null);
                }
            });
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
                    string endpoint = url.TrimEnd('/') + "/system_stats";
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

        private static void SubmitWorkflow(string endpoint, string workflowJson, string baseUrl, Action<Texture2D> callback)
        {
            Task.Run(async () =>
            {
                try
                {
                    string wrappedJson = "{\"prompt\":" + workflowJson + "}";
                    var content = new StringContent(wrappedJson, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(endpoint, content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Log.Error("[RimAiVinci] ComfyUI submit failed: " + response.StatusCode + "\n" + responseBody);
                            Messages.Message("RAV_ComfyUI_SubmitFail".Translate(((int)response.StatusCode).ToString()), MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                        return;
                    }

                    string promptId = ExtractJsonValue(responseBody, "prompt_id");
                    if (string.IsNullOrEmpty(promptId))
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            Log.Error("[RimAiVinci] ComfyUI: no prompt_id: " + responseBody);
                            callback?.Invoke(null);
                        }, "RimAiVinci_AIError", false, null);
                        return;
                    }

                    Log.Message("[RimAiVinci] ComfyUI submitted, prompt_id=" + promptId);

                    byte[] imageBytes = await PollForResultAsync(baseUrl, promptId);

                    LongEventHandler.QueueLongEvent(() =>
                    {
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            Texture2D tex = new Texture2D(2, 2);
                            tex.LoadImage(imageBytes);
                            tex.name = "AI_ComfyUI_" + DateTime.Now.Ticks;
                            callback?.Invoke(tex);
                        }
                        else
                        {
                            Log.Error("[RimAiVinci] ComfyUI: timeout or no output");
                            Messages.Message("RAV_ComfyUI_Timeout".Translate(), MessageTypeDefOf.RejectInput);
                            callback?.Invoke(null);
                        }
                    }, "RimAiVinci_ProcessingAIImage", false, null);
                }
                catch (Exception ex)
                {
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        Log.Error("[RimAiVinci] ComfyUI error: " + ex.Message);
                        Messages.Message("RAV_ComfyUI_NetError".Translate(), MessageTypeDefOf.RejectInput);
                        callback?.Invoke(null);
                    }, "RimAiVinci_AIError", false, null);
                }
            });
        }

        private static async Task<byte[]> PollForResultAsync(string baseUrl, string promptId, int maxWaitSeconds = 180)
        {
            string historyUrl = baseUrl + "/history/" + promptId;
            DateTime startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalSeconds < maxWaitSeconds)
            {
                await Task.Delay(2000);
                try
                {
                    HttpResponseMessage histResp = await client.GetAsync(historyUrl);
                    if (!histResp.IsSuccessStatusCode) continue;

                    string histBody = await histResp.Content.ReadAsStringAsync();
                    if (!histBody.Contains(promptId)) continue;

                    string outputsKey = "\"outputs\":";
                    int outputsIdx = histBody.IndexOf(outputsKey);
                    if (outputsIdx == -1) continue;

                    string outputsSection = histBody.Substring(outputsIdx);
                    string filename = ExtractJsonValue(outputsSection, "filename");
                    string subfolder = ExtractJsonValue(outputsSection, "subfolder");
                    string folderType = ExtractJsonValue(outputsSection, "type");
                    if (string.IsNullOrEmpty(folderType)) folderType = "output";

                    if (!string.IsNullOrEmpty(filename))
                    {
                        string viewUrl = baseUrl + "/view?filename=" + Uri.EscapeDataString(filename) + "&subfolder=" + Uri.EscapeDataString(subfolder ?? "") + "&type=" + Uri.EscapeDataString(folderType);
                        byte[] imageData = await client.GetByteArrayAsync(viewUrl);
                        return imageData;
                    }
                }
                catch { continue; }
            }
            return null;
        }

        private static async Task<string> UploadImageAsync(string uploadUrl, byte[] imageData, string filename)
        {
            try
            {
                var boundary = "----RimAiVinciBoundary" + DateTime.Now.Ticks;
                var formContent = new MultipartFormDataContent(boundary);
                formContent.Add(new ByteArrayContent(imageData), "image", filename);
                formContent.Add(new StringContent("input"), "type");

                HttpResponseMessage response = await client.PostAsync(uploadUrl, formContent);
                if (!response.IsSuccessStatusCode) return null;

                string body = await response.Content.ReadAsStringAsync();
                return ExtractJsonValue(body, "name");
            }
            catch { return null; }
        }

        #region Standard Workflow (SD1.5 / SDXL)

        private static string BuildTxt2ImgWorkflow(string prompt, string ckptName, int seed)
        {
            string safePrompt = Escape(prompt);
            string safeCkpt = Escape(ckptName);
            if (string.IsNullOrEmpty(safeCkpt)) safeCkpt = "v1-5-pruned-emaonly.safetensors";

            return "{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"cfg\":7,\"denoise\":1,\"latent_image\":[\"5\",0],\"model\":[\"4\",0],\"negative\":[\"7\",0],\"positive\":[\"6\",0],\"sampler_name\":\"euler_ancestral\",\"scheduler\":\"normal\",\"seed\":" + seed + ",\"steps\":25}},\"4\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"" + safeCkpt + "\"}},\"5\":{\"class_type\":\"EmptyLatentImage\",\"inputs\":{\"batch_size\":1,\"height\":1024,\"width\":1024}},\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"4\",1],\"text\":\"" + safePrompt + "\"}},\"7\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"4\",1],\"text\":\"bad quality, blurry, deformed, ugly, worst quality\"}},\"8\":{\"class_type\":\"VAEDecode\",\"inputs\":{\"samples\":[\"3\",0],\"vae\":[\"4\",2]}},\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"filename_prefix\":\"RimAiVinci\",\"images\":[\"8\",0]}}}";
        }

        private static string BuildImg2ImgWorkflow(string prompt, string ckptName, int seed, float strength, string uploadedFilename)
        {
            string safePrompt = Escape(prompt);
            string safeCkpt = Escape(ckptName);
            if (string.IsNullOrEmpty(safeCkpt)) safeCkpt = "v1-5-pruned-emaonly.safetensors";

            return "{\"3\":{\"class_type\":\"KSampler\",\"inputs\":{\"cfg\":7,\"denoise\":" + strength.ToString("F2") + ",\"latent_image\":[\"11\",0],\"model\":[\"4\",0],\"negative\":[\"7\",0],\"positive\":[\"6\",0],\"sampler_name\":\"euler_ancestral\",\"scheduler\":\"normal\",\"seed\":" + seed + ",\"steps\":25}},\"4\":{\"class_type\":\"CheckpointLoaderSimple\",\"inputs\":{\"ckpt_name\":\"" + safeCkpt + "\"}},\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"4\",1],\"text\":\"" + safePrompt + "\"}},\"7\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"4\",1],\"text\":\"bad quality, blurry, deformed, ugly, worst quality\"}},\"8\":{\"class_type\":\"VAEDecode\",\"inputs\":{\"samples\":[\"3\",0],\"vae\":[\"4\",2]}},\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"filename_prefix\":\"RimAiVinci\",\"images\":[\"8\",0]}},\"10\":{\"class_type\":\"LoadImage\",\"inputs\":{\"image\":\"" + Escape(uploadedFilename) + "\"}},\"11\":{\"class_type\":\"VAEEncode\",\"inputs\":{\"pixels\":[\"10\",0],\"vae\":[\"4\",2]}}}";
        }

        #endregion

        #region Flux Workflow

        private static string BuildFluxTxt2ImgWorkflow(string prompt, string unetName, int seed)
        {
            string safePrompt = Escape(prompt);
            string safeUnet = Escape(unetName);
            if (string.IsNullOrEmpty(safeUnet)) safeUnet = "flux1-dev.safetensors";

            string clipName1 = "t5xxl_fp16.safetensors";
            string clipName2 = "clip_l.safetensors";
            string clipPair = GetFluxClipPair();
            if (!string.IsNullOrEmpty(clipPair))
            {
                string[] parts = clipPair.Split(',');
                if (parts.Length >= 2) { clipName1 = parts[0].Trim(); clipName2 = parts[1].Trim(); }
                else if (parts.Length == 1) { clipName1 = parts[0].Trim(); }
            }

            string vaeName = GetFluxVaeName();
            if (string.IsNullOrEmpty(vaeName)) vaeName = "ae.safetensors";

            return "{\"10\":{\"class_type\":\"KSampler\",\"inputs\":{\"cfg\":3.5,\"denoise\":1,\"latent_image\":[\"5\",0],\"model\":[\"11\",0],\"negative\":[\"21\",0],\"positive\":[\"6\",0],\"sampler_name\":\"euler\",\"scheduler\":\"simple\",\"seed\":" + seed + ",\"steps\":20}},\"11\":{\"class_type\":\"UNETLoader\",\"inputs\":{\"unet_name\":\"" + Escape(safeUnet) + "\",\"weight_dtype\":\"default\"}},\"5\":{\"class_type\":\"EmptyLatentImage\",\"inputs\":{\"batch_size\":1,\"height\":1024,\"width\":1024}},\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"12\",0],\"text\":\"" + safePrompt + "\"}},\"12\":{\"class_type\":\"DualCLIPLoader\",\"inputs\":{\"clip_name1\":\"" + Escape(clipName1) + "\",\"clip_name2\":\"" + Escape(clipName2) + "\",\"type\":\"flux\"}},\"8\":{\"class_type\":\"VAEDecode\",\"inputs\":{\"samples\":[\"10\",0],\"vae\":[\"13\",0]}},\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"filename_prefix\":\"RimAiVinci\",\"images\":[\"8\",0]}},\"13\":{\"class_type\":\"VAELoader\",\"inputs\":{\"vae_name\":\"" + Escape(vaeName) + "\"}},\"21\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"12\",0],\"text\":\"bad quality, blurry, deformed, ugly\"}}}";
        }

        private static string BuildFluxImg2ImgWorkflow(string prompt, string unetName, int seed, float strength, string uploadedFilename)
        {
            string safePrompt = Escape(prompt);
            string safeUnet = Escape(unetName);
            if (string.IsNullOrEmpty(safeUnet)) safeUnet = "flux1-dev.safetensors";

            string clipName1 = "t5xxl_fp16.safetensors";
            string clipName2 = "clip_l.safetensors";
            string clipPair = GetFluxClipPair();
            if (!string.IsNullOrEmpty(clipPair))
            {
                string[] parts = clipPair.Split(',');
                if (parts.Length >= 2) { clipName1 = parts[0].Trim(); clipName2 = parts[1].Trim(); }
                else if (parts.Length == 1) { clipName1 = parts[0].Trim(); }
            }

            string vaeName = GetFluxVaeName();
            if (string.IsNullOrEmpty(vaeName)) vaeName = "ae.safetensors";

            return "{\"10\":{\"class_type\":\"KSampler\",\"inputs\":{\"cfg\":3.5,\"denoise\":" + strength.ToString("F2") + ",\"latent_image\":[\"20\",0],\"model\":[\"11\",0],\"negative\":[\"21\",0],\"positive\":[\"6\",0],\"sampler_name\":\"euler\",\"scheduler\":\"simple\",\"seed\":" + seed + ",\"steps\":20}},\"11\":{\"class_type\":\"UNETLoader\",\"inputs\":{\"unet_name\":\"" + Escape(safeUnet) + "\",\"weight_dtype\":\"default\"}},\"6\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"12\",0],\"text\":\"" + safePrompt + "\"}},\"12\":{\"class_type\":\"DualCLIPLoader\",\"inputs\":{\"clip_name1\":\"" + Escape(clipName1) + "\",\"clip_name2\":\"" + Escape(clipName2) + "\",\"type\":\"flux\"}},\"8\":{\"class_type\":\"VAEDecode\",\"inputs\":{\"samples\":[\"10\",0],\"vae\":[\"13\",0]}},\"9\":{\"class_type\":\"SaveImage\",\"inputs\":{\"filename_prefix\":\"RimAiVinci\",\"images\":[\"8\",0]}},\"13\":{\"class_type\":\"VAELoader\",\"inputs\":{\"vae_name\":\"" + Escape(vaeName) + "\"}},\"21\":{\"class_type\":\"CLIPTextEncode\",\"inputs\":{\"clip\":[\"12\",0],\"text\":\"bad quality, blurry, deformed, ugly\"}},\"14\":{\"class_type\":\"LoadImage\",\"inputs\":{\"image\":\"" + Escape(uploadedFilename) + "\"}},\"20\":{\"class_type\":\"VAEEncode\",\"inputs\":{\"pixels\":[\"14\",0],\"vae\":[\"13\",0]}}}";
        }

        #endregion

        public static void FetchModelLists(Action<List<string>, List<string>, List<string>, List<string>, string> callback)
        {
            string url = GetBaseUrl();
            Task.Run(async () =>
            {
                List<string> ckpts = null, unets = null, clips = null, vaes = null;
                string error = null;
                try
                {
                    ckpts = await FetchNodeList(url, "CheckpointLoaderSimple", "ckpt_name");
                    unets = await FetchNodeList(url, "UNETLoader", "unet_name");
                    clips = await FetchNodeList(url, "DualCLIPLoader", "clip_name1");
                    vaes = await FetchNodeList(url, "VAELoader", "vae_name");
                }
                catch (Exception ex) { error = ex.Message; }
                LongEventHandler.QueueLongEvent(() => callback?.Invoke(ckpts, unets, clips, vaes, error), "RimAiVinci_FetchModels", false, null);
            });
        }

        private static async Task<List<string>> FetchNodeList(string url, string nodeClass, string inputName)
        {
            HttpResponseMessage resp = await client.GetAsync(url + "/object_info/" + nodeClass);
            if (!resp.IsSuccessStatusCode) return null;
            string body = await resp.Content.ReadAsStringAsync();
            return ExtractStringList(body, inputName);
        }

        private static List<string> ExtractStringList(string json, string inputName)
        {
            try
            {
                string marker = "\"" + inputName + "\":[";
                int idx = json.IndexOf(marker);
                if (idx == -1) return null;
                int pos = idx + marker.Length;
                if (pos < json.Length && json[pos] == '[') pos++;
                List<string> result = new List<string>();
                while (pos < json.Length)
                {
                    char c = json[pos];
                    if (c == ']') break;
                    if (c == '"')
                    {
                        int end = json.IndexOf('"', pos + 1);
                        if (end == -1) break;
                        result.Add(json.Substring(pos + 1, end - pos - 1));
                        pos = end + 1;
                    }
                    else pos++;
                }
                return result;
            }
            catch { return null; }
        }

        private static string GetBaseUrl()
        {
            string url = RimAiVinciMod.settings.apiUrl;
            if (string.IsNullOrEmpty(url)) url = "http://127.0.0.1:8188";
            return url.TrimEnd('/');
        }

        private static string GetModelName()
        {
            string model = RimAiVinciMod.settings.modelName;
            return string.IsNullOrEmpty(model) ? "" : model;
        }

        private static string GetFluxClipPair()
        {
            string val = RimAiVinciMod.settings.comfyFluxClipName;
            return string.IsNullOrEmpty(val) ? "" : val;
        }

        private static string GetFluxVaeName()
        {
            string val = RimAiVinciMod.settings.comfyFluxVaeName;
            return string.IsNullOrEmpty(val) ? "" : val;
        }

        private static string ExtractJsonValue(string json, string key)
        {
            try
            {
                string searchKey = "\"" + key + "\":\"";
                int start = json.IndexOf(searchKey);
                if (start == -1)
                {
                    searchKey = "\"" + key + "\": ";
                    start = json.IndexOf(searchKey);
                    if (start == -1) return null;
                    start += searchKey.Length;
                    int end = json.IndexOfAny(new[] { ',', '}', ']' }, start);
                    if (end == -1) return null;
                    return json.Substring(start, end - start).Trim('"');
                }
                start += searchKey.Length;
                int quoteEnd = json.IndexOf("\"", start);
                if (quoteEnd == -1) return null;
                return json.Substring(start, quoteEnd - start);
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
