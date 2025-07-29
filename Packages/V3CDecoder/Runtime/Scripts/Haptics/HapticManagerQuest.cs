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

using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


public class HapticManagerQuest : HapticManager
{
#if OVR_Input
    public override void StartVibration(int channelId = 0, long duration = 500, float startIntensity = 1.0f, float endIntensity = 1.0f)
    {
        Task.Run(() =>
        {
            OVRInput.SetControllerLocalizedVibration(OVRInput.HapticsLocation.Hand, 1, (startIntensity + endIntensity) / 2.0f, (channelId == 0) ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);

            Thread.Sleep((int)duration);

            OVRInput.SetControllerLocalizedVibration(OVRInput.HapticsLocation.Hand, 0, 0, (channelId == 0) ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch);
        });
    }
#else
    [Header("To use this component, please make sure the Meta Quest SDK is installed in the project")]
    public bool KO = true; //Just to have something in the class
#endif
}

