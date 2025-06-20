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

using System.Collections;
using UnityEngine;

public class HapticManagerAndroid : HapticManager
{
    class HapticEvent
    {
        public float startTime;
        public float endTime;
        public int startIntensity;
        public int endIntensity;
        public float timer = 0f;
    };

    private HapticEvent hapticEvent = new HapticEvent();

    private Object threadLocker = new Object();

    private void Update()
    {
        SendHapticEvent();
    }

    public void SendHapticEvent()
    {
        
        if (hapticEvent != null && hapticEvent.timer < hapticEvent.endTime)
        {
            hapticEvent.timer += Time.deltaTime*1000f;
            float ratio = hapticEvent.timer / hapticEvent.endTime;
            int intensity = (int)Mathf.Lerp(hapticEvent.startIntensity, hapticEvent.endIntensity, ratio);
            Vibrator.Cancel();
            Vibrator.Vibrate((long)(hapticEvent.endTime - hapticEvent.timer), intensity);
        }
    }

    public void SetHapticEvent( long duration, int start_intensity, int end_intensity)
    {
        hapticEvent.startTime = 0f;
        hapticEvent.endTime = Mathf.Clamp(duration, 150, 10000);
        hapticEvent.startIntensity = Mathf.Clamp(start_intensity, 1, 255);
        hapticEvent.endIntensity = Mathf.Clamp(end_intensity, 1, 255);
        hapticEvent.timer = 0f;
    }
    
    public override void StartVibration(int channelId = 0, long duration = 500, float startIntensity = 1.0f, float endIntensity = 1.0f)
    {
        SetHapticEvent(duration, (int)(startIntensity*255), (int)(endIntensity*255));
    
    }

    public IEnumerator Vibe(long duration, int startIntensity, int endIntensity)
    {
        lock (threadLocker)
        {
            hapticEvent.startTime = Time.time * 1000f;
            // Clamp duration at min 150 because of the Tab S8
            hapticEvent.endTime = hapticEvent.startTime + Mathf.Clamp(duration, 150, 10000);
            hapticEvent.startIntensity = Mathf.Clamp(startIntensity, 1, 255);
            hapticEvent.endIntensity = Mathf.Clamp(endIntensity, 1, 255);
        }

        yield return null;
    }
}