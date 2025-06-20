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

#define V3C_HAPTIC

using System.Runtime.InteropServices;

public class HapticPluginInterface
{
    public delegate void HapticCallbackDelegate(int channelId = 0, long duration = 500, float startIntensity = 1.0f, float endIntensity = 1.0f);

    [DllImport("V3CImmersiveSynthesizerHaptic")]
    public static extern void OnCreateEvent(string configFile);

    [DllImport("V3CImmersiveSynthesizerHaptic")]
    public static extern void OnDestroyEvent();

    [DllImport("V3CImmersiveSynthesizerHaptic")]
    public static extern void SetHapticCallback(HapticCallbackDelegate cb);
}
