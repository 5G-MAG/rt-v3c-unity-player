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
using System.IO;
using UnityEngine;
using System.Collections;
using System.Text;
using AOT;

namespace IDCC.V3CDecoder
{
    public class V3CDecoderManager : MonoBehaviour
    {
        public static V3CDecoderManager instance;

        [MonoPInvokeCallback(typeof(DecoderPluginInterface.ErrorsCallbackDelegate))]
        public static void ErrorsCallBack(uint errorLevel, uint errorId)
        {
            if (instance != null)
            {
                instance.onErrorEvent.Invoke(errorLevel, errorId);
            }
        }

        //Struct to store everything needed for the rendering of a specific stream
        [Serializable]
        public struct V3CRenderData
        {
            public Camera m_mainCamera;
            //public RenderTexture m_v3cColTex;
            public DecoderPluginInterface.MediaType m_mediaType; //The type of media (VPCC, MIV)
                                                                 //public float m_referenceFoV; //Content FoV (typ. for MIV content)
            public int m_mediaId;
            //public int m_nbViews;
            public string m_mediaName;
        }

        public enum TextureSize { SensorSize, ScreenSize }
        public enum V3CRenderUnityCallback { OnPreCull, OnPreRender, OnPostRender };


        public event Action<string> OnInit = delegate { };

        //Event sent when requesting a new media
        public event Action OnMediaRequest = delegate { };

        //Events sent when finished loading a new media to setup rendering
        public event Action<V3CRenderData> OnPreMediaReady = delegate { };
        public event Action<V3CRenderData> OnMediaReady = delegate { };
        public event Action<V3CRenderData> OnPostMediaReady = delegate { };

        //Event sent when the decoding is paused/unpaused
        public event Action<bool> OnPause = delegate { };

        //Propagate Unity Render Events (accessible natively only on GameObject attached to camera, but not anymore)
        public event Action OnV3CPreRender = delegate { };
        public event Action OnV3CPostRender = delegate { };

        public delegate void OnErrorEvent(uint errorLevel, uint errorId);
        public event OnErrorEvent onErrorEvent;

        [Header("Rendering")]

        public Camera m_mainCamera;
        public float m_referenceFoV = 60.0f;
        public V3CRenderUnityCallback m_renderUnityCallback;
        public int m_startMediaId = 0;
        public bool m_autoStart = false;
        private DecoderPluginInterface.MediaType m_mediaType = DecoderPluginInterface.MediaType.Video;

        //Plugin status
        public bool m_isInit { get; private set; }
        public bool m_isStarted { get; private set; }
        public bool m_isMediaReady { get; private set; }
        public bool m_isPaused { get; private set; }

        private int m_currentMediaId = -1;
        private int m_requestedMediaId = -1;

        //Rendering Paramaters
        //private static uint g_sensorWidth = 1920;
        //private static uint g_sensorHeight = 1080;

        //public bool m_useHaptics = false;
        public bool m_useAudio = false;

        public string m_configFile = "config.json";

        [Range(1, 2)]
        public int m_ViewsNb = 1;

        [SerializeField]
        private V3CRenderData m_renderData;

        private CamEvent m_camEvents;

        public void Awake()
        {
            m_isInit = false;
            m_isStarted = false;
            m_isMediaReady = false;
            m_isPaused = false;
            //QualitySettings.vSyncCount = 0;

        }

        private void Start()
        {
            Application.targetFrameRate = 1000;
            InitPlugin();
            if (m_autoStart)
            {
                StartCoroutine(TryStartCoroutine());
            }
        }

        public void OnApplicationQuit()
        {
            //ReleaseTextures();
            ReleasePlugin();
        }

        public void InitPlugin()
        {
            if (!m_mainCamera)
            {
                Debug.LogError("No Camera setup in V3CDecoderManager, aborting initialisation");
                return;
            }

            //Add a helper class to the camera which propagates the camera exclusive render events
            m_camEvents = m_mainCamera.gameObject.AddComponent<CamEvent>();

            m_camEvents.OnPreCullEvent += OnPreCullEvent;
            m_camEvents.OnPreRenderEvent += OnPreRenderEvent;
            m_camEvents.OnPostRenderEvent += OnPostRenderEvent;

            m_renderData.m_mainCamera = m_mainCamera;
            StartCoroutine(InitCoroutine());
        }

        private IEnumerator InitCoroutine()
        {
            if (!m_isInit)
            {
                string configFile = Path.Combine(Application.persistentDataPath, m_configFile);
                DecoderPluginInterface.OnCreateEvent(configFile);
                V3CDecoderManager.instance = this;
                DecoderPluginInterface.SetOnErrorEventCallback(V3CDecoderManager.ErrorsCallBack);

                if (m_useAudio)
                {
                    AudioPluginInterface.OnCreateEvent(configFile);
                }

                HapticPluginInterface.OnCreateEvent(configFile);
                HapticManager hm = GameObject.Find("EventSystem").GetComponent<HapticManager>();
                HapticCallbacks.instance = hm;
                HapticPluginInterface.SetHapticCallback(new HapticPluginInterface.HapticCallbackDelegate(HapticCallbacks.CallBack));

                OnInit(configFile);

                GL.IssuePluginEvent(DecoderPluginInterface.GetGraphicsHandleSetterFunc(), 0);

                bool isReady = DecoderPluginInterface.CheckPluginStatus();

                while (!isReady)
                {
                    isReady = DecoderPluginInterface.CheckPluginStatus();
                    Debug.Log($"Is plugin ready? {isReady}");
                    yield return null;
                }
                m_isInit = true;

                //Set default values to avoid crashes
                DecoderPluginInterface.UpdateNumberOfJobs(1);
                DecoderPluginInterface.UpdateCameraIntrinsics(0, 60.0f, 60.0f, 860.0f, 540.0f);
                DecoderPluginInterface.UpdateCameraExtrinsics(0, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f);
            }
        }

        public void ReleasePlugin()
        {
            DecoderPluginInterface.OnStopEvent();
            AudioPluginInterface.OnDestroyEvent();
            DecoderPluginInterface.OnDestroyEvent();
            m_isInit = false;
            Debug.Log("Plugin released");
        }

        /////////Media Management//////////

        private IEnumerator TryStartCoroutine()
        {
            while (!m_isInit)
            {
                yield return null;
            }
            StartPlugin();
        }

        public void StartPlugin()
        {
            StartPlugin(m_startMediaId);
        }

        public void StartPlugin(int start_id)
        {
            //ResetIntrinsics();
            if (CheckMediaId(start_id))
            {
                DecoderPluginInterface.OnStartEvent((uint)start_id);
                m_requestedMediaId = start_id;
                m_isStarted = true;
                Debug.Log($"Plugin started on media id {start_id}");
            }
            else
            {
                Debug.LogError($"Invalid Start Media Id {start_id}, id should be in range [0, {DecoderPluginInterface.GetNumberOfMedia()}]");
            }
        }

        public void ChangeMedia(int id)
        {
            Debug.Log($"Change Media: Request = {id}, current = {m_currentMediaId}");
            if (m_isInit && m_isStarted)
            {
                if (CheckMediaId(id) && (id != m_currentMediaId))
                {
                    m_requestedMediaId = id;
                    DecoderPluginInterface.OnMediaRequest((uint)id);
                    OnMediaRequest();
                }
                else
                {
                    Debug.LogWarning($"Invalid Media Request, requested media id: {id}, total media available {DecoderPluginInterface.GetNumberOfMedia()}");
                }
            }
            else
            {
                StartPlugin(id);
            }
        }

        private bool CheckMediaId(int id)
        {
            if (id >= 0 && id < DecoderPluginInterface.GetNumberOfMedia())
            {
                return true;
            }
            else
            {
                Debug.LogWarning($"Invalid Media Request, requested media id: {id}, total media available {DecoderPluginInterface.GetNumberOfMedia()}");
                return false;
            }
        }


        public void TogglePlayPause()
        {
            Pause(!m_isPaused);
        }

        public void Pause(bool is_paused)
        {
            DecoderPluginInterface.OnPauseEvent(is_paused);
            m_isPaused = is_paused;
            OnPause(m_isPaused);
        }

        public void Stop()
        {
            //Stop the decoder by loading an invalid index
            m_currentMediaId = 255;
            m_requestedMediaId = 255;
            OnMediaRequest();
            DecoderPluginInterface.OnMediaRequest(0xFF);
            Debug.Log($"Stop: current = {m_currentMediaId}");
            m_isStarted = false;
        }

        public void Update()
        {
            if (m_isInit && m_isStarted)
            {
                if (m_requestedMediaId != m_currentMediaId)
                {
                    m_isMediaReady = (m_requestedMediaId == DecoderPluginInterface.GetMediaId());
                    if (m_isMediaReady)
                    {
                        m_currentMediaId = m_requestedMediaId;
                        m_mediaType = DecoderPluginInterface.GetMediaType();
                        m_renderData.m_mediaType = m_mediaType;
                        m_renderData.m_mediaId = m_currentMediaId;

                        if (m_currentMediaId != -1)
                        {
                            StringBuilder mediaName = new StringBuilder(1024);
                            DecoderPluginInterface.GetMediaName((uint)m_currentMediaId, mediaName);
                            m_renderData.m_mediaName = mediaName.ToString();
                        }
                        else
                        {
                            m_renderData.m_mediaName = "";
                        }

                        //ResetIntrinsics();

                        OnPreMediaReady(m_renderData);
                        OnMediaReady(m_renderData);
                        OnPostMediaReady(m_renderData);

                        Pause(false);

                        Debug.Log($"Media {m_requestedMediaId} Ready, type {m_mediaType}");
                    }
                    else
                    {
                        //Debug.Log($"Waiting for media {m_requestedMediaId}");
                    }
                }
            }
        }

        /////////Rendering//////////

        private void OnPreCullEvent()
        {
            //ClearOutTextures();
            if (m_renderUnityCallback == V3CRenderUnityCallback.OnPreCull)
            {
                RenderV3C();
            }
        }


        private void OnPreRenderEvent()
        {
            if (m_renderUnityCallback == V3CRenderUnityCallback.OnPreRender)
            {
                RenderV3C();
            }
        }

        private void RenderV3C()
        {
            if (m_isInit && m_isStarted && m_isMediaReady)
            {
                OnV3CPreRender();
                GL.IssuePluginEvent(DecoderPluginInterface.GetRenderEventFunc(), 0);
                OnV3CPostRender();
            }
        }

        private void OnPostRenderEvent()
        {
            if (m_renderUnityCallback == V3CRenderUnityCallback.OnPostRender)
            {
                RenderV3C();
            }
        }

        public V3CRenderData GetRenderData()
        {
            return m_renderData;
        }
    }
}
