/*
* Copyright (c) 2024 InterDigital R&D France
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using UnityEngine;
using UnityEngine.UI;

namespace IDCC.V3CDecoder
{
    public class MIVRenderer : MonoBehaviour
    {
        public enum Move { Freeze, FadeOut, Free }
        public delegate void ActivateMove(Move activate);
        public static ActivateMove OnActivateMove;

        public enum TargetType { FullScreen, Window }
        public bool activate = true;

        public V3CDecoderManager m_V3CDecoderManager;

        public TargetType m_targetType;

        [Header("Fullscreen settings")]
        public RawImage m_fullscreenTarget;
        public Material m_fullscreenMaterial;

        [Header("Window settings")]
        public MeshRenderer m_quadTarget;
        public Material m_meshRenderMaterial;

        private bool isSetup = false;

        private Camera m_cam;
        private Texture2D m_tex;

        private bool m_isWindowReady = false;
        private bool m_isFullscreenReady = false;

        private float m_winDistance;
        private float m_winHeight;
        private float m_winWidth;

        private uint m_texWidth;
        private uint m_texHeight;

        private float m_baseFOV;

        private Move m_activateMove = Move.Freeze;

        private Vector3 lastPos = Vector3.zero;

        public void Awake()
        {
            m_V3CDecoderManager.OnMediaRequest += DeactivateTarget;
            m_V3CDecoderManager.OnPreMediaReady += ReinitRendering;
            m_V3CDecoderManager.OnMediaReady += SetupRendering;
            m_V3CDecoderManager.OnV3CPreRender += UpdateCamInfo;
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
            if (m_quadTarget)
            {
                m_quadTarget.gameObject.SetActive(false);
            }
            if (m_fullscreenTarget)
            {
                m_fullscreenTarget.gameObject.SetActive(false);
            }
        }

        private void ReinitRendering(V3CDecoderManager.V3CRenderData data)
        {
            if (isSetup && data.m_mediaType != DecoderPluginInterface.MediaType.MIV)
            {
                Debug.Log("MIV Renderer Reinit");
                m_V3CDecoderManager.m_renderUnityCallback = V3CDecoderManager.V3CRenderUnityCallback.OnPreRender;

                //In fullscreen, we modify the cam FOV directly, and we need to reset that.
                if (m_isFullscreenReady) m_cam.fieldOfView = m_baseFOV;

                Destroy(m_tex);

                isSetup = false;
                //m_isWindowReady = false;
                if (m_quadTarget)
                {
                    m_quadTarget.gameObject.SetActive(false);
                }
                if (m_fullscreenTarget)
                {
                    m_fullscreenTarget.gameObject.SetActive(false);
                }
            }
        }

        private void SetupRendering(V3CDecoderManager.V3CRenderData data)
        {
            if (activate && data.m_mediaType == DecoderPluginInterface.MediaType.MIV)
            {
                Debug.Log("MIV Renderer Setup");

                m_V3CDecoderManager.m_renderUnityCallback = V3CDecoderManager.V3CRenderUnityCallback.OnPreRender;
                m_cam = data.m_mainCamera;

                m_texWidth = 1920;
                m_texHeight = 1080;

                if (!m_tex)
                {
                    Debug.Log("MIV Create Texture");
                    m_tex = new Texture2D((int)m_texWidth, (int)m_texHeight, TextureFormat.RGBA32, false);
                    DecoderPluginInterface.SetCanvasProperties(m_tex.GetNativeTexturePtr(), m_texWidth, m_texHeight, (uint)TextureFormat.RGBA32);
                    DecoderPluginInterface.UpdateNumberOfJobs(1);
                    DecoderPluginInterface.UpdateViewport(0, m_texWidth, m_texHeight, 0, 0);
                    DecoderPluginInterface.UpdateCameraResolution(0, m_texWidth, m_texHeight);
                }
                else
                {
                    Debug.Log("MIV Texture Reuse");
                }

                if (m_targetType == TargetType.FullScreen && m_fullscreenTarget)
                {
                    m_fullscreenTarget.material = m_fullscreenMaterial;
                    m_fullscreenMaterial.mainTexture = m_tex;
                    m_fullscreenTarget.gameObject.SetActive(true);
                    SetupFullscreen(data);

                }
                else if (m_targetType == TargetType.Window && m_quadTarget)
                {
                    m_quadTarget.material = m_meshRenderMaterial;
                    m_meshRenderMaterial.mainTexture = m_tex;
                    m_quadTarget.gameObject.SetActive(true);
                    SetupWindow(data);
                }

                isSetup = true;
            }
        }

        private void SetupFullscreen(V3CDecoderManager.V3CRenderData data)
        {
            Debug.Log("Setup Fullscreen");

            DecoderPluginInterface.UpdateCameraProjection(0, DecoderPluginInterface.GetReferenceCameraType());
            DecoderPluginInterface.UpdateCameraResolution(0, m_texWidth, m_texHeight);

            m_baseFOV = m_cam.fieldOfView;
            m_cam.fieldOfView = DecoderPluginInterface.GetReferenceCameraVerticalFoV() * Mathf.Rad2Deg;

            m_isFullscreenReady = true;
        }

        private void SetupWindow(V3CDecoderManager.V3CRenderData data)
        {
            Debug.Log("Setup Window");

            // Media info 
            float aspectRatio = DecoderPluginInterface.GetReferenceCameraAspectRatio();
            float verticalFoV = DecoderPluginInterface.GetReferenceCameraVerticalFoV();

            float zMin = 0.0F, zMax = 0.0F;

            DecoderPluginInterface.GetReferenceCameraClippingRange(ref zMin, ref zMax);

            m_winDistance = zMin;
            m_winHeight = 2.0F * zMin * Mathf.Tan(verticalFoV / 2.0F);
            m_winWidth = m_winHeight * aspectRatio;

            m_quadTarget.transform.localScale = new Vector3(m_winWidth, m_winHeight, 1);

            // Update job
            DecoderPluginInterface.UpdateViewport(0, m_texWidth, m_texHeight, 0, 0);
            DecoderPluginInterface.UpdateCameraProjection(0, (uint)DecoderPluginInterface.CameraType.Perspective);
            DecoderPluginInterface.UpdateCameraResolution(0, m_texWidth, m_texHeight);

            m_isWindowReady = true;
        }

        private void UpdateCamInfo()
        {
            if (activate && isSetup)
            {
                if (m_activateMove == Move.Freeze && !DecoderPluginInterface.IsViewingSpaceCameraIn(m_cam.transform.position.x, m_cam.transform.position.y, m_cam.transform.position.z))
                {
                    m_cam.transform.position = lastPos;
                    return;
                }

                //Debug.LogWarning("Cam position: " + m_cam.transform.position);

                Material mat = null;
                if (m_isWindowReady)
                {
                    var scale = m_quadTarget.transform.localScale;
                    m_quadTarget.transform.localScale = Vector3.one;
                    var ref0 = m_quadTarget.worldToLocalMatrix;
                    m_quadTarget.transform.localScale = scale;

                    Vector3 dist = ref0 * m_quadTarget.transform.position - ref0 * m_cam.transform.position;

                    float ppm = m_texWidth / m_winWidth;
                    float focal = dist.z * ppm;
                    float cx = (0.5F * m_winWidth - dist.x) * ppm;
                    float cy = (0.5F * m_winHeight + dist.y) * ppm;

                    DecoderPluginInterface.UpdateCameraIntrinsics(0, focal, focal, cx, cy);
                    DecoderPluginInterface.UpdateCameraExtrinsics(0, -dist.x, -dist.y, m_winDistance - dist.z, 0, 0, 0, 1);

                    mat = m_quadTarget.material;
                }
                else if (m_isFullscreenReady) //Fullscreen
                {
                    float focal = Camera.FieldOfViewToFocalLength(m_cam.fieldOfView, m_texHeight);
                    DecoderPluginInterface.UpdateCameraIntrinsics(0, focal, focal, m_texWidth / 2, m_texHeight / 2);
                    DecoderPluginInterface.UpdateCameraExtrinsics(0,
                                                m_cam.transform.position.x,
                                                m_cam.transform.position.y,
                                                m_cam.transform.position.z,
                                                m_cam.transform.rotation.x,
                                                m_cam.transform.rotation.y,
                                                m_cam.transform.rotation.z,
                                                m_cam.transform.rotation.w);

                    mat = m_fullscreenTarget.material;
                }

                mat?.SetFloat("_AlphaMask", (m_activateMove == Move.FadeOut) ? DecoderPluginInterface.GetViewingSpaceInclusion(0) : 1);

                if (DecoderPluginInterface.GetViewingSpaceInclusion(0) > 0.0f)
                {
                    // Save the last good postion
                    lastPos = m_cam.transform.position;
                }
            }
        }

        public void ToggleMove()
        {
            switch (m_activateMove)
            {
                case Move.Freeze:
                    m_activateMove = Move.FadeOut;
                    break;
                case Move.FadeOut:
                    m_activateMove = Move.Free;
                    break;
                case Move.Free:
                    m_activateMove = Move.Freeze;
                    break;
                default:
                    break;
            }
            OnActivateMove(m_activateMove);
        }
    }
}