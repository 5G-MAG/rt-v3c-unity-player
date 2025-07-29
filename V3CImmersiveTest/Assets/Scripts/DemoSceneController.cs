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
using static IDCC.V3CDecoder.V3CDecoderManager;

namespace IDCC.V3CDecoder.Demo
{
    public class DemoSceneController : MonoBehaviour
    {
        public V3CDecoderManager m_v3cDecoder;

        public GameObject errorUI;

        //private bool m_isStarted = false;
        private int m_mediaIndex = 0;

        private bool m_isPaused = false;

        private uint m_errorLevel = 0;
        private uint m_errorId = 0;

        private void Start()
        {
            m_v3cDecoder.onErrorEvent += OnErrorEvent;
            m_mediaIndex = m_v3cDecoder.m_startMediaId;
        }

        public void OnErrorEvent(uint errorLevel, uint errorId)
        {
            m_errorLevel = errorLevel;
            m_errorId = errorId;
        }

        private void Update()
        {
            if (m_errorId != 0)
            {
                ErrorManager errorManager = errorUI.GetComponent<ErrorManager>();
                errorUI.SetActive(true);
                errorManager.SetError(m_errorLevel, m_errorId);
                errorManager.ShowErrorMenu();
                //if (m_errorLevel > 1)
                //    m_v3cDecoder.Stop();
                m_errorLevel = 0;
                m_errorId = 0;
            }
        }

        public void StartStop()
        {
            if (m_v3cDecoder.m_isStarted)
            {
                m_v3cDecoder.TogglePlayPause();

            }
            else
            {
                m_v3cDecoder.StartPlugin();
            }
        }

        public void LoadNextMedia()
        {
            m_mediaIndex = (m_mediaIndex + 1) % (int)DecoderPluginInterface.GetNumberOfMedia();
            Debug.Log($"Loading next media at index {m_mediaIndex}");
            m_v3cDecoder.ChangeMedia(m_mediaIndex);
        }

        public void LoadPreviousMedia()
        {
            int num_media = (int)DecoderPluginInterface.GetNumberOfMedia();
            m_mediaIndex = (m_mediaIndex - 1 + num_media) % num_media;
            Debug.Log($"Loading previous media at index {m_mediaIndex}");
            m_v3cDecoder.ChangeMedia(m_mediaIndex);
        }

    }
}
