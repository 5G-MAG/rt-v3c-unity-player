/*
* Copyright (c) 2025 InterDigital CE Patent Holdings SASU
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace IDCC.V3CDecoder
{
    public class MIVRendererStereo : MonoBehaviour
    {
        public event Action OnWindowReady = delegate { };

        public V3CDecoderManager m_V3CDecoderManager;
        public MeshRenderer m_quadTarget;
        public Material m_renderMaterial;

        public bool m_isParallax = true;
        public bool m_isStereo = true;
        public bool m_isImmersiveMode = false;
        // This field should be m_cam.fieldOfView but the real fov is bigger
        // than that :-/ Still wondering why
        public float m_realFieldOfView = 110.0f;

        private int m_nbViews = 2;

        private bool m_isSetup = false;
        private bool m_isWindowReady = false;
        private bool m_isFullscreenReady = false;

        private Transform m_initParent;

        private List<Pose> m_camPoses;
        private Pose m_leftEyePose;
        private Pose m_rightEyePose;
        private Pose m_headPose;
        private Camera m_cam;
        private Texture2D m_tex;

        private float m_winDistance;
        private float m_refHeight;
        private float m_refWidth;

        private uint m_texWidth;
        private uint m_texHeight;

        //private float m_baseFOV;

        private Vector3 m_viewDist;

        private float m_initialHeight = 1.1173f;

        public void Awake()
        {
            m_V3CDecoderManager.OnMediaRequest += DeactivateTarget;
            m_V3CDecoderManager.OnPreMediaReady += ReinitRendering;
            m_V3CDecoderManager.OnMediaReady += SetupRendering;
            m_V3CDecoderManager.OnV3CPreRender += UpdateCamInfo;

            m_camPoses = new List<Pose>();

            m_initParent = m_quadTarget.transform.parent;
        }

        public void OnDestroy()
        {
            m_V3CDecoderManager.OnMediaRequest -= DeactivateTarget;
            m_V3CDecoderManager.OnPreMediaReady -= ReinitRendering;
            m_V3CDecoderManager.OnMediaReady -= SetupRendering;
            m_V3CDecoderManager.OnV3CPreRender -= UpdateCamInfo;
        }

        private void DeactivateTarget()
        {
            m_quadTarget.enabled = false;
        }

        private void ReinitRendering(V3CDecoderManager.V3CRenderData data)
        {
            m_isWindowReady = false;
            m_isFullscreenReady = false;
            m_isImmersiveMode = false;
            if (m_isSetup && data.m_mediaType != DecoderPluginInterface.MediaType.MIV)
            {
                m_V3CDecoderManager.m_renderUnityCallback = V3CDecoderManager.V3CRenderUnityCallback.OnPreRender;

                Destroy(m_tex);

                m_isSetup = false;
                m_isWindowReady = false;
                m_isFullscreenReady = false;
                m_quadTarget.gameObject.SetActive(false);
            }
        }

        private void SetupRendering(V3CDecoderManager.V3CRenderData data)
        {
            if (data.m_mediaType == DecoderPluginInterface.MediaType.MIV)
            {
                m_V3CDecoderManager.m_renderUnityCallback = V3CDecoderManager.V3CRenderUnityCallback.OnPreRender;
                m_quadTarget.enabled = true;

                m_cam = data.m_mainCamera;

                if (isShow360())
                {
                    m_texWidth = (uint)m_cam.pixelWidth;
                    m_texHeight = (uint)m_cam.pixelHeight;
                }
                else
                {
                    m_texWidth = 1920;
                    m_texHeight = 1080;
                }

                if (m_tex)
                {
                    Destroy(m_tex);
                }

                m_tex = new Texture2D((int)m_texWidth * m_nbViews, (int)m_texHeight, TextureFormat.RGBA32, false);
                DecoderPluginInterface.SetCanvasProperties(m_tex.GetNativeTexturePtr(), m_texWidth, m_texHeight, (uint)TextureFormat.RGBA32);

                m_quadTarget.material = m_renderMaterial;
                m_renderMaterial.mainTexture = m_tex;
                m_quadTarget.gameObject.SetActive(true);
                if (isShow360())
                {
                    // Disable environment
                    Transform tv1 = m_quadTarget.transform.Find("TV 1");
                    tv1.gameObject.SetActive(false);

                    SetupFullscreen(/*data*/);
                }
                else
                {
                    Transform tv1 = m_quadTarget.transform.Find("TV 1");
                    tv1.gameObject.SetActive(true);

                    SetupWindow(/*data*/);
                }
                m_isSetup = true;
            }
        }

        private void SetupFullscreen(/*V3CDecoderManager.V3CRenderData data*/)
        {
            DecoderPluginInterface.UpdateNumberOfJobs((uint)m_nbViews);
            for (uint i = 0; i < m_nbViews; i++)
            {
                DecoderPluginInterface.UpdateViewport(i, m_texWidth, m_texHeight, m_texWidth * i, 0);
                DecoderPluginInterface.UpdateCameraProjection(i, DecoderPluginInterface.GetReferenceCameraType());
                DecoderPluginInterface.UpdateCameraResolution(i, m_texWidth, m_texHeight);
            }

            float zMin = 0.0F, zMax = 0.0F;
            DecoderPluginInterface.GetReferenceCameraClippingRange(ref zMin, ref zMax);

            m_winDistance = zMin;
            m_quadTarget.transform.localPosition = Vector3.zero;
            m_quadTarget.transform.localRotation = Quaternion.identity;

            m_isFullscreenReady = true;

            InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (device.isValid)
            {
                Vector3 headPosition;
                if (device.TryGetFeatureValue(CommonUsages.devicePosition, out headPosition))
                {
                    m_initialHeight = headPosition.y;
                }
            }
        }

        private void SetupWindow(/*V3CDecoderManager.V3CRenderData data*/)
        {
            // Media info 
            float aspectRatio = (float)m_texWidth / (float)m_texHeight;
            float verticalFoV = DecoderPluginInterface.GetReferenceCameraVerticalFoV();

            float zMin = 0.0F, zMax = 0.0F;

            DecoderPluginInterface.GetReferenceCameraClippingRange(ref zMin, ref zMax);

            m_winDistance = zMin;
            m_refHeight = 2.0F * zMin * Mathf.Tan(verticalFoV / 2.0F);
            m_refWidth = m_refHeight * aspectRatio;

            m_quadTarget.transform.parent = m_initParent;

            m_quadTarget.transform.localPosition = Vector3.zero;
            m_quadTarget.transform.localRotation = Quaternion.identity;
            m_quadTarget.transform.localScale = new Vector3(m_refWidth, m_refHeight, 1);

            // Update job
            DecoderPluginInterface.UpdateNumberOfJobs((uint)m_nbViews);
            for (uint i = 0; i < m_nbViews; i++)
            {
                DecoderPluginInterface.UpdateViewport(i, m_texWidth, m_texHeight, m_texWidth * i, 0);
                DecoderPluginInterface.UpdateCameraProjection(i, (uint)DecoderPluginInterface.CameraType.Perspective);
                DecoderPluginInterface.UpdateCameraResolution(i, m_texWidth, m_texHeight);
            }

            m_isWindowReady = true;
            OnWindowReady();
        }

        private void UpdateCamInfo()
        {
            if (m_isWindowReady || m_isFullscreenReady)
            {
                //Fetch the eye positions
                m_leftEyePose = new Pose();
                m_rightEyePose = new Pose();
                m_headPose = new Pose();

                m_leftEyePose.position = Vector3.zero;
                m_rightEyePose.position = Vector3.zero;
                m_headPose.position = Vector3.zero;
                m_leftEyePose.rotation = Quaternion.identity;
                m_rightEyePose.rotation = Quaternion.identity;
                m_headPose.rotation = Quaternion.identity;

                InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (device.isValid)
                {
                    Quaternion leftRotation, rightRotation;
                    if (device.TryGetFeatureValue(CommonUsages.leftEyeRotation, out leftRotation))
                    {
                        m_leftEyePose.rotation = leftRotation;
                    }
                    if (device.TryGetFeatureValue(CommonUsages.rightEyeRotation, out rightRotation))
                    {
                        m_rightEyePose.rotation = rightRotation;
                    }
                    Quaternion headRotation;
                    if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out headRotation))
                    {
                        m_headPose.rotation = headRotation;
                    }
                    if (m_isParallax)
                    {
                        Vector3 leftPosition, rightPosition;
                        if (device.TryGetFeatureValue(CommonUsages.leftEyePosition, out leftPosition))
                        {
                            m_leftEyePose.position = leftPosition;
                        }
                        if (device.TryGetFeatureValue(CommonUsages.rightEyePosition, out rightPosition))
                        {
                            m_rightEyePose.position = rightPosition;
                        }
                        Vector3 headPosition;
                        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out headPosition))
                        {
                            m_headPose.position = headPosition;
                        }
                    }
                }

                if (Application.isEditor)
                {
                    var t = Camera.main.transform;
                    m_leftEyePose = new Pose(t.position, t.rotation);
                    m_rightEyePose = new Pose(t.position, t.rotation);
                    m_headPose = new Pose(t.position, t.rotation);
                }

                if (m_isStereo)
                {
                    m_camPoses.Add(m_leftEyePose);
                    m_camPoses.Add(m_rightEyePose);
                }
                else
                {
                    m_camPoses.Add(m_headPose);
                    m_camPoses.Add(m_headPose);
                }

                ComputeCameraParams();
                m_camPoses.Clear();
            }
        }

        private void ComputeCameraParams()
        {
            if (isShow360())
            {
                m_quadTarget.transform.parent = m_cam.transform;
                m_quadTarget.transform.localPosition = new Vector3(0, 0, m_winDistance);
                m_quadTarget.transform.localRotation = Quaternion.identity;

                float verticalFoV = m_realFieldOfView * Mathf.Deg2Rad;
                float scaleY = 2f * m_winDistance * Mathf.Tan(verticalFoV * 0.5f);
                float scaleX = scaleY * ((float)m_texWidth / (float)m_texHeight);
                m_quadTarget.transform.localScale = new Vector3(scaleX, scaleY, 1.0f);

                float focal = Camera.FieldOfViewToFocalLength(m_realFieldOfView, m_texHeight);

                for (int i = 0; i < m_nbViews; i++)
                {
                    float h = (m_isParallax) ? m_initialHeight : 0;

                    DecoderPluginInterface.UpdateCameraIntrinsics((uint)i, focal, focal, m_texWidth / 2, m_texHeight / 2);
                    DecoderPluginInterface.UpdateCameraExtrinsics((uint)i,
                                                                m_camPoses[i].position.x,
#if UNITY_EDITOR
                                                                m_camPoses[i].position.y,
#else
                                                                m_camPoses[i].position.y - h,
#endif
                                                                m_camPoses[i].position.z,
                                                                m_camPoses[i].rotation.x,
                                                                m_camPoses[i].rotation.y,
                                                                m_camPoses[i].rotation.z,
                                                                m_camPoses[i].rotation.w);
                }
            }
            else
            {
                //To compute the MIV cameras position relative to the target quad, we need a good referential to work with.
                //We'll use the target Model referential, as it's the most straightforward for our computations.

                //We need to get an orthonormal referential, so we ignore the scale for our tranform matrix.
                //This also ensure it will work for a target at any scale
                var scale = m_quadTarget.transform.localScale;
                m_quadTarget.transform.localScale = Vector3.one;
                var ref0 = m_quadTarget.worldToLocalMatrix;

                //Set back scale to keep aspect ratio for the model.
                m_quadTarget.transform.localScale = scale;

                for (int i = 0; i < m_nbViews; i++)
                {
                    if (m_isParallax)
                    {
                        m_viewDist = ref0 * m_quadTarget.transform.position - ref0 * m_camPoses[i].position;
                    }

                    float ppm = m_texWidth / m_refWidth;
                    float focal = m_viewDist.z* ppm;

                    //This work without updating the width and height because we are computing the distance in an unscaled referential
                    //(so when compensating for a bigger scale the distance ends up smaller, and vice versa, which is what we need)
                    float cx = (0.5F * m_refWidth - m_viewDist.x) * ppm;
                    float cy = (0.5F * m_refHeight + m_viewDist.y) * ppm;

                    DecoderPluginInterface.UpdateCameraIntrinsics((uint)i, focal, focal, cx, cy);
                    DecoderPluginInterface.UpdateCameraExtrinsics((uint)i, -m_viewDist.x, -m_viewDist.y, m_winDistance - m_viewDist.z, 0, 0, 0, 1);
                }
            }
        }

        public void MivViewSwitcher()
        {
            m_isParallax = !m_isParallax;
            m_isStereo = !m_isStereo;
        }

        public void Miv360Switcher()
        {
            m_isImmersiveMode = !m_isImmersiveMode;
            SetupRendering(m_V3CDecoderManager.GetRenderData());
        }

        private bool isShow360()
        {
            return m_isImmersiveMode && (DecoderPluginInterface.GetReferenceCameraVerticalFoV() == (0.5f * Mathf.PI));
        }
    }
}


