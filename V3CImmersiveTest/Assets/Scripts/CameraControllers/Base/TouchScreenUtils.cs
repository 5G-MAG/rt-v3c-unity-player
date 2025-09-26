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
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public static class TouchScreenUtils{

    public static Phase phase;
    private static float touch_timestamp = 0f;

    public static bool HasTwoTouches()
    {
        return (Input.touchCount == 2);
    }

    public static bool IsLongPress(double longPressTimeThreshold)
    {
        if (Input.touchCount > 0)
        {
            double deltaTime = Time.realtimeSinceStartup - touch_timestamp;//Touchscreen.current.primaryTouch.startTime.ReadValue();
            return (deltaTime > longPressTimeThreshold);
        }
        return false;
    }

    public static bool HasMoved(float movementThreshold)
    {
        if (Input.touchCount > 0)
        {
            float movement = Input.GetTouch(0).deltaPosition.sqrMagnitude;
            return (movement > movementThreshold);
        }
        return false;
    }

    public static void UpdatePhase(float movementThreshold, double longPressTimeThreshold)
    {
        if (Input.touchCount > 0)
        {
            switch (Input.GetTouch(0).phase)
            {
                case TouchPhase.Began:
                    phase = Phase.Began;
                    touch_timestamp = Time.realtimeSinceStartup;
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    phase = Phase.None;
                    break;
                default:  // do not overwrite phase
                    break;
            }

            if (HasTwoTouches())
                phase = Phase.DualTouch;
            else if (phase == Phase.DualTouch)
                phase = Phase.None;
            else
            if (phase == Phase.Began)
            {
                if (IsLongPress(longPressTimeThreshold))
                    phase = Phase.Stationary;
                else if (HasMoved(movementThreshold))
                    phase = Phase.Moved;
            }
        }
        else
        {
            phase = Phase.None;
        }
    }

    public static float GetPinch()
    {
        if (Input.touchCount >= 2)
        {
            //TouchControl primary = Touchscreen.current.primaryTouch;
            Touch primary = Input.GetTouch(0);

            float pinch = 0.0f;
            try
            {
                Touch secondary = Input.GetTouch(1);
                
                float end = (primary.position - primary.deltaPosition - secondary.position + secondary.deltaPosition).sqrMagnitude;
                float start = (primary.position - secondary.position).sqrMagnitude;
                pinch = (end - start) / Mathf.Max(Screen.width, Screen.height);
            }
            catch (InvalidOperationException) { }

            return pinch;
        }
        return 0f;
    }

    public enum Phase
    {
        None, Began, Stationary, Moved, DualTouch
    }
}
