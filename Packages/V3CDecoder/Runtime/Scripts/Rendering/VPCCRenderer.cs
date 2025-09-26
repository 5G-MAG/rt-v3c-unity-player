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
using UnityEngine;
using UnityEngine.Rendering;

namespace IDCC.V3CDecoder
{
    public class VPCCRenderer: MonoBehaviour
    {
        public enum Decimation { None, Two, Four };

        public enum RenderingMode { GLPoints, SortedBlend}

        public bool m_activate = true;
        public V3CDecoderManager m_V3CDecoderManager;
        public Transform m_VPCCModelTransform;
        public PointCloudSorter m_PointCloudSorter;

        [Header("Point Cloud Rendering")]
        public RenderingMode m_renderMode = RenderingMode.GLPoints;
        public Decimation m_decimationLevel = Decimation.None;
        private int m_decimationValue = 1;
        public float m_maxBbox = 1024.0f;
        public bool m_QuestColorCorrection = false;

        [Header("Dynamic Decimation")]
        public bool m_useDynamicDecimation = false;
        public float m_r1=15;
        public float m_r2=30;
        public float m_viewportCullThreshold;
        public bool m_showRanges = false;

        [Header("Rendering params")]
        public Material m_mat;
        public float m_pointSize = 2;

        [Header("Sorted Rendering params")]
        public Material m_sortedMat;
        public ComputeShader m_sortingCompute;
        [Tooltip("Number of slice to sort the points into")]
        public int m_sortingRange;
        public float m_sortedPointSize = 2;
        [Tooltip("Points are drawn as round splats with a gaussian alpha, this is 1 / variance")]
        public float m_pointAlphaFalloff = 8;

        [Header("Shadow Options")]
        public bool m_showShadow = true;
        public MeshRenderer m_ShadowTargetMesh;
        public Material m_shadowMat;
        public int m_shadowResolution = 512;
        public Color m_shadowColor;

        [Header("Stream Info")]
        public bool m_logNumPoints = false;

        [Header("Force GPU/CPU synchro\nFix issue on quest3 passthrough where the model won't stay in place")]
        public bool m_useQuestStabilityHack = false;        
        
        private RenderParams m_sortedRP;
        private bool m_useSort;

        //Textures
        private RenderTexture m_v3cPointColTex;
        private RenderTexture m_v3cPointPosTex;
        private RenderTexture m_v3cShadowTex;

        //State
        private bool m_texAllocFlag = false;
        private bool m_isSetup = false;

        //Shadow
        private Vector3 m_ShadowDefaultPosition;

        //Rendering setup
        private GraphicsBuffer m_IndirectArgs;
        private RenderParams m_rp;
        private Camera m_render_cam;
        private int m_numVertPerPoint = 1;

        public void Awake()
        {
            m_V3CDecoderManager.OnMediaRequest += HideShadow;
            m_V3CDecoderManager.OnPreMediaReady += ReinitRendering;
            m_V3CDecoderManager.OnMediaReady += SetupRendering;
            m_V3CDecoderManager.OnV3CPreRender += UpdateVertexPerPoint;
            m_V3CDecoderManager.OnV3CPreRender += ResizeTex;
            m_V3CDecoderManager.OnV3CPreRender += UpdateCamera;
            m_V3CDecoderManager.OnV3CPostRender += Draw;
        }

        public void OnDestroy()
        {
            ReleaseTextures();

            m_V3CDecoderManager.OnMediaRequest -= HideShadow;
            m_V3CDecoderManager.OnPreMediaReady -= ReinitRendering;
            m_V3CDecoderManager.OnMediaReady -= SetupRendering;
            m_V3CDecoderManager.OnV3CPreRender -= UpdateVertexPerPoint;
            m_V3CDecoderManager.OnV3CPreRender -= ResizeTex;
            m_V3CDecoderManager.OnV3CPreRender -= UpdateCamera;
            m_V3CDecoderManager.OnV3CPostRender -= Draw;
        }

        private void HideShadow()
        {
            m_ShadowTargetMesh.gameObject.SetActive(false);
        }

        private void ReinitRendering(V3CDecoderManager.V3CRenderData data)
        {
            if (m_isSetup && data.m_mediaType != DecoderPluginInterface.MediaType.VPCC)
            {
                //Reset the callbacks and canvas
                m_V3CDecoderManager.m_renderUnityCallback = V3CDecoderManager.V3CRenderUnityCallback.OnPostRender;
                m_isSetup = false;
            }
        }

        private void SetupRendering(V3CDecoderManager.V3CRenderData data)
        {
            //Setup the camera for VPCC rendering
            if (m_activate && data.m_mediaType == DecoderPluginInterface.MediaType.VPCC)
            {
                if (m_texAllocFlag)
                {
                    int height = DecoderPluginInterface.GetAtlasFrameHeight();
                    int width = DecoderPluginInterface.GetAtlasFrameWidth();
                    UpdateSize(height, width);
                    m_PointCloudSorter?.Reinit(m_v3cPointPosTex);
                }


                if (!m_texAllocFlag)
                {
                    int height = DecoderPluginInterface.GetAtlasFrameHeight();
                    int width = DecoderPluginInterface.GetAtlasFrameWidth();

                    if (height != 0 && width != 0)
                    {
                        //PC Color
                        m_v3cPointColTex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
                        m_v3cPointColTex.enableRandomWrite = true;
                        m_v3cPointColTex.filterMode = FilterMode.Point;
                        m_v3cPointColTex.Create();
                        //PC Position
                        m_v3cPointPosTex = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat);
                        m_v3cPointPosTex.filterMode = FilterMode.Point;
                        m_v3cPointPosTex.enableRandomWrite = true;
                        m_v3cPointPosTex.Create();

                        m_v3cShadowTex = new RenderTexture(m_shadowResolution, m_shadowResolution, 0, RenderTextureFormat.ARGBFloat);
                        m_v3cShadowTex.enableRandomWrite = true;
                        m_v3cShadowTex.Create();


                        m_rp = new RenderParams(m_mat);
                        m_rp.worldBounds = new Bounds(Vector3.zero, 1000 * Vector3.one); // use tighter bounds
                        m_rp.matProps = new MaterialPropertyBlock();

                        m_sortedRP = new RenderParams(m_sortedMat);
                        m_sortedRP.worldBounds = new Bounds(Vector3.zero, 1000 * Vector3.one); // use tighter bounds
                        m_sortedRP.matProps = new MaterialPropertyBlock();


                        m_ShadowTargetMesh.material = m_shadowMat;
                        m_ShadowDefaultPosition = m_ShadowTargetMesh.transform.localPosition;
                        m_ShadowTargetMesh.gameObject.SetActive(m_showShadow);

                        //Command buffer for indirect drawing, updated on the plugin side
                        m_IndirectArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawArgs.size);
                        GraphicsBuffer.IndirectDrawArgs[] buff = new GraphicsBuffer.IndirectDrawArgs[1];
                        buff[0].vertexCountPerInstance = 0;
                        buff[0].instanceCount = 1;
                        m_IndirectArgs.SetData(buff);
                        

                        IntPtr ptr = m_IndirectArgs.GetNativeBufferPtr();
                        Debug.Log($"CommandBuffer ID: {ptr}");
                        VPCCSynthesizerInterface.SetIndirectBufferPtr(ptr);

                        m_texAllocFlag = true;

                        m_render_cam = data.m_mainCamera;

                        m_PointCloudSorter = new PointCloudSorter();
                        m_PointCloudSorter.Setup(m_sortingCompute, m_render_cam, m_VPCCModelTransform, m_sortingRange);
                        m_PointCloudSorter.Init(m_v3cPointPosTex);
                        
                    }
                    else
                    {
                        Debug.Log("Issues getting the VPCC frame Size");
                    }
                }

                //m_V3CDecoderManager.m_UICamera.gameObject.SetActive(false);
                m_V3CDecoderManager.m_renderUnityCallback = V3CDecoderManager.V3CRenderUnityCallback.OnPreCull;

                DecoderPluginInterface.SetCanvasProperties(m_v3cPointColTex.GetNativeTexturePtr(), (uint)m_v3cPointColTex.width, (uint)m_v3cPointColTex.height, (uint)TextureFormat.RGBAFloat);
                VPCCSynthesizerInterface.SetPositionProperties(m_v3cPointPosTex.GetNativeTexturePtr(), (uint)m_v3cPointPosTex.width, (uint)m_v3cPointPosTex.height, (uint)TextureFormat.RGBAFloat);
                VPCCSynthesizerInterface.SetShadowProperties(m_v3cShadowTex.GetNativeTexturePtr(), (uint)m_v3cShadowTex.width, (uint)m_v3cShadowTex.height, (uint)TextureFormat.RGBAFloat);
                VPCCSynthesizerInterface.setDynamicDecimation(m_useDynamicDecimation);
                m_render_cam = data.m_mainCamera;

                m_isSetup = true;
            }
        }

        private void UpdateVertexPerPoint()
        {
            if (m_activate && m_isSetup)
            {
                switch (m_renderMode)
                {
                    case RenderingMode.GLPoints:
                        m_numVertPerPoint = 1;
                        break;
                    case RenderingMode.SortedBlend:
                        m_numVertPerPoint = 3;
                        break;
                    default:
                        m_numVertPerPoint = 1;
                        break;
                }

                VPCCSynthesizerInterface.SetNumVertexPerPoint(m_numVertPerPoint);
            }
        }

        private void ResizeTex()
        {
            if (m_activate && m_isSetup)
            {
                m_ShadowTargetMesh.gameObject.SetActive(m_showShadow);

                int height = DecoderPluginInterface.GetAtlasFrameHeight();
                int width = DecoderPluginInterface.GetAtlasFrameWidth();

                if (height > m_v3cPointColTex.height || width > m_v3cPointColTex.width)
                {
                    UpdateSize(height, width);

                }

                switch (m_decimationLevel)
                {
                    case Decimation.None:
                        m_decimationValue = 1;
                        break;
                    case Decimation.Two:
                        m_decimationValue = 2;
                        break;
                    case Decimation.Four:
                        m_decimationValue = 4;
                        break;
                    default:
                        m_decimationValue = 1;
                        break;
                }
                VPCCSynthesizerInterface.SetDecimationLevel(m_decimationValue);
                VPCCSynthesizerInterface.setDynamicDecimation(m_useDynamicDecimation);
                VPCCSynthesizerInterface.SetMaxBbox(m_maxBbox);
            }
        }

        private void UpdateCamera()
        {
            if (m_activate && m_isSetup && m_render_cam != null)
            {
                Matrix4x4 MVP_mat = m_render_cam.projectionMatrix * m_render_cam.worldToCameraMatrix * m_VPCCModelTransform.localToWorldMatrix;
                float[] mvp_arr = new float[16];
                for (int i = 0; i < 16; i++)
                {
                    mvp_arr[i] = MVP_mat.transpose[i];
                }
                VPCCSynthesizerInterface.SetMVP(mvp_arr);
                VPCCSynthesizerInterface.setDecimationRanges(m_r1 / m_maxBbox, m_r2 / m_maxBbox, m_viewportCullThreshold);
            }
        }

        private void UpdateSize(int height, int width)
        {
            m_v3cPointColTex.Release();
            m_v3cPointColTex.width = width;
            m_v3cPointColTex.height = height;
            m_v3cPointColTex.Create();

            m_v3cPointPosTex.Release();
            m_v3cPointPosTex.width = width;
            m_v3cPointPosTex.height = height;
            m_v3cPointPosTex.Create();

            DecoderPluginInterface.SetCanvasProperties(m_v3cPointColTex.GetNativeTexturePtr(), (uint)m_v3cPointColTex.width, (uint)m_v3cPointColTex.height, (uint)TextureFormat.RGBAFloat);
            VPCCSynthesizerInterface.SetPositionProperties(m_v3cPointPosTex.GetNativeTexturePtr(), (uint)m_v3cPointPosTex.width, (uint)m_v3cPointPosTex.height, (uint)TextureFormat.RGBAFloat);
        }

        private void Draw()
        {
            if (m_activate && m_isSetup)
            {
                if (m_showShadow)
                {
                    m_shadowMat.mainTexture = m_v3cShadowTex;
                    m_shadowMat.color = m_shadowColor;
                    m_shadowMat.SetTexture("_PosTex", m_v3cPointPosTex);
                    m_shadowMat.SetMatrix("_LocalToWorld", m_VPCCModelTransform.localToWorldMatrix);
                }

                MeshTopology topo;
                switch (m_renderMode)
                {

                    case RenderingMode.SortedBlend:
                        m_useSort = true;
                        topo = MeshTopology.Triangles;
                        break;
                    case RenderingMode.GLPoints:
                    default:
                        m_useSort = false;
                        topo = MeshTopology.Points;
                        break;
                }

                if (m_logNumPoints || m_useQuestStabilityHack)
                {
                    //On really big models with high quality rendering, the Quest may struggle to render,
                    //dropping to absymal framerate and not managing to track content anymore.
                    //This can also happen if the Scene support is enabled, or any other processing heavy features.
                    //This seems to be caused by GPU/CPU synchronisation issues.
                    //GetData force synchronisation between CPU and GPU. This might improve framerate and tracking stability.
                    GraphicsBuffer.IndirectDrawArgs[] data = new GraphicsBuffer.IndirectDrawArgs[1];
                    m_IndirectArgs.GetData(data);

                    if (m_logNumPoints)
                    {
                        Debug.Log($"Indirect data: num vert={data[0].vertexCountPerInstance}, num instances={data[0].instanceCount}, start instance={data[0].startInstance}, start vertex={data[0].startVertex}");
                    }
                }

                if (m_useSort)
                {
                    m_PointCloudSorter.Compute(m_IndirectArgs, m_numVertPerPoint);//, (int)data[0].vertexCountPerInstance / m_numVertPerPoint);

                    m_sortedRP.camera = null;
                    m_sortedRP.matProps.SetTexture("_UV", m_PointCloudSorter.m_output);
                    m_sortedRP.matProps.SetTexture("_PosTex", m_v3cPointPosTex);
                    m_sortedRP.matProps.SetTexture("_ColTex", m_v3cPointColTex);
                    m_sortedRP.matProps.SetFloat("_PointSize", m_sortedPointSize);
                    m_sortedRP.matProps.SetInteger("_Width", m_v3cPointPosTex.width);
                    m_sortedRP.matProps.SetInteger("_Height", m_v3cPointPosTex.height);
                    m_sortedRP.matProps.SetMatrix("_LocalToWorld", m_VPCCModelTransform.localToWorldMatrix);
                    m_sortedRP.matProps.SetFloat("_LocalScale", m_VPCCModelTransform.lossyScale.x);
                    m_sortedRP.matProps.SetInteger("_NumVertex", m_numVertPerPoint);
                    m_sortedRP.matProps.SetFloat("_InvSigmaSq", m_pointAlphaFalloff);
                    m_sortedRP.matProps.SetFloat("_InvMaxBbox", 1.0f/m_maxBbox);
                    m_sortedRP.matProps.SetInt("_ShowDecimationRanges", m_showRanges ? 1 : 0);
                    m_sortedRP.matProps.SetInt("_UseLinearCorrection", m_QuestColorCorrection ? 1 : 0);

                    Graphics.RenderPrimitivesIndirect(m_sortedRP, topo, m_IndirectArgs, 1, 0);
                }
                else 
                {
                    m_rp.camera = null;
                    m_rp.matProps.SetTexture("_PosTex", m_v3cPointPosTex);
                    m_rp.matProps.SetTexture("_ColTex", m_v3cPointColTex);
                    m_rp.matProps.SetFloat("_PointSize", m_pointSize);
                    m_rp.matProps.SetInt("_ShowDecimationRanges", m_showRanges?1 : 0);
                    m_rp.matProps.SetInteger("_Width", m_v3cPointPosTex.width);
                    m_rp.matProps.SetInteger("_Height", m_v3cPointPosTex.height);
                    m_rp.matProps.SetMatrix("_LocalToWorld", m_VPCCModelTransform.localToWorldMatrix);
                    m_rp.matProps.SetFloat("_LocalScale", m_VPCCModelTransform.lossyScale.x);
                    m_rp.matProps.SetInteger("_NumVertex", m_numVertPerPoint);
                    m_rp.matProps.SetFloat("_InvMaxBbox", 1.0f / m_maxBbox);
                    m_rp.matProps.SetInt("_UseLinearCorrection", m_QuestColorCorrection ? 1 : 0);

                    Graphics.RenderPrimitivesIndirect(m_rp, topo, m_IndirectArgs, 1, 0);
                }
            }
        }

        public void ReleaseTextures()
        {
            m_v3cPointColTex?.Release();
            m_v3cPointPosTex?.Release();
            m_v3cShadowTex?.Release();
            m_IndirectArgs?.Release();
            m_texAllocFlag = false;
        }

        public void SetShadowOffset(float offset)
        {
            m_ShadowTargetMesh.transform.localPosition = m_ShadowDefaultPosition + Vector3.up * offset;
        }

        public void SetRenderMode(RenderingMode val)
        {
            m_renderMode = val;
		}

        public void SetMaxBbox(float size)
        {
            m_maxBbox = size;
        }
    }
}