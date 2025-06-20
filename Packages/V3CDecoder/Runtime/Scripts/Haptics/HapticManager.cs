/*
* Copyright (c) 2024 InterDigital
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using UnityEngine;
using IDCC.V3CDecoder;

public class HapticManager : MonoBehaviour
{
    public V3CDecoderManager m_decoderManager;

    public void Awake()
    {
        m_decoderManager.OnInit += Init;
    }

    public void OnDestroy()
    {
        HapticPluginInterface.OnDestroyEvent();
    }

    public void Init(string config)
    {
        HapticPluginInterface.OnCreateEvent(config);
        HapticCallbacks.instance = this;
        HapticPluginInterface.SetHapticCallback(new HapticPluginInterface.HapticCallbackDelegate(HapticCallbacks.CallBack));
    }

    public virtual void StartVibration(int channelId = 0, long duration = 500, float startIntensity = 1.0f, float endIntensity = 1.0f) 
    {
        Debug.LogError("HapticManager.StartVibration Not Implemented: Nothing will happen");
    }
}