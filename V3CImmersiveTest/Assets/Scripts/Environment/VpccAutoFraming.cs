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
using UnityEngine;
using UnityEngine.Rendering;

namespace IDCC.V3CDecoder
{
    // Frames V-PCC content from its own points. About a second after a V-PCC title is ready it reads the point
    // positions the plugin wrote, takes the 0.5% to 99.5% extent of the points on each axis (a few stray points
    // do not count), and puts the orbit pivot at the centre of that box and the camera at the distance that
    // gives the figure 55% of the view height. "Reset view" applies the same framing.
    public class VpccAutoFraming : MonoBehaviour
    {
        private const int Bins = 1024;
        private const float FillOfViewHeight = 0.55f;

        // raised once the figure's centre and size (in scene units) are known, or again for a title already measured
        public static event System.Action<Vector3, Vector3> Measured;

        private V3CDecoderManager m_manager;
        private readonly System.Collections.Generic.Dictionary<int, Known> m_known = new System.Collections.Generic.Dictionary<int, Known>();
        private class Known { public Vector3 Centre, Size; public float Distance; }
        private Coroutine m_running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<V3CDecoderManager>() == null) return;
            new GameObject("VpccAutoFraming").AddComponent<VpccAutoFraming>();
        }

        private void Start()
        {
            m_manager = FindFirstObjectByType<V3CDecoderManager>();
            m_manager.OnMediaReady += OnMediaReady;
        }

        private void OnDestroy()
        {
            if (m_manager != null) m_manager.OnMediaReady -= OnMediaReady;
        }

        private void OnMediaReady(V3CDecoderManager.V3CRenderData data)
        {
            if (m_running != null) StopCoroutine(m_running);
            m_running = null;
            if (data.m_mediaType != DecoderPluginInterface.MediaType.VPCC) return;
            Known known;
            if (m_known.TryGetValue(data.m_mediaId, out known))
            {
                // a title already measured keeps its first good measurement, so it is framed at once and the same way
                var cameraController = FindFirstObjectByType<V3CCameraController>();
                if (cameraController != null) cameraController.SetVpccFraming(known.Centre, known.Distance);
                Measured?.Invoke(known.Centre, known.Size);
                return;
            }
            m_running = StartCoroutine(Frame(data.m_mediaId));
        }

        private class Retry { public bool Again; }

        // The first frames of a title can still be empty or collapsed onto a line while the decoder warms up, which gives
        // a box with no width or depth. Such a measurement is thrown away and taken again, once a second, for as long as
        // the same title plays.
        private IEnumerator Frame(int mediaId)
        {
            for (float wait = 0.9f; ; wait = 1f)
            {
                var retry = new Retry();
                yield return FrameOnce(mediaId, wait, retry);
                if (!retry.Again) yield break;
            }
        }

        private IEnumerator FrameOnce(int mediaId, float wait, Retry retry)
        {
            var renderer = FindFirstObjectByType<VPCCRenderer>();
            var cameraController = FindFirstObjectByType<V3CCameraController>();
            if (renderer == null || cameraController == null) yield break;
            yield return new WaitForSeconds(wait);
            var tex = renderer.PointPositionTexture;
            if (tex == null || m_manager.GetRenderData().m_mediaId != mediaId) yield break;

            var request = AsyncGPUReadback.Request(tex, 0, TextureFormat.RGBAFloat);
            while (!request.done) yield return null;
            if (request.hasError || m_manager.GetRenderData().m_mediaId != mediaId) yield break;
            var points = request.GetData<Vector4>();

            Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
            int valid = 0;
            for (int i = 0; i < points.Length; i++)
            {
                if (!Usable(points[i])) continue;
                valid++;
                Vector4 p = points[i];
                mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p);
            }
            if (valid < 100) { Debug.Log($"VpccAutoFraming: only {valid} usable points, keeping the default framing"); yield break; }

            var hist = new int[3, Bins];
            for (int i = 0; i < points.Length; i++)
            {
                if (!Usable(points[i])) continue;
                Vector4 p = points[i];
                hist[0, Bin(p.x, mn.x, mx.x)]++; hist[1, Bin(p.y, mn.y, mx.y)]++; hist[2, Bin(p.z, mn.z, mx.z)]++;
            }
            var lo = new Vector3(Quantile(hist, 0, valid, 0.005f, mn.x, mx.x), Quantile(hist, 1, valid, 0.005f, mn.y, mx.y), Quantile(hist, 2, valid, 0.005f, mn.z, mx.z));
            var hi = new Vector3(Quantile(hist, 0, valid, 0.995f, mn.x, mx.x), Quantile(hist, 1, valid, 0.995f, mn.y, mx.y), Quantile(hist, 2, valid, 0.995f, mn.z, mx.z));

            // an extent of no more than one histogram bin on an axis is no extent at this resolution
            float bin = 1f / (Bins - 1);
            if (hi.x - lo.x <= (mx.x - mn.x) * bin || hi.y - lo.y <= (mx.y - mn.y) * bin || hi.z - lo.z <= (mx.z - mn.z) * bin)
            {
                Debug.Log($"VpccAutoFraming: {valid} points but no extent on every axis (from {lo} to {hi}), measuring again");
                retry.Again = true;
                yield break;
            }

            var model = renderer.ModelTransform;
            if (model == null) yield break;
            Vector3 centre = model.TransformPoint((lo + hi) * 0.5f);
            Vector3 size = Vector3.Scale(hi - lo, model.lossyScale);
            var cam = m_manager.m_mainCamera;
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float forHeight = size.y / FillOfViewHeight / (2f * tanHalf);
            float forWidth = Mathf.Max(size.x, size.z) / 0.45f / (2f * tanHalf * cam.aspect);
            float distance = Mathf.Max(Mathf.Max(forHeight, forWidth), size.z * 0.5f + 0.6f);
            Debug.Log($"VpccAutoFraming: {valid} points, size={size}, centre={centre}, distance={distance:0.00}");
            m_known[mediaId] = new Known { Centre = centre, Size = size, Distance = distance };
            cameraController.SetVpccFraming(centre, distance);
            Measured?.Invoke(centre, size);
        }

        private static bool Usable(Vector4 p)
        {
            return !(p.x == 0f && p.y == 0f && p.z == 0f) && !float.IsNaN(p.x + p.y + p.z)
                   && Mathf.Abs(p.x) < 1000f && Mathf.Abs(p.y) < 1000f && Mathf.Abs(p.z) < 1000f;
        }

        private static int Bin(float v, float mn, float mx)
        {
            return mx <= mn ? 0 : Mathf.Clamp((int)((v - mn) / (mx - mn) * (Bins - 1)), 0, Bins - 1);
        }

        private static float Quantile(int[,] hist, int axis, int total, float q, float mn, float mx)
        {
            long target = (long)(total * q), run = 0;
            for (int b = 0; b < Bins; b++)
            {
                run += hist[axis, b];
                if (run >= target) return mn + (mx - mn) * (b / (float)(Bins - 1));
            }
            return mx;
        }
    }
}
