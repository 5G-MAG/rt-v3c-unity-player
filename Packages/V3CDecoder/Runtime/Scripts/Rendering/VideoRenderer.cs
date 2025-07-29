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

using UnityEngine;
using UnityEngine.UI;

namespace IDCC.V3CDecoder
{
    public class VideoRenderer : MonoBehaviour
    {
        public enum TargetType { FullScreen, MeshRenderer, SphereRenderer }
        public bool activate = true;
        public V3CDecoderManager m_V3CDecoderManager;

        public TargetType m_targetType;
        public MeshRenderer m_quadTarget;
        public MeshRenderer m_sphereTarget;
        public RawImage m_fullscreenTarget;
        public Material m_meshRenderMaterial;
        public Material m_sphereRenderMaterial;
        public Material m_fullscreenMaterial;

        public bool m_isImmersiveMode = false;

        public float m_aspectRatio = 16.0f / 9.0f;

        private bool m_isSetup = false;

        private Texture2D m_tex;

        private float m_winHeight;
        private float m_winWidth;

        private uint m_texWidth;
        private uint m_texHeight;

        public void Awake()
        {
            m_V3CDecoderManager.OnMediaRequest += DeactivateTarget;
            m_V3CDecoderManager.OnPreMediaReady += ReinitRendering;
            m_V3CDecoderManager.OnMediaReady += SetupRendering;
        }

        public void OnDestroy()
        {
            m_V3CDecoderManager.OnMediaRequest -= DeactivateTarget;
            m_V3CDecoderManager.OnPreMediaReady -= ReinitRendering;
            m_V3CDecoderManager.OnMediaReady -= SetupRendering;
        }

        private void DeactivateTarget()
        {
            if (m_quadTarget)
            {
                m_quadTarget.gameObject.SetActive(false);
            }
            if (m_sphereTarget)
            {
                m_sphereTarget.gameObject.SetActive(false);
            }
            if (m_fullscreenTarget)
            {
                m_fullscreenTarget.gameObject.SetActive(false);
            }
        }

        private void ReinitRendering(V3CDecoderManager.V3CRenderData data)
        {
            if (m_isSetup && data.m_mediaType != DecoderPluginInterface.MediaType.Video)
            {
                m_V3CDecoderManager.m_renderUnityCallback = V3CDecoderManager.V3CRenderUnityCallback.OnPreRender;
                Destroy(m_tex);

                m_isSetup = false;
                if (m_quadTarget)
                {
                    m_quadTarget.gameObject.SetActive(false);
                }
                if (m_sphereTarget)
                {
                    m_sphereTarget.gameObject.SetActive(false);
                }
                if (m_fullscreenTarget)
                {
                    m_fullscreenTarget.gameObject.SetActive(false);
                }
            }
        }

        private void SetupRendering(V3CDecoderManager.V3CRenderData data)
        {
            if (activate && data.m_mediaType == DecoderPluginInterface.MediaType.Video)
            {
                m_V3CDecoderManager.m_renderUnityCallback = V3CDecoderManager.V3CRenderUnityCallback.OnPreRender;
                if (isShow360() && m_sphereTarget)
                {
                    m_sphereTarget.enabled = true;
                    m_texWidth = 2048;
                    m_texHeight = 1024;
                    m_targetType = TargetType.SphereRenderer;
                }
                else if (m_targetType == TargetType.MeshRenderer && m_quadTarget)
                {
                    m_quadTarget.enabled = true;
                    m_texWidth = 1920;
                    m_texHeight = 1080;
                    m_targetType = TargetType.MeshRenderer;
                }
                else if (m_targetType == TargetType.FullScreen && m_fullscreenTarget)
                {
                    m_fullscreenTarget.enabled = true;
                    m_texWidth = 1920;
                    m_texHeight = 1080;
                    m_targetType = TargetType.FullScreen;
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

                m_tex = new Texture2D((int)m_texWidth, (int)m_texHeight, TextureFormat.RGBA32, false);
                DecoderPluginInterface.SetCanvasProperties(m_tex.GetNativeTexturePtr(), m_texWidth, m_texHeight, (uint)TextureFormat.RGBA32);
                DecoderPluginInterface.UpdateNumberOfJobs(1);
                DecoderPluginInterface.UpdateViewport(0, m_texWidth, m_texHeight, 0, 0);
                DecoderPluginInterface.UpdateCameraResolution(0, m_texWidth, m_texHeight);


                if (m_targetType == TargetType.FullScreen && m_fullscreenTarget)
                {
                    m_fullscreenTarget.material = m_fullscreenMaterial;
                    m_fullscreenMaterial.mainTexture = m_tex;
                    m_fullscreenTarget.gameObject.SetActive(true);
                }
                else if (m_targetType == TargetType.MeshRenderer && m_quadTarget)
                {
                    m_quadTarget.material = m_meshRenderMaterial;
                    m_meshRenderMaterial.mainTexture = m_tex;
                    m_quadTarget.gameObject.SetActive(true);
                    SetupWindow(data);
                }
                else if (m_targetType == TargetType.SphereRenderer && m_sphereTarget)
                {
                    m_sphereTarget.material = m_sphereRenderMaterial;
                    m_sphereRenderMaterial.mainTexture = m_tex;
                    m_sphereTarget.gameObject.SetActive(true);
                    SetupWindow(data);
                }

                m_isSetup = true;
            }
        }

        private void SetupWindow(V3CDecoderManager.V3CRenderData data)
        {
            m_winHeight = m_quadTarget.transform.localScale.y;
            m_winWidth = m_winHeight * m_aspectRatio;

            m_quadTarget.transform.localScale = new Vector3(m_winWidth, m_winHeight, 1);
        }
        private bool isShow360()
        {
            return m_isImmersiveMode;
        }
    }
}