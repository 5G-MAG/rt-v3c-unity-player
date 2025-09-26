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

public static class Vibrator
{
#if UNITY_ANDROID && !UNITY_EDITOR
    public static AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
    public static AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
    public static AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
    public static AndroidJavaClass vibratorEffect = new AndroidJavaClass("android.os.VibrationEffect");
#else
    public static AndroidJavaClass unityPlayer;
    public static AndroidJavaObject currentActivity;
    public static AndroidJavaObject vibrator;
    public static AndroidJavaClass vibratorEffect;
#endif

    public static void Vibrate_old(long milliseconds = 250)
    {
        if (IsAndroid())
        {
            //vibrator.Call("cancel");
            vibrator.Call("vibrate", milliseconds);
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        else
        {
            Handheld.Vibrate();
        }
#endif
    }

    public static void Vibrate(long[] pattern, int repeat)
    {
        if (IsAndroid())
        {
            //vibrator.Call("cancel");
            vibrator.Call("vibrate", pattern, repeat);
        }
        else
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }

    public static void Vibrate(long milliseconds = 300, int amplitude = 255)
    {
        if (IsAndroid())
        {
            //vibrator.Call("cancel");
            vibrator.Call("vibrate", vibratorEffect.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude));
        }
        else
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }

    public static void Cancel()
    {
        if (IsAndroid())
        {
            vibrator.Call("cancel");
        }
    }

    private static bool IsAndroid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return true;
#else
        return false;
#endif
    }
}
