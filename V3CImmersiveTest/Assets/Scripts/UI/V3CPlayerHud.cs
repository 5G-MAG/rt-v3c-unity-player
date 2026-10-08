/*
 * License: 5G-MAG Public License (v1.0)
 * Author: Jordi J. Gimenez (5G-MAG)
 * Copyright: (C) 2026 5G-MAG Association
 *
 * For full license terms please see the LICENSE file distributed with this
 * program. If this file is missing then the license can be retrieved from
 * https://www.5g-mag.com/license.
 */

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using IDCC.V3CDecoder;

namespace IDCC.V3CDecoder.Demo
{
    // Player heads-up display in the 5G-MAGflix style (dark surfaces, one blue accent, pill buttons, Ubuntu).
    // Built at runtime so the scene file stays untouched: it shows what is playing and where it comes from,
    // a loading state, a start hint, on-screen errors that stay until dismissed, and labelled controls.
    public class V3CPlayerHud : MonoBehaviour
    {
        public static V3CPlayerHud Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null || FindFirstObjectByType<DemoSceneController>() == null)
            {
                return;
            }
            new GameObject("V3CPlayerHud").AddComponent<V3CPlayerHud>();
        }

        // ---- palette (rt-dvb-i-android-application res/values/colors.xml) ----
        private static readonly Color Surface = new Color32(0x12, 0x12, 0x12, 0xFF);
        private static readonly Color Card = new Color32(0x1E, 0x1E, 0x1E, 0xF5);
        private static readonly Color Accent = new Color32(0x00, 0xA0, 0xD2, 0xFF);
        private static readonly Color Secondary = new Color32(0xB3, 0xB3, 0xB3, 0xFF);
        private static readonly Color Divider = new Color32(0x2C, 0x2C, 0x2C, 0xFF);
        private static readonly Color Warn = new Color32(0xF2, 0xA3, 0x3A, 0xFF);
        private static readonly Color Good = new Color32(0x4C, 0xC3, 0x8A, 0xFF);

        // ---- library.json / config.json, as the native plugin reads them ----
        [Serializable] private class LibStream { public string Type; public string ServerName; public float[] Duration; }
        [Serializable] private class LibItem { public string Mode; public string Name; public string Title; public string Description; public string Environment; public LibStream[] StreamList; }
        [Serializable] private class Lib { public LibItem[] Playlist; }
        [Serializable] private class CfgServer { public string Name; public string IP; public int Port; }
        [Serializable] private class CfgNet { public CfgServer[] Servers; }
        [Serializable] private class Cfg { public CfgNet Networking; public string Library; }

        private class MediaInfo
        {
            public string Title;
            public string Format;   // "V-PCC", "MIV", "Video"
            public bool Streamed;
            public string Origin;   // "127.0.0.1:8080" when streamed
            public float Seconds;
            public string Raw;      // name as the plugin reports it
            public string Description;
            public string Environment;  // optional backdrop named in library.json ("stadium")
        }

        private V3CDecoderManager m_manager;
        private DemoSceneController m_scene;
        private MediaInfo[] m_media = new MediaInfo[0];
        private bool m_mediaLoaded;

        private Font m_regular, m_medium, m_bold;
        private Sprite m_round, m_gradDown, m_gradUp, m_ring;
        private Sprite m_iPlay, m_iPause, m_iPrev, m_iNext, m_iReset;

        private CanvasGroup m_controls, m_startCard, m_loading, m_errorCard, m_toast;
        private Text m_title, m_subtitle, m_counter, m_playLabel, m_startTitle, m_startInfo, m_loadText, m_loadSub;
        private Text m_errTitle, m_errMsg, m_errDetail, m_toastText, m_originText;
        private Image m_playIcon, m_formatBadge, m_sourceBadge;
        private Text m_formatText, m_sourceText;
        private RectTransform m_spinner;
        private CanvasGroup m_library, m_settings, m_stats;
        private RectTransform m_libContent;
        private Text m_statsText, m_testStatus;
        private InputField m_ipField, m_portField;
        private Image m_statsToggleBg;
        private RectTransform m_statsKnob;
        private Sprite m_gridIcon;
        private float m_fps = 60f;
        private Canvas m_canvas;
        private RectTransform m_localBtn;
        private int m_localIdx = -1;
        private readonly System.Collections.Generic.Dictionary<string, Texture2D> m_thumbs = new System.Collections.Generic.Dictionary<string, Texture2D>();
        private int m_envFor = -2;
        private bool m_statsOn;
        private System.Threading.Tasks.Task<string> m_testTask;

        private bool m_wasStarted, m_isLoading;
        private float m_lastTouch;
        private float m_toastUntil;
        private volatile bool m_errPending;
        private uint m_errLevel, m_errId;
        private bool m_gestureHintShown;
        private bool m_openedAtStart;

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            m_manager = FindFirstObjectByType<V3CDecoderManager>();
            m_scene = FindFirstObjectByType<DemoSceneController>();
            LoadAssets();
            HideLegacyRail();
            BuildUi();
            if (m_manager != null)
            {
                m_manager.onErrorEvent += OnError;
                m_manager.OnMediaReady += OnMediaReady;
                m_manager.OnMediaRequest += OnMediaRequest;
            }
            m_lastTouch = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            if (m_manager != null)
            {
                m_manager.onErrorEvent -= OnError;
                m_manager.OnMediaReady -= OnMediaReady;
                m_manager.OnMediaRequest -= OnMediaRequest;
            }
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ events

        private void OnError(uint level, uint id)
        {
            // may arrive from a native thread: store, handle in Update
            m_errLevel = level;
            m_errId = id;
            m_errPending = true;
        }

        private void OnMediaReady(V3CDecoderManager.V3CRenderData data)
        {
            m_isLoading = false;
            StartCoroutine(CaptureThumb(Current()));
            m_lastTouch = Time.unscaledTime;   // show the controls for a moment once the picture is there
            if (!m_gestureHintShown && PlayerPrefs.GetInt("hud.gestureHint", 0) == 0)
            {
                m_gestureHintShown = true;
                PlayerPrefs.SetInt("hud.gestureHint", 1);
                PlayerPrefs.Save();
                ShowToast("Drag to orbit, pinch to zoom, tap for controls", Accent, 5f);
            }
        }

        private void OnMediaRequest()
        {
            m_isLoading = true;
            HideError();
        }

        // ------------------------------------------------------------------ data

        private void EnsureMedia()
        {
            if (m_mediaLoaded || m_manager == null || !m_manager.m_isInit)
            {
                return;
            }
            m_mediaLoaded = true;

            Lib lib = null;
            Cfg cfg = null;
            try
            {
                string cfgPath = Path.Combine(Application.persistentDataPath, m_manager.m_configFile);
                if (File.Exists(cfgPath))
                {
                    cfg = JsonUtility.FromJson<Cfg>(File.ReadAllText(cfgPath));
                    string libRel = cfg != null && !string.IsNullOrEmpty(cfg.Library) ? cfg.Library : "data/library.json";
                    string libPath = Path.Combine(Application.persistentDataPath, libRel);
                    if (File.Exists(libPath))
                    {
                        lib = JsonUtility.FromJson<Lib>(File.ReadAllText(libPath));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"HUD: could not read library.json/config.json: {e.Message}");
            }

            int n = (int)DecoderPluginInterface.GetNumberOfMedia();
            m_media = new MediaInfo[n];
            for (int i = 0; i < n; i++)
            {
                var sb = new StringBuilder(1024);
                DecoderPluginInterface.GetMediaName((uint)i, sb);
                string raw = sb.ToString();
                var info = new MediaInfo { Title = PrettyTitle(raw), Raw = raw, Format = "", Streamed = false, Origin = "", Description = "", Environment = "" };

                LibItem item = null;
                if (lib != null && lib.Playlist != null)
                {
                    foreach (var p in lib.Playlist) { if (p.Name == raw) { item = p; break; } }
                    if (item == null && i < lib.Playlist.Length) item = lib.Playlist[i];
                }
                if (item != null)
                {
                    if (!string.IsNullOrEmpty(item.Title)) info.Title = item.Title;
                    info.Description = item.Description ?? "";
                    info.Environment = item.Environment ?? "";
                    info.Streamed = string.Equals(item.Mode, "dash", StringComparison.OrdinalIgnoreCase);
                    if (item.StreamList != null)
                    {
                        foreach (var s in item.StreamList)
                        {
                            if (string.Equals(s.Type, "haptic", StringComparison.OrdinalIgnoreCase)) continue;
                            info.Format = FormatName(s.Type);
                            if (s.Duration != null) { foreach (float d in s.Duration) info.Seconds += d; }
                            if (info.Streamed && cfg != null && cfg.Networking != null && cfg.Networking.Servers != null)
                            {
                                foreach (var sv in cfg.Networking.Servers)
                                {
                                    if (sv.Name == s.ServerName) { info.Origin = $"{sv.IP}:{sv.Port}"; break; }
                                }
                            }
                            break;
                        }
                    }
                }
                m_media[i] = info;
            }
            // streamed entries carry no duration in library.json: take it from the same title when it is on the device
            foreach (var a in m_media)
            {
                if (a == null || a.Seconds > 0) continue;
                foreach (var b in m_media) { if (b != null && b != a && TitleKey(b.Raw) == TitleKey(a.Raw) && b.Seconds > 0) { a.Seconds = b.Seconds; break; } }
            }
        }

        // the streamed copy of a title is listed as "<name> Remote": both copies share one key
        private static string TitleKey(string raw)
        {
            return Regex.Replace(raw ?? "", "\\s+Remote$", "");
        }

        private static string FormatName(string t)
        {
            switch ((t ?? "").ToLowerInvariant())
            {
                case "vpcc": return "V-PCC";
                case "miv": return "MIV";
                case "2d": return "2D video";
                default: return string.IsNullOrEmpty(t) ? "" : t;
            }
        }

        // "S41C2RAR05_footprod_1GOP_R24" -> "S41C2RAR05 footprod"; the raw name stays in the log.
        private static string PrettyTitle(string raw)
        {
            string t = Regex.Replace(raw ?? "", @"_\d+GOP.*$", "");
            t = t.Replace('_', ' ').Trim();
            return t.Length == 0 ? raw : t;
        }

        // The backdrop a title names in library.json ("Environment"), empty when it names none.
        // The HUD only reports it; a component such as StadiumBackdrop applies it.
        public static string CurrentEnvironment { get; private set; } = "";
        public static event Action<string> EnvironmentChanged;

        private void ApplyEnvironment()
        {
            int idx = m_scene != null ? m_scene.MediaIndex : -1;
            if (idx == m_envFor || m_media.Length == 0) return;
            m_envFor = idx;
            var info = Current();
            CurrentEnvironment = info != null && info.Environment != null ? info.Environment : "";
            EnvironmentChanged?.Invoke(CurrentEnvironment);
        }

        private MediaInfo Current()
        {
            int i = m_scene != null ? m_scene.MediaIndex : 0;
            return (i >= 0 && i < m_media.Length) ? m_media[i] : null;
        }

        // ------------------------------------------------------------------ errors

        private static string PlainMessage(uint id, out string hint)
        {
            hint = "";
            switch (id)
            {
                case 1: return "The stream could not be initialised.";
                case 2: return "The first segment of the stream is empty.";
                case 3:
                    hint = "Check that the origin is running and the address in the settings is right.";
                    return "The player could not reach the origin to download the manifest.";
                case 4: return "The manifest was downloaded but could not be read.";
                case 5:
                    hint = "Check the file path on the origin.";
                    return "The origin has no manifest at this address.";
                case 6:
                    hint = "Check that the origin is running and reachable from this device.";
                    return "The connection to the origin failed.";
                case 7: return "The origin answered with an HTTP error.";
                case 8: return "The streaming client reported an error.";
                case 9: return "The segments could not be unpacked.";
                case 10: return "The haptics file could not be read.";
                case 11: return "This title has no haptics file; playback goes on without it.";
                case 12: return "The haptics file could not be downloaded.";
                case 13: return "The haptics configuration file is missing.";
                case 14: return "This title has no haptics.";
                case 15: return "The initialisation segment is missing on the origin.";
                case 16: return "A media segment is missing on the origin.";
                case 17: return "A media segment could not be downloaded.";
                case 101: return "The stream file is not valid.";
                case 201:
                    hint = "The Android audio and video libraries are not part of this build.";
                    return "The decoder libraries (avcodec) were not found.";
                default: return "The player reported an unknown problem.";
            }
        }

        private void HandleError()
        {
            m_errPending = false;
            uint level = m_errLevel, id = m_errId;
            if (id == 0 || id == 100 || id == 200) return;
            string hint;
            string msg = PlainMessage(id, out hint);
            Debug.Log($"HUD error level={level} id={id}");

            if (level < 2)
            {
                ShowToast(msg, Warn, 4f);
                return;
            }

            m_isLoading = false;
            var info = Current();
            m_errTitle.text = info != null ? $"Can't play {info.Title}" : "Can't play this title";
            m_errMsg.text = msg + (hint.Length > 0 ? "\n" + hint : "");
            string where = (info != null && info.Streamed && info.Origin.Length > 0) ? $"Origin {info.Origin}   " : "";
            m_errDetail.text = $"{where}Code {id}";
            m_localIdx = -1;
            if (info != null && info.Streamed && m_scene != null)
            {
                for (int j = 0; j < m_media.Length; j++)
                {
                    if (j != m_scene.MediaIndex && m_media[j] != null && !m_media[j].Streamed && TitleKey(m_media[j].Raw) == TitleKey(info.Raw)) { m_localIdx = j; break; }
                }
            }
            m_localBtn.gameObject.SetActive(m_localIdx >= 0);
            SetGroup(m_errorCard, true);
        }

        private void HideError()
        {
            SetGroup(m_errorCard, false);
        }

        private void PlayLocalCopy()
        {
            HideError();
            if (m_localIdx >= 0) PlayIndex(m_localIdx);
        }

        private void Retry()
        {
            HideError();
            if (m_manager == null || m_scene == null) return;
            int idx = m_scene.MediaIndex;
            m_isLoading = true;
            if (!m_manager.m_isStarted)
            {
                m_manager.StartPlugin(idx);
            }
            else
            {
                // same id as the current one is ignored by ChangeMedia, so restart the plugin on it
                m_manager.Stop();
                m_manager.StartPlugin(idx);
            }
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            if (m_manager == null) return;
            EnsureMedia();
            ApplyEnvironment();

            // the app opens on the library once the titles are known; Back leads to the start card
            if (!m_openedAtStart && m_media.Length > 0)
            {
                m_openedAtStart = true;
                OpenLibrary();
            }

            if (m_errPending) HandleError();

            bool started = m_manager.m_isStarted;
            if (started && !m_wasStarted) { m_isLoading = true; HideError(); }
            if (!started && m_wasStarted) { m_isLoading = false; }
            m_wasStarted = started;

            if (Input.touchCount > 0 || Input.GetMouseButton(0) || Input.GetMouseButtonDown(0))
            {
                m_lastTouch = Time.unscaledTime;
            }

            var info = Current();
            bool paused = m_manager.m_isPaused;
            bool errorUp = m_errorCard.alpha > 0.5f;
            bool overlayUp = m_library.alpha > 0.5f || m_settings.alpha > 0.5f;

            // texts
            if (info != null)
            {
                m_title.text = info.Title;
                m_startTitle.text = info.Title;
                m_loadText.text = "Loading " + info.Title;
                m_formatText.text = info.Format;
                m_formatBadge.gameObject.SetActive(info.Format.Length > 0);
                m_sourceText.text = info.Streamed ? "Streaming" : "On this device";
                m_originText.text = info.Streamed && info.Origin.Length > 0 ? info.Origin : "";
                m_loadSub.text = info.Streamed && info.Origin.Length > 0 ? "Fetching from " + info.Origin : "Reading from this device";
                string len = info.Seconds > 0 ? $"   {info.Seconds:0.0} s loop" : "";
                m_startInfo.text = $"{info.Format}   {(info.Streamed ? "Streaming" : "On this device")}{len}";
                m_subtitle.text = len.Trim();
            }
            m_counter.text = (m_media.Length > 0 && m_scene != null) ? $"{m_scene.MediaIndex + 1} of {m_media.Length}" : "";
            m_playIcon.sprite = (started && !paused) ? m_iPause : m_iPlay;
            m_playLabel.text = !started ? "Play" : (paused ? "Resume" : "Pause");

            // visibility
            bool idle = Time.unscaledTime - m_lastTouch > 3.5f;
            bool showControls = started && !errorUp && !overlayUp && (paused || !idle) && !m_isLoading;
            Fade(m_controls, showControls ? 1f : 0f);
            Fade(m_startCard, (!started && !errorUp && !overlayUp) ? 1f : 0f);
            Fade(m_loading, (started && m_isLoading && !errorUp && !overlayUp) ? 1f : 0f);
            m_controls.blocksRaycasts = m_controls.alpha > 0.5f;
            m_startCard.blocksRaycasts = m_startCard.alpha > 0.5f;
            m_errorCard.blocksRaycasts = errorUp;
            m_errorCard.interactable = errorUp;

            Fade(m_toast, Time.unscaledTime <= m_toastUntil ? 1f : 0f);

            UpdateStats(info, started, paused);
            if (m_testTask != null && m_testTask.IsCompleted)
            {
                string r = m_testTask.Result;
                m_testTask = null;
                if (r == "ok") { m_testStatus.text = "Reachable"; m_testStatus.color = Good; }
                else { m_testStatus.text = "Can't reach it. " + FriendlyNetError(r); m_testStatus.color = Warn; }
            }

            if (m_spinner != null && m_loading.alpha > 0.01f)
            {
                m_spinner.Rotate(0f, 0f, -240f * Time.unscaledDeltaTime);
            }
        }

        private static void Fade(CanvasGroup g, float target)
        {
            g.alpha = Mathf.MoveTowards(g.alpha, target, Time.unscaledDeltaTime * 5f);
        }

        private static void SetGroup(CanvasGroup g, bool on)
        {
            g.alpha = on ? 1f : 0f;
            g.blocksRaycasts = on;
            g.interactable = on;
        }

        private void ShowToast(string text, Color tint, float seconds)
        {
            m_toastText.text = text;
            m_toastText.color = tint == Accent ? Color.white : tint;
            m_toastUntil = Time.unscaledTime + seconds;
        }

        // ------------------------------------------------------------------ actions

        private void OnPlayPause() { if (m_scene != null) m_scene.StartStop(); m_lastTouch = Time.unscaledTime; }
        private void OnNext() { if (m_scene != null) m_scene.LoadNextMedia(); m_lastTouch = Time.unscaledTime; }
        private void OnPrev() { if (m_scene != null) m_scene.LoadPreviousMedia(); m_lastTouch = Time.unscaledTime; }
        private void OnReset()
        {
            var cam = FindFirstObjectByType<V3CCameraController>();
            if (cam != null) cam.ResetView();
            m_lastTouch = Time.unscaledTime;
        }

        private void OpenLibrary()
        {
            BuildLibraryCards();
            SetGroup(m_settings, false);
            SetGroup(m_library, true);
            m_lastTouch = Time.unscaledTime;
        }

        private void CloseLibrary() { SetGroup(m_library, false); }

        private void OpenSettings()
        {
            SetGroup(m_library, false);
            string ip, port;
            ReadServer(out ip, out port);
            m_ipField.text = ip;
            m_portField.text = port;
            m_testStatus.text = "";
            SetStatsToggle();
            SetGroup(m_settings, true);
        }

        private void CloseSettings() { SetGroup(m_settings, false); }

        private void PlayIndex(int index)
        {
            CloseLibrary();
            if (m_scene != null) m_scene.PlayMedia(index);
            m_lastTouch = Time.unscaledTime;
        }

        private string ConfigPath()
        {
            return Path.Combine(Application.persistentDataPath, m_manager != null ? m_manager.m_configFile : "config.json");
        }

        private void ReadServer(out string ip, out string port)
        {
            ip = ""; port = "";
            try
            {
                string text = File.ReadAllText(ConfigPath());
                var mi = Regex.Match(text, "\"IP\"\\s*:\\s*\"([^\"]*)\"");
                var mp = Regex.Match(text, "\"Port\"\\s*:\\s*(\\d+)");
                if (mi.Success) ip = mi.Groups[1].Value;
                if (mp.Success) port = mp.Groups[1].Value;
            }
            catch (Exception e) { Debug.LogWarning($"HUD: cannot read config.json: {e.Message}"); }
        }

        private void SaveSettings()
        {
            string ip = m_ipField.text.Trim();
            int port;
            if (ip.Length == 0 || !int.TryParse(m_portField.text, out port) || port < 1 || port > 65535)
            {
                m_testStatus.text = "Enter an address and a port between 1 and 65535.";
                m_testStatus.color = Warn;
                return;
            }
            try
            {
                string path = ConfigPath();
                string text = File.ReadAllText(path);
                var ri = new Regex("\"IP\"\\s*:\\s*\"[^\"]*\"");
                var rp = new Regex("\"Port\"\\s*:\\s*\\d+");
                if (!ri.IsMatch(text) || !rp.IsMatch(text)) throw new Exception("no server entry in config.json");
                text = ri.Replace(text, "\"IP\": \"" + ip + "\"", 1);
                text = rp.Replace(text, "\"Port\": " + port, 1);
                File.WriteAllText(path, text);
                m_testStatus.text = "Saved. Close and reopen the app to apply.";
                m_testStatus.color = Good;
            }
            catch (Exception e)
            {
                m_testStatus.text = "Could not save: " + e.Message;
                m_testStatus.color = Warn;
            }
        }

        private void TestConnection()
        {
            string ip = m_ipField.text.Trim();
            int port;
            if (ip.Length == 0 || !int.TryParse(m_portField.text, out port))
            {
                m_testStatus.text = "Enter an address and a port first.";
                m_testStatus.color = Warn;
                return;
            }
            m_testStatus.text = "Testing " + ip + ":" + port + " ...";
            m_testStatus.color = Secondary;
            m_testTask = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using (var c = new System.Net.Sockets.TcpClient())
                    {
                        var t = c.ConnectAsync(ip, port);
                        return (t.Wait(2500) && c.Connected) ? "ok" : "no answer in 2.5 s";
                    }
                }
                catch (Exception e) { return e.GetBaseException().Message; }
            });
        }

        private static string FriendlyNetError(string r)
        {
            string l = (r ?? "").ToLowerInvariant();
            if (l.Contains("(111)") || l.Contains("refused")) return "Connection refused: nothing is listening at that address and port.";
            if (l.Contains("(113)") || l.Contains("(101)") || l.Contains("unreachable")) return "No route to that address from this device.";
            if (l.Contains("no answer")) return "No answer within 2.5 s. Check the address and the network.";
            if (l.Contains("resolve") || l.Contains("no such host") || l.Contains("name")) return "The address could not be resolved.";
            return r;
        }

        private void ToggleStats()
        {
            m_statsOn = !m_statsOn;
            PlayerPrefs.SetInt("hud.stats", m_statsOn ? 1 : 0);
            PlayerPrefs.Save();
            SetStatsToggle();
        }

        private void SetStatsToggle()
        {
            m_statsToggleBg.color = m_statsOn ? Accent : new Color32(0x3A, 0x3A, 0x3A, 0xFF);
            m_statsKnob.anchoredPosition = new Vector2(m_statsOn ? 30f : -30f, 0f);
        }

        private bool HardwareHevc()
        {
            try
            {
                string text = File.ReadAllText(ConfigPath());
                var m = Regex.Match(text, "\"Name\"\\s*:\\s*\"hevc\"[^}]*?\"HardwareAcceleration\"\\s*:\\s*(true|false)");
                return m.Success && m.Groups[1].Value == "true";
            }
            catch (Exception) { return false; }
        }

        private float m_statsNext;
        private void UpdateStats(MediaInfo info, bool started, bool paused)
        {
            m_fps = Mathf.Lerp(m_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
            Fade(m_stats, m_statsOn ? 1f : 0f);
            if (!m_statsOn || Time.unscaledTime < m_statsNext) return;
            m_statsNext = Time.unscaledTime + 0.5f;
            string state = !started ? "stopped" : (m_isLoading ? "loading" : (paused ? "paused" : "playing"));
            m_statsText.text =
                $"Render   {m_fps:0} fps\n" +
                $"State    {state}\n" +
                $"Title    {(info != null ? info.Title : "-")}\n" +
                $"Source   {(info != null ? (info.Streamed ? "streaming " + info.Origin : "device") : "-")}\n" +
                $"HEVC     {(HardwareHevc() ? "hardware" : "software")} (config)\n" +
                $"Screen   {Screen.width}x{Screen.height}\n" +
                $"GPU      {SystemInfo.graphicsDeviceName}";
        }

        // ------------------------------------------------------------------ ui construction

        private void LoadAssets()
        {
            m_regular = Resources.Load<Font>("Hud/Fonts/Ubuntu-Regular");
            m_medium = Resources.Load<Font>("Hud/Fonts/Ubuntu-Medium");
            m_bold = Resources.Load<Font>("Hud/Fonts/Ubuntu-Bold");
            if (m_regular == null) m_regular = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (m_medium == null) m_medium = m_regular;
            if (m_bold == null) m_bold = m_regular;

            m_iPlay = IconSprite("play");
            m_iPause = IconSprite("pause");
            m_iPrev = IconSprite("previous");
            m_iNext = IconSprite("next");
            m_iReset = IconSprite("reset");

            m_round = MakeRounded(40, 16);
            m_gradDown = MakeGradient(true);
            m_gradUp = MakeGradient(false);
            m_ring = MakeRing(128);
            m_gridIcon = MakeGridIcon();
            m_statsOn = PlayerPrefs.GetInt("hud.stats", 0) == 1;
        }

        private static Sprite IconSprite(string name)
        {
            var tex = Resources.Load<Texture2D>("Hud/Icons/" + name);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) / 2f, half = size / 2f - radius;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - c) - half + 0.5f, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - c) - half + 0.5f, 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Sprite MakeGradient(bool darkAtTop)
        {
            var tex = new Texture2D(2, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
            {
                float t = y / 63f;                  // 0 bottom .. 1 top
                float a = darkAtTop ? t : 1f - t;
                a = a * a * 0.85f;
                for (int x = 0; x < 2; x++) tex.SetPixel(x, y, new Color(0, 0, 0, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite MakeRing(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float c = (size - 1) / 2f, rOut = size / 2f - 1f, rIn = rOut - size * 0.09f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01(rOut - d + 0.5f) * Mathf.Clamp01(d - rIn + 0.5f);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;          // -180..180
                    float arc = Mathf.Clamp01((ang + 180f) / 270f);           // gap in the last quarter
                    float fade = (ang + 180f) < 270f ? Mathf.Lerp(0.15f, 1f, arc) : 0f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, edge * fade));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------ framing and thumbnails

        private string ThumbPath(MediaInfo m)
        {
            string safe = Regex.Replace(TitleKey(m.Raw ?? "title"), "[^A-Za-z0-9_.-]", "_");
            return Path.Combine(Application.persistentDataPath, "thumbs", safe + ".png");
        }

        // Thumbnails are taken from the player's own picture on this device and kept in its app folder.
        private Texture2D Thumb(MediaInfo m)
        {
            if (m == null || m.Raw == null) return null;
            Texture2D t;
            if (m_thumbs.TryGetValue(TitleKey(m.Raw), out t)) return t;
            string path = ThumbPath(m);
            if (!File.Exists(path)) return null;
            try
            {
                t = new Texture2D(2, 2, TextureFormat.RGB24, false);
                t.LoadImage(File.ReadAllBytes(path));
                m_thumbs[TitleKey(m.Raw)] = t;
                return t;
            }
            catch (Exception e) { Debug.LogWarning($"HUD: cannot read thumbnail: {e.Message}"); return null; }
        }

        private IEnumerator CaptureThumb(MediaInfo info)
        {
            if (info == null || info.Raw == null || File.Exists(ThumbPath(info))) yield break;
            yield return new WaitForSeconds(2.4f);
            if (Current() != info || m_manager == null || !m_manager.m_isStarted || m_manager.m_isPaused || m_isLoading || m_errorCard.alpha > 0.5f) yield break;

            m_canvas.enabled = false;
            yield return new WaitForEndOfFrame();
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            m_canvas.enabled = true;
            if (full == null) yield break;

            int h = full.height, w = Mathf.Min(full.width, Mathf.RoundToInt(h * 1.6f)), x0 = (full.width - w) / 2;
            var crop = new Texture2D(w, h, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x0, 0, w, h));
            crop.Apply();
            Destroy(full);

            var rt = RenderTexture.GetTemporary(480, 300, 0);
            Graphics.Blit(crop, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(480, 300, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0, 0, 480, 300), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(crop);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThumbPath(info)));
                File.WriteAllBytes(ThumbPath(info), small.EncodeToPNG());
                m_thumbs[TitleKey(info.Raw)] = small;
            }
            catch (Exception e) { Debug.LogWarning($"HUD: cannot save thumbnail: {e.Message}"); }
        }

        private static Sprite MakeGridIcon()
        {
            int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            int cell = 36, gap = 12, off = (n - (2 * cell + gap)) / 2, r = 7;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float a = 0f;
                    for (int gy = 0; gy < 2; gy++)
                    {
                        for (int gx = 0; gx < 2; gx++)
                        {
                            float cx = off + gx * (cell + gap) + cell / 2f, cy = off + gy * (cell + gap) + cell / 2f;
                            float dx = Mathf.Max(Mathf.Abs(x - cx) - (cell / 2f - r), 0f), dy = Mathf.Max(Mathf.Abs(y - cy) - (cell / 2f - r), 0f);
                            a = Mathf.Max(a, Mathf.Clamp01(0.5f - (Mathf.Sqrt(dx * dx + dy * dy) - r)));
                        }
                    }
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        private void BuildLibraryCards()
        {
            foreach (Transform t in m_libContent) Destroy(t.gameObject);
            int current = m_scene != null ? m_scene.MediaIndex : -1;
            bool started = m_manager != null && m_manager.m_isStarted;
            float y = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                bool streamed = pass == 0;
                var idx = new System.Collections.Generic.List<int>();
                for (int i = 0; i < m_media.Length; i++) { if (m_media[i] != null && m_media[i].Streamed == streamed) idx.Add(i); }
                if (idx.Count == 0) continue;

                var head = Rect("Head", m_libContent, TL, TL, TL, new Vector2(0, -y), new Vector2(1500, 44));
                Txt(head, streamed ? "Streaming from origin" : "On this device", 32, m_bold, Color.white, TextAnchor.MiddleLeft);
                string sub = streamed ? (m_media[idx[0]].Origin.Length > 0 ? m_media[idx[0]].Origin : "") : "works offline";
                var hs = Rect("Sub", m_libContent, TL, TL, TL, new Vector2(420, -y), new Vector2(700, 44));
                Txt(hs, sub, 24, m_regular, Secondary, TextAnchor.MiddleLeft);
                y += 64f;

                for (int k = 0; k < idx.Count; k++)
                {
                    int i = idx[k];
                    var info = m_media[i];
                    float x = (k % 3) * 610f;
                    float yy = y + (k / 3) * 196f;
                    var card = Rect("Card" + i, m_libContent, TL, TL, TL, new Vector2(x, -yy), new Vector2(590, 176));
                    var bg = Img(card, m_round, i == current && started ? new Color32(0x24, 0x32, 0x38, 0xFF) : new Color32(0x1E, 0x1E, 0x1E, 0xFF), true, 20, true);
                    bg.pixelsPerUnitMultiplier = 16f / 24f;
                    var b = card.gameObject.AddComponent<Button>();
                    b.targetGraphic = bg;
                    int captured = i;
                    b.onClick.AddListener(() => PlayIndex(captured));

                    var tile = Rect("Tile", card, TL, TL, TL, new Vector2(12, -12), new Vector2(152, 152));
                    bool pcc = info.Format == "V-PCC";
                    var tileBg = Img(tile, m_round, pcc ? new Color32(0x0B, 0x4F, 0x66, 0xFF) : new Color32(0x1B, 0x4A, 0x3E, 0xFF), true, 20);
                    tileBg.pixelsPerUnitMultiplier = 16f / 20f;
                    var thumb = Thumb(info);
                    if (thumb != null)
                    {
                        tile.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                        var picRt = Rect("Pic", tile, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
                        var raw = picRt.gameObject.AddComponent<RawImage>();
                        raw.texture = thumb; raw.uvRect = new Rect(0.1875f, 0f, 0.625f, 1f); raw.raycastTarget = false;
                        var fmt = Rect("Fmt", tile, BL, BL, BL, new Vector2(8, 8), new Vector2(104, 34));
                        Img(fmt, m_round, new Color(0f, 0f, 0f, 0.65f), true, 12).pixelsPerUnitMultiplier = 16f / 17f;
                        Txt(Rect("t", fmt, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero), info.Format, 20, m_bold, Color.white, TextAnchor.MiddleCenter);
                    }
                    else
                    {
                        var tt = Rect("T", tile, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
                        Txt(tt, info.Format.Length > 0 ? info.Format : "V3C", 34, m_bold, Color.white, TextAnchor.MiddleCenter);
                    }

                    var title = Rect("Title", card, TL, TL, TL, new Vector2(184, -16), new Vector2(390, 44));
                    var tx = Txt(title, info.Title, 32, m_bold, Color.white, TextAnchor.MiddleLeft);
                    tx.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var desc = Rect("Desc", card, TL, TL, TL, new Vector2(184, -62), new Vector2(390, 36));
                    Txt(desc, info.Description, 24, m_regular, Color.white, TextAnchor.MiddleLeft).horizontalOverflow = HorizontalWrapMode.Overflow;
                    var meta = Rect("Meta", card, TL, TL, TL, new Vector2(184, -100), new Vector2(390, 34));
                    string len = info.Seconds > 0 ? $"{info.Seconds:0.0} s loop" : "";
                    Txt(meta, len, 22, m_regular, Secondary, TextAnchor.MiddleLeft);
                    if (i == current && started)
                    {
                        var tag = Rect("Tag", card, TL, TL, TL, new Vector2(184, -134), new Vector2(180, 32));
                        Img(tag, m_round, Accent, true, 14).pixelsPerUnitMultiplier = 16f / 16f;
                        Txt(Rect("t", tag, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero), "Now playing", 20, m_bold, new Color(0f, 0.137f, 0.176f, 1f), TextAnchor.MiddleCenter);
                    }
                }
                y += ((idx.Count + 2) / 3) * 196f + 20f;
            }
            if (y == 0f)
            {
                var none = Rect("None", m_libContent, TL, TL, TL, Vector2.zero, new Vector2(1500, 60));
                Txt(none, "No titles found. Check that library.json is in the data folder of the app.", 30, m_regular, Secondary, TextAnchor.MiddleLeft);
            }
        }

        private RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        private Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced, float cornerPx = 16f, bool raycast = false)
        {
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite; im.color = color; im.raycastTarget = raycast;
            if (sliced && sprite != null) { im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 16f / cornerPx; }
            return im;
        }

        private Text Txt(RectTransform rt, string text, int size, Font font, Color color, TextAnchor anchor)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.color = color; t.alignment = anchor; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; t.supportRichText = false;
            return t;
        }

        private static readonly Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = new Vector2(0, 0), BR = new Vector2(1, 0);
        private static readonly Vector2 C = new Vector2(0.5f, 0.5f);

        private void BuildUi()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            m_canvas = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 860);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var root = canvasGo.GetComponent<RectTransform>();

            BuildControls(root);
            BuildStartCard(root);
            BuildLoading(root);
            BuildStats(root);
            BuildLibrary(root);
            BuildSettings(root);
            BuildToast(root);
            BuildErrorCard(root);
        }

        private CanvasGroup Group(RectTransform rt, float alpha)
        {
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = alpha; g.blocksRaycasts = alpha > 0.5f; g.interactable = true;
            return g;
        }

        private void BuildControls(RectTransform root)
        {
            var wrap = Rect("Controls", root, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            m_controls = Group(wrap, 0f);

            // top: title, badges
            var top = Rect("TopBar", wrap, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 220));
            Img(top, m_gradDown, Color.white, false);
            var title = Rect("Title", top, TL, TL, TL, new Vector2(56, -36), new Vector2(1100, 64));
            m_title = Txt(title, "", 46, m_bold, Color.white, TextAnchor.MiddleLeft);

            var fmt = Rect("Format", top, TL, TL, TL, new Vector2(56, -112), new Vector2(150, 44));
            m_formatBadge = Img(fmt, m_round, new Color(0f, 0.17f, 0.23f, 0.92f), true, 16);
            m_formatText = Txt(Rect("t", fmt, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero), "", 24, m_medium, new Color32(0x4F, 0xCB, 0xF2, 0xFF), TextAnchor.MiddleCenter);
            var src = Rect("Source", top, TL, TL, TL, new Vector2(224, -112), new Vector2(210, 44));
            m_sourceBadge = Img(src, m_round, new Color(0.07f, 0.07f, 0.07f, 0.8f), true, 16);
            m_sourceText = Txt(Rect("t", src, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero), "", 24, m_medium, Color.white, TextAnchor.MiddleCenter);
            var org = Rect("Origin", top, TL, TL, TL, new Vector2(452, -112), new Vector2(520, 44));
            m_originText = Txt(org, "", 24, m_regular, Secondary, TextAnchor.MiddleLeft);
            var sub = Rect("Sub", top, TR, TR, TR, new Vector2(-56, -50), new Vector2(420, 40));
            m_subtitle = Txt(sub, "", 24, m_regular, Secondary, TextAnchor.MiddleRight);

            // bottom: transport
            var bot = Rect("BottomBar", wrap, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 260));
            Img(bot, m_gradUp, Color.white, false);

            var cnt = Rect("Counter", bot, BL, BL, BL, new Vector2(56, 196), new Vector2(300, 40));
            m_counter = Txt(cnt, "", 24, m_regular, Secondary, TextAnchor.MiddleLeft);

            MakeIconButton(bot, "Library", 0f, 104, m_gridIcon, "Library", false, OpenLibrary, null, float.NaN, true);
            MakeIconButton(bot, "Prev", -190f, 104, m_iPrev, "Previous", false, OnPrev, null);
            MakeIconButton(bot, "Play", 0f, 140, m_iPlay, "Play", true, OnPlayPause, p => { m_playIcon = p.icon; m_playLabel = p.label; });
            MakeIconButton(bot, "Next", 190f, 104, m_iNext, "Next", false, OnNext, null);
            MakeIconButton(bot, "Reset", 0f, 104, m_iReset, "Reset view", false, OnReset, null, -56f);
        }

        private struct BtnParts { public Image icon; public Text label; }

        // x is from the bar centre; xFromRight (when given) is from the right edge instead
        private void MakeIconButton(RectTransform parent, string name, float x, float size, Sprite icon, string label, bool primary,
                                    UnityEngine.Events.UnityAction onClick, Action<BtnParts> keep, float xFromRight = float.NaN, bool left = false)
        {
            bool right = !float.IsNaN(xFromRight);
            var a = right ? BR : new Vector2(0.5f, 0f);
            var rt = Rect(name, parent, a, a, new Vector2(0.5f, 0f), new Vector2(right ? xFromRight - size / 2f - 0f : x, 76), new Vector2(size, size));
            if (right) rt.anchoredPosition = new Vector2(-56f - size / 2f, 76);
            if (left)
            {
                rt.anchorMin = rt.anchorMax = BL;
                rt.anchoredPosition = new Vector2(56f + size / 2f, 76);
            }
            var bg = Img(rt, m_round, primary ? Color.white : new Color(1, 1, 1, 0.16f), true, 16, true);
            bg.pixelsPerUnitMultiplier = 16f / (size / 2f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); colors.highlightedColor = Color.white; colors.selectedColor = Color.white;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var ic = Rect("Icon", rt, C, C, C, Vector2.zero, new Vector2(size * 0.6f, size * 0.58f));
            var icon_img = Img(ic, icon, primary ? new Color(0.07f, 0.07f, 0.07f, 1f) : Color.white, false);
            icon_img.preserveAspect = true;

            var lb = Rect("Label", rt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(240, 36));
            var lt = Txt(lb, label, 24, m_medium, Secondary, TextAnchor.UpperCenter);
            if (keep != null) keep(new BtnParts { icon = icon_img, label = lt });
        }

        private void BuildStartCard(RectTransform root)
        {
            var scrim = Rect("Start", root, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            m_startCard = Group(scrim, 1f);
            var card = Rect("Card", scrim, C, C, C, Vector2.zero, new Vector2(980, 480));
            Img(card, m_round, Card, true, 24, true).pixelsPerUnitMultiplier = 16f / 28f;

            var kick = Rect("Kicker", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(-96, 36));
            Txt(kick, "MPEG V3C IMMERSIVE PLATFORM", 22, m_medium, Accent, TextAnchor.MiddleLeft);
            var t = Rect("Title", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(-96, 80));
            m_startTitle = Txt(t, "V3C Immersive Player", 56, m_bold, Color.white, TextAnchor.MiddleLeft);
            var inf = Rect("Info", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -184), new Vector2(-96, 40));
            m_startInfo = Txt(inf, "", 28, m_regular, Secondary, TextAnchor.MiddleLeft);

            var btn = Rect("PlayButton", card, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(48, 52), new Vector2(300, 96));
            var bg = Img(btn, m_round, Accent, true, 16, true);
            bg.pixelsPerUnitMultiplier = 16f / 48f;
            var b = btn.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var cc = b.colors; cc.pressedColor = new Color(0.75f, 0.85f, 0.9f, 1f); b.colors = cc;
            b.onClick.AddListener(OnPlayPause);
            var ic = Rect("Icon", btn, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(46, 46));
            Img(ic, m_iPlay, new Color(0f, 0.137f, 0.176f, 1f), false).preserveAspect = true;
            var lt = Rect("Label", btn, Vector2.zero, Vector2.one, C, new Vector2(30, 0), Vector2.zero);
            Txt(lt, "Play", 32, m_bold, new Color(0f, 0.137f, 0.176f, 1f), TextAnchor.MiddleCenter);

            MakeCardButton(card, "Library", 372f, 240f, false, OpenLibrary);
            var hint = Rect("Hint", card, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(48, 150), new Vector2(860, 40));
            Txt(hint, "Press play to start. Drag to orbit, pinch to zoom.", 24, m_regular, Secondary, TextAnchor.MiddleLeft);
        }

        private void BuildLoading(RectTransform root)
        {
            var wrap = Rect("Loading", root, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            m_loading = Group(wrap, 0f);
            var card = Rect("Card", wrap, C, C, C, Vector2.zero, new Vector2(700, 280));
            Img(card, m_round, new Color(0.07f, 0.07f, 0.07f, 0.82f), true, 24).pixelsPerUnitMultiplier = 16f / 28f;
            m_spinner = Rect("Spinner", card, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -78), new Vector2(86, 86));
            Img(m_spinner, m_ring, Accent, false);
            var t = Rect("Text", card, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 74), new Vector2(-48, 50));
            m_loadText = Txt(t, "Loading", 34, m_bold, Color.white, TextAnchor.MiddleCenter);
            var s = Rect("Sub", card, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(-48, 36));
            m_loadSub = Txt(s, "", 24, m_regular, Secondary, TextAnchor.MiddleCenter);
        }

        private void BuildToast(RectTransform root)
        {
            var wrap = Rect("Toast", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(1100, 72));
            m_toast = Group(wrap, 0f);
            m_toast.blocksRaycasts = false; m_toast.interactable = false;
            Img(wrap, m_round, new Color(0.07f, 0.07f, 0.07f, 0.88f), true, 16).pixelsPerUnitMultiplier = 16f / 36f;
            m_toastText = Txt(Rect("t", wrap, Vector2.zero, Vector2.one, C, Vector2.zero, new Vector2(-48, 0)), "", 28, m_medium, Color.white, TextAnchor.MiddleCenter);
        }

        private void BuildErrorCard(RectTransform root)
        {
            var scrim = Rect("Error", root, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            Img(scrim, null, new Color(0f, 0f, 0f, 0.55f), false, 16, true);
            m_errorCard = Group(scrim, 0f);
            var card = Rect("Card", scrim, C, C, C, Vector2.zero, new Vector2(1080, 520));
            Img(card, m_round, Card, true, 24, true).pixelsPerUnitMultiplier = 16f / 28f;

            var kick = Rect("Kicker", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(-96, 32));
            Txt(kick, "PLAYBACK PROBLEM", 22, m_medium, Warn, TextAnchor.MiddleLeft);
            var t = Rect("Title", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(-96, 64));
            m_errTitle = Txt(t, "", 44, m_bold, Color.white, TextAnchor.MiddleLeft);
            var m = Rect("Message", card, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(-96, 150));
            m_errMsg = Txt(m, "", 30, m_regular, Color.white, TextAnchor.UpperLeft);
            var d = Rect("Detail", card, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 176), new Vector2(-96, 56));
            Img(d, m_round, new Color(0.086f, 0.086f, 0.086f, 1f), true, 12).pixelsPerUnitMultiplier = 16f / 14f;
            m_errDetail = Txt(Rect("t", d, Vector2.zero, Vector2.one, C, Vector2.zero, new Vector2(-32, 0)), "", 24, m_regular, Secondary, TextAnchor.MiddleLeft);

            MakeCardButton(card, "Retry", 48f, 300f, true, Retry);
            MakeCardButton(card, "Dismiss", 372f, 240f, false, HideError);
            m_localBtn = MakeCardButton(card, "Play local copy", 636f, 400f, false, PlayLocalCopy);
            m_localBtn.gameObject.SetActive(false);
        }

        private RectTransform MakeCardButton(RectTransform card, string label, float x, float w, bool filled, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect(label, card, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 44), new Vector2(w, 92));
            var bg = Img(rt, m_round, filled ? Accent : new Color32(0x2C, 0x2C, 0x2C, 0xFF), true, 16, true);
            bg.pixelsPerUnitMultiplier = 16f / 46f;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(onClick);
            Txt(Rect("t", rt, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero), label, 32, m_bold,
                filled ? new Color(0f, 0.137f, 0.176f, 1f) : Accent, TextAnchor.MiddleCenter);
            return rt;
        }

        private void BuildStats(RectTransform root)
        {
            var r = Rect("Stats", root, TR, TR, TR, new Vector2(-28, -120), new Vector2(430, 252));
            m_stats = Group(r, 0f);
            m_stats.blocksRaycasts = false; m_stats.interactable = false;
            Img(r, m_round, new Color(0f, 0f, 0f, 0.62f), true, 16).pixelsPerUnitMultiplier = 16f / 18f;
            var t = Rect("t", r, Vector2.zero, Vector2.one, C, Vector2.zero, new Vector2(-36, -24));
            m_statsText = Txt(t, "", 22, Resources.Load<Font>("Hud/Fonts/Ubuntu-Regular"), Color.white, TextAnchor.UpperLeft);
            m_statsText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void BuildLibrary(RectTransform root)
        {
            var scrim = Rect("Library", root, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            Img(scrim, null, Surface, false, 16, true);
            m_library = Group(scrim, 0f);
            m_library.interactable = false;

            var back = Rect("Back", scrim, TL, TL, TL, new Vector2(48, -36), new Vector2(190, 76));
            var bg = Img(back, m_round, new Color32(0x2C, 0x2C, 0x2C, 0xFF), true, 16, true);
            bg.pixelsPerUnitMultiplier = 16f / 38f;
            var bb = back.gameObject.AddComponent<Button>(); bb.targetGraphic = bg; bb.onClick.AddListener(CloseLibrary);
            Txt(Rect("t", back, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero), "Back", 30, m_bold, Color.white, TextAnchor.MiddleCenter);

            var title = Rect("Title", scrim, TL, TL, TL, new Vector2(270, -34), new Vector2(900, 80));
            Txt(title, "Library", 52, m_bold, Color.white, TextAnchor.MiddleLeft);

            var set = Rect("Settings", scrim, TR, TR, TR, new Vector2(-48, -36), new Vector2(250, 76));
            var sbg = Img(set, m_round, new Color32(0x2C, 0x2C, 0x2C, 0xFF), true, 16, true);
            sbg.pixelsPerUnitMultiplier = 16f / 38f;
            var sb = set.gameObject.AddComponent<Button>(); sb.targetGraphic = sbg; sb.onClick.AddListener(OpenSettings);
            Txt(Rect("t", set, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero), "Settings", 30, m_bold, Accent, TextAnchor.MiddleCenter);

            m_libContent = Rect("Content", scrim, TL, TL, TL, new Vector2(48, -150), new Vector2(1824, 680));
        }

        private InputField MakeField(RectTransform parent, string name, Vector2 pos, Vector2 size, InputField.ContentType type)
        {
            var rt = Rect(name, parent, TL, TL, TL, pos, size);
            var bg = Img(rt, m_round, new Color32(0x16, 0x16, 0x16, 0xFF), true, 12, true);
            bg.pixelsPerUnitMultiplier = 16f / 14f;
            var txt = Txt(Rect("Text", rt, Vector2.zero, Vector2.one, C, Vector2.zero, new Vector2(-32, -8)), "", 32, m_regular, Color.white, TextAnchor.MiddleLeft);
            txt.supportRichText = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = Txt(Rect("Placeholder", rt, Vector2.zero, Vector2.one, C, Vector2.zero, new Vector2(-32, -8)), "", 32, m_regular, Secondary, TextAnchor.MiddleLeft);
            var f = rt.gameObject.AddComponent<InputField>();
            f.targetGraphic = bg; f.textComponent = txt; f.placeholder = ph; f.contentType = type;
            f.caretColor = Accent; f.selectionColor = new Color(0f, 0.627f, 0.824f, 0.4f);
            return f;
        }

        private void BuildSettings(RectTransform root)
        {
            var scrim = Rect("Settings", root, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero);
            Img(scrim, null, new Color(0f, 0f, 0f, 0.7f), false, 16, true);
            m_settings = Group(scrim, 0f);
            m_settings.interactable = false;
            var card = Rect("Card", scrim, C, C, C, Vector2.zero, new Vector2(1180, 720));
            Img(card, m_round, Card, true, 24, true).pixelsPerUnitMultiplier = 16f / 28f;

            var t = Rect("Title", card, TL, TL, TL, new Vector2(48, -36), new Vector2(800, 64));
            Txt(t, "Settings", 46, m_bold, Color.white, TextAnchor.MiddleLeft);
            var k = Rect("Kicker", card, TL, TL, TL, new Vector2(48, -112), new Vector2(600, 32));
            Txt(k, "ORIGIN", 22, m_medium, Accent, TextAnchor.MiddleLeft);

            var l1 = Rect("L1", card, TL, TL, TL, new Vector2(48, -150), new Vector2(600, 34));
            Txt(l1, "Address", 24, m_regular, Secondary, TextAnchor.MiddleLeft);
            m_ipField = MakeField(card, "Ip", new Vector2(48, -188), new Vector2(720, 76), InputField.ContentType.Standard);
            var l2 = Rect("L2", card, TL, TL, TL, new Vector2(800, -150), new Vector2(300, 34));
            Txt(l2, "Port", 24, m_regular, Secondary, TextAnchor.MiddleLeft);
            m_portField = MakeField(card, "Port", new Vector2(800, -188), new Vector2(240, 76), InputField.ContentType.IntegerNumber);

            var test = Rect("Test", card, TL, TL, TL, new Vector2(48, -292), new Vector2(300, 76));
            var tbg = Img(test, m_round, new Color32(0x2C, 0x2C, 0x2C, 0xFF), true, 16, true);
            tbg.pixelsPerUnitMultiplier = 16f / 38f;
            var tb = test.gameObject.AddComponent<Button>(); tb.targetGraphic = tbg; tb.onClick.AddListener(TestConnection);
            Txt(Rect("t", test, Vector2.zero, Vector2.one, C, Vector2.zero, Vector2.zero), "Test connection", 28, m_bold, Accent, TextAnchor.MiddleCenter);
            var st = Rect("Status", card, TL, TL, TL, new Vector2(376, -292), new Vector2(740, 76));
            m_testStatus = Txt(st, "", 26, m_regular, Secondary, TextAnchor.MiddleLeft);

            var div = Rect("Div", card, TL, TL, TL, new Vector2(48, -396), new Vector2(1084, 2));
            Img(div, null, new Color32(0x2C, 0x2C, 0x2C, 0xFF), false);
            var sl = Rect("StatsLabel", card, TL, TL, TL, new Vector2(48, -420), new Vector2(800, 44));
            Txt(sl, "Show stats panel", 30, m_medium, Color.white, TextAnchor.MiddleLeft);
            var sd = Rect("StatsDesc", card, TL, TL, TL, new Vector2(48, -462), new Vector2(900, 34));
            Txt(sd, "Frame rate, state, source, decoder setting, screen and GPU.", 22, m_regular, Secondary, TextAnchor.MiddleLeft);

            var tog = Rect("Toggle", card, TR, TR, TR, new Vector2(-48, -428), new Vector2(120, 60));
            m_statsToggleBg = Img(tog, m_round, new Color32(0x3A, 0x3A, 0x3A, 0xFF), true, 16, true);
            m_statsToggleBg.pixelsPerUnitMultiplier = 16f / 30f;
            var tgb = tog.gameObject.AddComponent<Button>(); tgb.targetGraphic = m_statsToggleBg; tgb.onClick.AddListener(ToggleStats);
            m_statsKnob = Rect("Knob", tog, C, C, C, new Vector2(-30, 0), new Vector2(44, 44));
            Img(m_statsKnob, m_round, Color.white, true, 16).pixelsPerUnitMultiplier = 16f / 22f;

            MakeCardButton(card, "Save", 48f, 240f, true, SaveSettings);
            MakeCardButton(card, "Close", 316f, 240f, false, CloseSettings);
        }

        // The scene's original icon rail is replaced by the HUD controls.
        private void HideLegacyRail()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "PlayerUI" && go.scene.IsValid())
                {
                    var g = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
                    g.alpha = 0f; g.blocksRaycasts = false; g.interactable = false;
                }
            }
        }
    }
}
