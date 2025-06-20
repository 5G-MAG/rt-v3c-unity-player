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
using AOT;
using System.Collections;
using System.Threading.Tasks;

public static class HapticCallbacks
{
    public static HapticManager instance;

    [MonoPInvokeCallback(typeof(HapticPluginInterface.HapticCallbackDelegate))]
    public static void CallBack(int channelId = 0, long duration = 500, float startIntensity = 1.0f, float endIntensity = 1.0f)
    {
        Debug.LogWarning("Vibrate on " + channelId + ": " + duration + "ms from " + (int)(startIntensity * 100) + "% to " + (int)(endIntensity * 100) + "%");
        if (instance != null)
        {
            instance.StartVibration(channelId, duration, startIntensity, endIntensity);
        }
    }
}
