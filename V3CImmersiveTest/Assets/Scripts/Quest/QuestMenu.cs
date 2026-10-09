/*
 * License: 5G-MAG Public License (v1.0)
 * Author: Jordi J. Gimenez (5G-MAG)
 * Copyright: (C) 2026 5G-MAG Association
 *
 * For full license terms please see the LICENSE file distributed with this
 * program. If this file is missing then the license can be retrieved from
 * https://www.5g-mag.com/license.
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.OpenXR.Features.Meta;
using IDCC.V3CDecoder.Demo;

namespace IDCC.V3CDecoder.Quest
{
    // Shows the player's interface in the room, facing the viewer, only when the viewer asks for it (a pinch with the
    // left hand, or X on the left controller) or when something needs attention (nothing playing yet, a title
    // loading, an error). It is pointed at with the right hand or controller (pinch or trigger clicks) and offers
    // the choice of background: the stadium or the real room through the headset's cameras (passthrough).
    public class QuestMenu : MonoBehaviour
    {
        private const float PanelDistance = 0.85f;      // metres from the viewer, nearer than the figure so it never hides the panel
        private const float ShownSeconds = 6f;          // how long the panels stay once summoned and left alone
        private const float PanelScale = 0.0006f;       // metres per interface pixel
        private static readonly Color Accent = new Color32(0x00, 0xA0, 0xD2, 0xFF);
        private static readonly Color Button = new Color32(0x2C, 0x2C, 0x2C, 0xFF);

        private Transform m_rig;
        private Camera m_cam;
        private QuestRig m_questRig;
        private V3CPlayerHud m_hud;
        private RectTransform m_hudRect, m_barRect;
        private Canvas m_barCanvas;
        private Text m_backgroundLabel, m_boundaryLabel;
        private LineRenderer m_ray;
        private Selectable m_hover;
        private bool m_visible = true, m_triggerWas, m_xWas;
        private bool m_passthrough, m_hideBoundary, m_wasWorn;
        private float m_behindSince = -1f;
        private V3CDecoderManager m_manager;
        private float m_shownUntil;
        private bool m_summonWas;
        private AudioSource m_audio;
        private AudioClip m_tickClip, m_clickClip;
        private float m_pressUntil;
        private readonly Dictionary<Selectable, Fx> m_fx = new Dictionary<Selectable, Fx>();

        // the look of a control being pointed at: it lights up, comes toward the viewer and grows a little
        private class Fx { public RectTransform rect; public Image glow; public Vector3 scale, position; public float amount; }
        private ARCameraManager m_cameraManager;
        private bool m_ready;

        public void Init(Transform rig, Camera cam, QuestRig questRig)
        {
            m_rig = rig; m_cam = cam; m_questRig = questRig;
            StartCoroutine(Build());
        }

        private IEnumerator Build()
        {
            while (V3CPlayerHud.Instance == null || V3CPlayerHud.Instance.Canvas == null) yield return null;
            m_hud = V3CPlayerHud.Instance;

            // the interface becomes a panel in the room
            var canvas = m_hud.Canvas;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = m_cam;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            m_hudRect = (RectTransform)canvas.transform;
            m_hudRect.SetParent(m_rig, false);
            m_hudRect.anchorMin = m_hudRect.anchorMax = m_hudRect.pivot = new Vector2(0.5f, 0.5f);
            m_hudRect.sizeDelta = new Vector2(1920, 860);
            m_hudRect.localScale = Vector3.one * PanelScale;

            m_manager = FindFirstObjectByType<V3CDecoderManager>();
            var depthShader = Shader.Find("V3C/UIDepth");
            if (depthShader != null) m_uiDepth = new Material(depthShader);

            // the playback controls float in the room without the dark frames the phone layout draws behind them
            var controls = canvas.transform.Find("Controls") as RectTransform;
            if (controls != null)
            {
                controls.anchorMin = controls.anchorMax = new Vector2(0.5f, 0.5f);
                controls.pivot = new Vector2(0.5f, 0.5f);
                controls.anchoredPosition = Vector2.zero;
                controls.sizeDelta = new Vector2(1100, 520);
                foreach (var name in new[] { "TopBar", "BottomBar" })
                {
                    var bar = controls.Find(name);
                    var frame = bar != null ? bar.GetComponent<Image>() : null;
                    if (frame != null) frame.enabled = false;
                }
            }

            m_audio = gameObject.AddComponent<AudioSource>();
            m_audio.spatialBlend = 0f; m_audio.playOnAwake = false;
            m_tickClip = Tone(2200f, 0.03f, 0.12f);
            m_clickClip = Tone(1250f, 0.09f, 0.3f);

            BuildBar();

            // passthrough: the camera manager turns it on and off, a transparent background lets it show
            new GameObject("ARSession").AddComponent<ARSession>();
            m_cameraManager = m_cam.gameObject.AddComponent<ARCameraManager>();
            m_cameraManager.enabled = false;

            var lineObject = new GameObject("PointerRay");
            lineObject.transform.SetParent(m_rig, false);
            m_ray = lineObject.AddComponent<LineRenderer>();
            m_ray.material = new Material(Shader.Find("Sprites/Default"));
            m_ray.startColor = m_ray.endColor = Accent;
            m_ray.positionCount = 2;
            m_ray.sortingOrder = 200;
            m_ray.enabled = false;

            yield return new WaitForSeconds(2.5f);        // give the headset a moment to report its pose
            PlacePanels();
            m_ready = true;
            SetBackground(PlayerPrefs.GetInt("quest.passthrough", 0) == 1);
            SetBoundaryHidden(PlayerPrefs.GetInt("quest.hideBoundary", 0) == 1);
        }

        private void BuildBar()
        {
            var go = new GameObject("QuestBar", typeof(RectTransform));
            m_barRect = (RectTransform)go.transform;
            m_barRect.SetParent(m_rig, false);
            m_barCanvas = go.AddComponent<Canvas>();
            m_barCanvas.renderMode = RenderMode.WorldSpace;
            m_barCanvas.worldCamera = m_cam;
            go.AddComponent<GraphicRaycaster>();
            m_barRect.sizeDelta = new Vector2(1920, 110);
            m_barRect.localScale = Vector3.one * PanelScale;

            var back = new GameObject("Back", typeof(RectTransform)).GetComponent<RectTransform>();
            back.SetParent(m_barRect, false);
            back.anchorMin = Vector2.zero; back.anchorMax = Vector2.one; back.offsetMin = back.offsetMax = Vector2.zero;
            var bi = back.gameObject.AddComponent<Image>();
            bi.color = new Color(0.07f, 0.07f, 0.07f, 0.92f);
            bi.raycastTarget = false;

            var logo = Resources.Load<Texture2D>("Hud/Logo5GMAG");
            if (logo != null)
            {
                var lr = new GameObject("Logo", typeof(RectTransform)).GetComponent<RectTransform>();
                lr.SetParent(m_barRect, false);
                lr.anchorMin = lr.anchorMax = new Vector2(1, 0.5f); lr.pivot = new Vector2(1, 0.5f);
                lr.sizeDelta = new Vector2(300, 300f * logo.height / logo.width);
                lr.anchoredPosition = new Vector2(-40, 0);
                var li = lr.gameObject.AddComponent<Image>();
                li.sprite = Sprite.Create(logo, new Rect(0, 0, logo.width, logo.height), new Vector2(0.5f, 0.5f), 100f);
                li.preserveAspect = true; li.raycastTarget = false;
            }

            m_backgroundLabel = MakeButton("Background", 25, 450, () => SetBackground(!m_passthrough));
            m_boundaryLabel = MakeButton("Boundary", 505, 450, () => SetBoundaryHidden(!m_hideBoundary));
            MakeButton("Bring the figure to me", 985, 490, () => m_questRig.BringFigure()).text = "Bring the figure to me";
        }

        private Text MakeButton(string name, float x, float width, UnityEngine.Events.UnityAction onClick)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(m_barRect, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0); rt.sizeDelta = new Vector2(width, 76);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = m_hud.RoundSprite; img.color = Button;
            if (img.sprite != null) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 16f / 38f; }
            var b = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img; b.onClick.AddListener(onClick);
            var colors = b.colors; colors.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f); b.colors = colors;

            var tr = new GameObject("t", typeof(RectTransform)).GetComponent<RectTransform>();
            tr.SetParent(rt, false);
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var t = tr.gameObject.AddComponent<Text>();
            t.font = m_hud.BoldFont; t.fontSize = 30; t.color = Accent; t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false; t.text = name;
            return t;
        }

        private void SetBackground(bool passthrough)
        {
            m_passthrough = passthrough;
            PlayerPrefs.SetInt("quest.passthrough", passthrough ? 1 : 0);
            m_cameraManager.enabled = passthrough;
            m_cam.clearFlags = passthrough ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            m_cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            m_backgroundLabel.text = passthrough ? "Background: your room" : "Background: stadium";
            SetBoundaryHidden(m_hideBoundary);
        }

        // Hiding the boundary is a request the headset only accepts while the cameras show the room
        private void SetBoundaryHidden(bool hide)
        {
            m_hideBoundary = hide;
            PlayerPrefs.SetInt("quest.hideBoundary", hide ? 1 : 0);
            var feature = OpenXRSettings.Instance != null ? OpenXRSettings.Instance.GetFeature<BoundaryVisibilityFeature>() : null;
            string state = hide ? "hidden" : "shown";
            if (feature != null && feature.enabled)
            {
                var result = feature.TryRequestBoundaryVisibility(hide && m_passthrough ? XrBoundaryVisibility.VisibilitySuppressed : XrBoundaryVisibility.VisibilityNotSuppressed);
                Debug.Log($"QuestMenu: boundary request hide={hide} passthrough={m_passthrough} result={(int)result}");
                // right after passthrough is switched on the headset still refuses ("not allowed"), so ask again until it accepts
                if (hide && m_passthrough && (int)result == BoundaryVisibilityFeature.XR_BOUNDARY_VISIBILITY_SUPPRESSION_NOT_ALLOWED_META)
                {
                    if (m_boundaryRetry != null) StopCoroutine(m_boundaryRetry);
                    m_boundaryRetry = StartCoroutine(RetryBoundary(feature));
                }
                if (hide && !m_passthrough) state = "shown (room view only)";
            }
            else if (hide) state = "shown (not available)";
            m_boundaryLabel.text = "Boundary: " + state;
        }

        // The panels sit in front of where the viewer looks, a little below eye level, turned to face the head
        // The figure is drawn after the interface, so the interface has to write depth to stay in front of a figure that
        // is behind it. Controls are created while the app runs (library cards, hover glow), so this is repeated.
        private void ApplyDepthMaterial()
        {
            if (m_uiDepth == null || Time.unscaledTime < m_nextMaterialPass) return;
            m_nextMaterialPass = Time.unscaledTime + 0.3f;
            foreach (var canvas in new[] { m_hud.Canvas, m_barCanvas })
            {
                if (canvas == null || !canvas.enabled) continue;
                foreach (var g in canvas.GetComponentsInChildren<Graphic>(true))
                    if (g.material != m_uiDepth) g.material = m_uiDepth;
            }
        }

        private Coroutine m_boundaryRetry;

        private IEnumerator RetryBoundary(BoundaryVisibilityFeature feature)
        {
            for (int attempt = 0; attempt < 15 && m_hideBoundary && m_passthrough; attempt++)
            {
                yield return new WaitForSeconds(0.4f);
                var result = feature.TryRequestBoundaryVisibility(XrBoundaryVisibility.VisibilitySuppressed);
                Debug.Log($"QuestMenu: boundary request again (attempt {attempt + 1}) result={(int)result}");
                if ((int)result == 0) yield break;
            }
            m_boundaryRetry = null;
        }

        private void PlacePanels()
        {
            Vector3 head = m_cam.transform.localPosition;
            Vector3 fwd = Vector3.ProjectOnPlane(m_cam.transform.localRotation * Vector3.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 centre = head + fwd * PanelDistance + Vector3.down * 0.12f;
            Quaternion rot = Quaternion.LookRotation(centre - head, Vector3.up);
            m_hudRect.localPosition = centre; m_hudRect.localRotation = rot;
            m_barRect.localPosition = centre + rot * Vector3.down * (860 * PanelScale * 0.5f + 0.07f); m_barRect.localRotation = rot;
        }

        // Panels stay where they are while you look around and walk round the figure. If they end up behind you
        // (more than 100 degrees from where you look) for a second and a half, they are brought back in front.
        private void KeepInFront()
        {
            if (!m_visible) { m_behindSince = -1f; return; }
            Vector3 toPanel = m_hudRect.localPosition - m_cam.transform.localPosition; toPanel.y = 0f;
            Vector3 gaze = m_cam.transform.localRotation * Vector3.forward; gaze.y = 0f;
            bool behind = toPanel.sqrMagnitude > 1e-4f && gaze.sqrMagnitude > 1e-4f && Vector3.Angle(gaze, toPanel) > 100f;
            if (!behind) { m_behindSince = -1f; return; }
            if (m_behindSince < 0f) m_behindSince = Time.unscaledTime;
            else if (Time.unscaledTime - m_behindSince > 1.5f) { PlacePanels(); m_behindSince = -1f; }
        }

        private IEnumerator ReplaceSoon()
        {
            yield return new WaitForSeconds(1f);          // let the head pose settle
            ReplaceForViewer();
        }

        private bool m_pausedByMenu;
        private Material m_uiDepth;
        private float m_nextMaterialPass;

        private void SetVisible(bool on)
        {
            // the picture stops while the panels are up, and carries on when they go away (unless a title is loading)
            if (m_manager != null && m_manager.m_isStarted)
            {
                if (on && !m_visible && !m_manager.m_isPaused && !m_hud.KeepVisible) { m_manager.Pause(true); m_pausedByMenu = true; }
                else if (!on && m_pausedByMenu) { m_pausedByMenu = false; if (m_manager.m_isPaused) m_manager.Pause(false); }
            }
            m_visible = on;
            if (m_hud != null) m_hud.Canvas.enabled = on;
            m_barCanvas.enabled = on;
            if (on) PlacePanels();
        }

        // Panels and figure are placed where the head is. When the headset goes back on (it may have been set down
        // while the app started) or the app regains focus, bring them to where the viewer is now.
        private void ReplaceForViewer()
        {
            if (m_hud == null || m_ray == null) return;
            PlacePanels();
            m_questRig.BringFigure();
        }

        private void OnApplicationFocus(bool focus) { if (focus) ReplaceForViewer(); }

        private bool Wanted()
        {
            bool started = m_manager != null && m_manager.m_isStarted;
            return !started || m_hud.KeepVisible || Time.unscaledTime < m_shownUntil;
        }

        // asked for by the viewer: show the panels in front of them, or put them away again
        private void Summon()
        {
            bool started = m_manager != null && m_manager.m_isStarted;
            if (m_visible && started && !m_hud.KeepVisible) { m_shownUntil = 0f; return; }
            m_shownUntil = Time.unscaledTime + ShownSeconds;
            m_hud.NotifyActivity();
            if (!m_visible) SetVisible(true); else PlacePanels();
        }

        private void Update()
        {
            if (!m_ready || m_hud == null || m_ray == null) return;
            bool worn;
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            if (head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out worn))
            {
                if (worn && !m_wasWorn) StartCoroutine(ReplaceSoon());
                m_wasWorn = worn;
            }
            // the controller's device class depends on the headset (Touch, Touch Pro, Touch Plus), so controls are read by name
            var right = XRController.rightHand;
            var left = XRController.leftHand;

            // the pointer is a tracked controller, or a hand when no controller is in use (pinch clicks)
            XRController pointerHand = null;
            Vector3 pointerPosition = Vector3.zero; Quaternion pointerRotation = Quaternion.identity;
            foreach (var c in new[] { right, left })
            {
                if (c == null || !c.isTracked.isPressed) continue;
                if (Pointer(c, out pointerPosition, out pointerRotation)) { pointerHand = c; break; }
            }
            bool tracked = pointerHand != null;
            // a pinch is the thumb and index finger meeting, as the headset's own hand tracking reports it (the pinch
            // strength itself only reaches about 0.7 when the fingers touch, so no threshold on it is used)
            bool trigger = tracked && (Pressed(pointerHand, "triggerPressed") || Pressed(pointerHand, "pinchTouched"));

            // summon: X on the left controller, or a pinch (or trigger) while the panels are away; the left hand is not
            // used because the headset often does not track it while the right hand points
            bool summon = Pressed(left, "primaryButton") || (!m_visible && trigger && !m_triggerWas);
            if (summon && !m_summonWas) Summon();
            m_summonWas = summon;

            bool want = Wanted();
            if (want != m_visible) SetVisible(want);
            KeepInFront();
            AnimateControls();
            ApplyDepthMaterial();

            if (!m_visible || !tracked) { m_ray.enabled = false; SetHover(null); m_triggerWas = trigger; return; }

            Vector3 origin = m_rig.TransformPoint(pointerPosition);
            Vector3 dir = m_rig.rotation * (pointerRotation * Vector3.forward);

            Selectable best = null; float bestT = float.MaxValue;
            Probe(m_hud.Canvas, origin, dir, ref best, ref bestT);
            Probe(m_barCanvas, origin, dir, ref best, ref bestT);
            SetHover(best);

            // the ray is drawn whenever the panels are up, so it can be aimed; it ends on the panel it hits, else a metre and a half out
            m_ray.enabled = true;
            m_ray.widthMultiplier = 0.003f * m_rig.localScale.x;
            m_ray.SetPosition(0, origin);
            m_ray.SetPosition(1, origin + dir * (bestT < float.MaxValue ? bestT : 1.5f * m_rig.localScale.x));
            if (best != null) m_shownUntil = Mathf.Max(m_shownUntil, Time.unscaledTime + ShownSeconds);

            if (trigger && !m_triggerWas)
            {
                m_hud.NotifyActivity();
                Click(best);
            }
            m_triggerWas = trigger;
        }

        private void Click(Selectable target)
        {
            if (target == null) return;
            m_pressUntil = Time.unscaledTime + 0.14f;
            m_audio.PlayOneShot(m_clickClip);
            EnsureEventSystem();
            ExecuteEvents.Execute(target.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }

        private static bool Pressed(XRController c, string control)
        {
            var b = c != null ? c.TryGetChildControl<ButtonControl>(control) : null;
            return b != null && b.isPressed;
        }

        private static bool Pointer(XRController c, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero; rotation = Quaternion.identity;
            var p = c.TryGetChildControl<Vector3Control>("pointerPosition");
            var r = c.TryGetChildControl<QuaternionControl>("pointerRotation");
            if (p == null || r == null) return false;
            position = p.ReadValue(); rotation = r.ReadValue();
            return true;
        }

        // Finds the control under the ray on one canvas: the last one drawn wins where controls overlap
        private static void Probe(Canvas canvas, Vector3 origin, Vector3 dir, ref Selectable best, ref float bestT)
        {
            if (canvas == null || !canvas.enabled) return;
            var tr = canvas.transform;
            Vector3 normal = tr.forward;
            float denom = Vector3.Dot(dir, normal);
            if (Mathf.Abs(denom) < 1e-4f) return;
            float t = Vector3.Dot(tr.position - origin, normal) / denom;
            if (t <= 0f || t >= bestT) return;
            Vector3 hit = origin + dir * t;
            Selectable found = null;
            foreach (var s in canvas.GetComponentsInChildren<Selectable>(false))
            {
                if (s is InputField || !Live(s)) continue;
                var rt = (RectTransform)s.transform;
                if (rt.rect.Contains(rt.InverseTransformPoint(hit))) found = s;
            }
            if (found != null) { best = found; bestT = t; }
        }

        private static bool Live(Selectable s)
        {
            if (!s.IsActive() || !s.IsInteractable()) return false;
            float alpha = 1f;
            for (var t = s.transform; t != null; t = t.parent)
            {
                var g = t.GetComponent<CanvasGroup>();
                if (g == null) continue;
                alpha *= g.alpha;
                if (!g.blocksRaycasts) return false;
                if (g.ignoreParentGroups) break;
            }
            return alpha > 0.5f;
        }

        private void SetHover(Selectable s)
        {
            if (s == m_hover) return;
            EnsureEventSystem();
            var data = new PointerEventData(EventSystem.current);
            if (m_hover != null) ExecuteEvents.Execute(m_hover.gameObject, data, ExecuteEvents.pointerExitHandler);
            m_hover = s;
            if (m_hover != null)
            {
                ExecuteEvents.Execute(m_hover.gameObject, data, ExecuteEvents.pointerEnterHandler);
                m_hud.NotifyActivity();
                m_audio.PlayOneShot(m_tickClip);
                if (!m_fx.ContainsKey(m_hover)) m_fx[m_hover] = MakeFx(m_hover);
            }
        }

        private static Fx MakeFx(Selectable s)
        {
            var fx = new Fx { rect = (RectTransform)s.transform };
            fx.scale = fx.rect.localScale; fx.position = fx.rect.localPosition;
            var target = s.targetGraphic as Image;
            if (target != null)
            {
                var go = new GameObject("Glow", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(fx.rect, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                fx.glow = go.AddComponent<Image>();
                fx.glow.sprite = target.sprite; fx.glow.type = target.type;
                fx.glow.pixelsPerUnitMultiplier = target.pixelsPerUnitMultiplier;
                fx.glow.color = new Color(1f, 1f, 1f, 0f);
                fx.glow.raycastTarget = false;
            }
            return fx;
        }

        // eases each control toward its pointed-at look, and back again once the pointer leaves
        private void AnimateControls()
        {
            if (m_fx.Count == 0) return;
            List<Selectable> done = null;
            bool pressed = Time.unscaledTime < m_pressUntil;
            foreach (var pair in m_fx)
            {
                var fx = pair.Value;
                if (fx.rect == null) { (done ?? (done = new List<Selectable>())).Add(pair.Key); continue; }
                float target = pair.Key == m_hover ? 1f : 0f;
                fx.amount = Mathf.MoveTowards(fx.amount, target, Time.unscaledDeltaTime * 9f);
                float press = pair.Key == m_hover && pressed ? 0.93f : 1f;
                fx.rect.localScale = fx.scale * ((1f + 0.07f * fx.amount) * press);
                fx.rect.localPosition = fx.position + new Vector3(0f, 0f, -16f * fx.amount);
                if (fx.glow != null) fx.glow.color = new Color(1f, 1f, 1f, 0.22f * fx.amount * (pressed && pair.Key == m_hover ? 1.6f : 1f));
                if (fx.amount <= 0f && target <= 0f) (done ?? (done = new List<Selectable>())).Add(pair.Key);
            }
            if (done != null)
                foreach (var key in done)
                {
                    var fx = m_fx[key];
                    if (fx.rect != null) { fx.rect.localScale = fx.scale; fx.rect.localPosition = fx.position; }
                    if (fx.glow != null) Destroy(fx.glow.gameObject);
                    m_fx.Remove(key);
                }
        }

        private static AudioClip Tone(float frequency, float seconds, float gain)
        {
            const int Rate = 44100;
            int n = Mathf.RoundToInt(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / Rate) * Mathf.Exp(-4f * i / n) * gain;
            var clip = AudioClip.Create("tone", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null) new GameObject("EventSystem").AddComponent<EventSystem>();
        }
    }
}
