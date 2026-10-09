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
using UnityEngine.SpatialTracking;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace IDCC.V3CDecoder.Quest
{
    // Makes the player run in a headset: the camera is moved under a rig that sits on the floor, the headset drives the
    // camera's pose, and a V-PCC figure is shown life-size in front of the viewer. Does nothing unless the project was set
    // up for a headset (QuestSetup), so phone builds are not affected.
    public class QuestRig : MonoBehaviour
    {
        private const float LifeSizeHeight = 1.75f;     // metres the figure is shown at
        private const float ViewingDistance = 1.0f;     // metres in front of the viewer, close enough to walk round it
        private const float WalkSpeed = 1.6f;           // metres per second at full stick
        private const float SnapTurnDegrees = 30f;

        private Transform m_rig;
        private Camera m_cam;
        private QuestMiv m_miv;
        private Vector3 m_figureCentre, m_figureSize;
        private bool m_hasFigure, m_turnReady = true, m_recentreReady = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var xr = XRGeneralSettings.Instance;
            if (xr == null || xr.Manager == null || xr.Manager.activeLoader == null) return;
            if (FindFirstObjectByType<V3CDecoderManager>() == null) return;
            new GameObject("QuestRig").AddComponent<QuestRig>();
        }

        private IEnumerator Start()
        {
            // the headset's session only becomes active once it is worn, which may be after the app has started
            while (!XRSettings.isDeviceActive) yield return new WaitForSeconds(0.5f);

            var manager = FindFirstObjectByType<V3CDecoderManager>();
            m_cam = manager.m_mainCamera != null ? manager.m_mainCamera : Camera.main;

            m_rig = new GameObject("XRRig").transform;
            m_cam.transform.SetParent(m_rig, false);
            gameObject.AddComponent<QuestMenu>().Init(m_rig, m_cam, this);
            m_miv = gameObject.AddComponent<QuestMiv>();
            m_miv.Init(m_rig, m_cam, manager);
            var driver = m_cam.gameObject.AddComponent<TrackedPoseDriver>();
            driver.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.Center);
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);

            // the project renders in linear colour space on a headset: tell the V-PCC renderer to correct its colours
            foreach (var r in FindObjectsByType<VPCCRenderer>(FindObjectsSortMode.None))
                r.m_QuestColorCorrection = QualitySettings.activeColorSpace == ColorSpace.Linear;

            // the phone vibrator path makes a native call every frame and finds no vibrator service on a headset
            foreach (var h in FindObjectsByType<HapticManagerAndroid>(FindObjectsSortMode.None)) h.enabled = false;

            VpccAutoFraming.Measured += PlaceLifeSize;
            Debug.Log($"QuestRig: {XRSettings.loadedDeviceName}, eye {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight} at {XRDevice.refreshRate} Hz");
        }

        private void OnDestroy() { VpccAutoFraming.Measured -= PlaceLifeSize; }

        // The rig is scaled down so the figure, which is a fraction of a metre tall in scene units, appears life-size.
        // The figure stays where it is; the rig is placed so that the figure stands on the floor ViewingDistance in front
        // of where the viewer's head is, so the viewer can walk round it in the room.
        private void PlaceLifeSize(Vector3 centre, Vector3 size)
        {
            if (size.y < 0.01f) return;
            m_figureCentre = centre; m_figureSize = size; m_hasFigure = true;
            BringFigure();
        }

        // Puts the figure back in front of the viewer, on the floor
        public void BringFigure()
        {
            if (m_cam == null) return;
            if (m_miv != null && m_miv.Active) { m_miv.Place(); return; }
            if (!m_hasFigure) return;
            float s = m_figureSize.y / LifeSizeHeight;
            Vector3 head = m_cam.transform.localPosition;
            Vector3 flatForward = Vector3.ProjectOnPlane(m_cam.transform.localRotation * Vector3.forward, Vector3.up).normalized;
            Vector3 inFront = new Vector3(head.x, 0f, head.z) + flatForward * ViewingDistance;      // a point in the tracking space
            Vector3 foot = new Vector3(m_figureCentre.x, m_figureCentre.y - m_figureSize.y * 0.5f, m_figureCentre.z);
            m_rig.localScale = Vector3.one * s;
            m_rig.rotation = Quaternion.identity;
            m_rig.position = foot - inFront * s;
        }

        // Thumbsticks move the viewer beyond the physical play space: the left stick walks in the direction the head
        // faces, the right stick turns in steps (steps are easier on the stomach than a smooth turn). The A button brings
        // the figure back in front of the viewer.
        private void Update()
        {
            if (m_rig == null || m_cam == null) return;

            bool a = false;
            var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid) rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out a);
            if (a && m_recentreReady) BringFigure();
            m_recentreReady = !a;

            Vector2 move, turn;
            Stick(XRNode.LeftHand, out move);
            Stick(XRNode.RightHand, out turn);
            if (move.sqrMagnitude > 0.04f)
            {
                Vector3 forward = Vector3.ProjectOnPlane(m_cam.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(m_cam.transform.right, Vector3.up).normalized;
                m_rig.position += (forward * move.y + right * move.x) * (WalkSpeed * m_rig.localScale.x * Time.unscaledDeltaTime);
            }
            if (Mathf.Abs(turn.x) > 0.7f)
            {
                if (m_turnReady) m_rig.RotateAround(m_cam.transform.position, Vector3.up, Mathf.Sign(turn.x) * SnapTurnDegrees);
                m_turnReady = false;
            }
            else if (Mathf.Abs(turn.x) < 0.3f) m_turnReady = true;
        }

        private static bool Stick(XRNode node, out Vector2 value)
        {
            value = Vector2.zero;
            var device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out value);
        }
    }
}
