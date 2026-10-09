/*
 * License: 5G-MAG Public License (v1.0)
 * Author: Jordi J. Gimenez (5G-MAG)
 * Copyright: (C) 2026 5G-MAG Association
 *
 * For full license terms please see the LICENSE file distributed with this
 * program. If this file is missing then the license can be retrieved from
 * https://www.5g-mag.com/license.
 */

using UnityEngine;

namespace IDCC.V3CDecoder.Quest
{
    // Multiview (MIV) titles on a headset: the package's stereo renderer draws one picture per eye onto a window
    // in the room. The phone renderer (a full-screen overlay) cannot be seen in a headset, so it is switched off.
    public class QuestMiv : MonoBehaviour
    {
        private const float WindowDistance = 1.2f;      // metres from the viewer to the window

        private Transform m_rig, m_window;
        private Camera m_cam;
        private V3CDecoderManager m_manager;
        private bool m_active;

        public void Init(Transform rig, Camera cam, V3CDecoderManager manager)
        {
            m_rig = rig; m_cam = cam; m_manager = manager;
            foreach (var phone in FindObjectsByType<MIVRenderer>(FindObjectsSortMode.None)) phone.activate = false;

            m_window = new GameObject("MivWindow").transform;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(m_window, false);
            quad.GetComponent<MeshRenderer>().enabled = false;
            new GameObject("TV 1").transform.SetParent(quad.transform, false);     // the stereo renderer looks for this child

            var shader = Shader.Find("V3C/MivStereo");
            if (shader == null) { Debug.LogError("QuestMiv: the stereo shader is missing from the build"); return; }

            // the renderer reads its references in Awake, so it is created switched off and filled in first
            var host = new GameObject("MIVRendererStereo");
            host.SetActive(false);
            var renderer = host.AddComponent<MIVRendererStereo>();
            renderer.m_V3CDecoderManager = manager;
            renderer.m_quadTarget = quad.GetComponent<MeshRenderer>();
            renderer.m_renderMaterial = new Material(shader);
            host.SetActive(true);

            manager.OnMediaReady += OnMediaReady;
        }

        private void OnDestroy()
        {
            if (m_manager != null) m_manager.OnMediaReady -= OnMediaReady;
        }

        private void OnMediaReady(V3CDecoderManager.V3CRenderData data)
        {
            m_active = data.m_mediaType == DecoderPluginInterface.MediaType.MIV;
            if (m_active) Place();
        }

        public bool Active => m_active;

        // The synthesis takes the eye positions as they are in the tracking space and the window as it is in the
        // scene, so the rig is put back to scale 1 at the origin, and the window is set in front of the viewer.
        public void Place()
        {
            m_rig.localScale = Vector3.one;
            m_rig.rotation = Quaternion.identity;
            m_rig.position = Vector3.zero;

            Vector3 head = m_cam.transform.localPosition;
            Vector3 fwd = Vector3.ProjectOnPlane(m_cam.transform.localRotation * Vector3.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            m_window.position = new Vector3(head.x, head.y - 0.1f, head.z) + fwd * WindowDistance;
            m_window.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }
    }
}
