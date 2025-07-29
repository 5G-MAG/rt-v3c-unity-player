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
using System.Collections.Generic;
using UnityEngine;

public class NvidiaShieldRemote
{
    public enum ButtonName { Menu, Power, Netflix, FastBackward, FastForward, PlayPause, VolDown, VolUp, Ok, Right, Left, Down, Up, Mic, Home, Back}
    public enum State {Up, Pressed, Down}

    public static bool GetButton(ButtonName name, State state = State.Pressed)
    {
        Func<KeyCode, bool> Get;
        switch (state)
        {
            case State.Up:
                Get = Input.GetKeyUp;
                break;
            case State.Pressed:
                Get = Input.GetKey;
                break;
            case State.Down:
                Get = Input.GetKeyDown;
                break;
            default:
                Get = Input.GetKey;
                break;
        }

        KeyCode k;
        switch (name)
        {
            case ButtonName.Menu:
                k = KeyCode.JoystickButton0;
                break;
            case ButtonName.Power:
                k = KeyCode.JoystickButton1;
                break;
            case ButtonName.Netflix:
                k = KeyCode.JoystickButton2;
                break;
            case ButtonName.FastBackward:
                k = KeyCode.JoystickButton3;
                break;
            case ButtonName.FastForward:
                k = KeyCode.JoystickButton4;
                break;
            case ButtonName.PlayPause:
                k = KeyCode.JoystickButton5;
                break;
            case ButtonName.VolDown:
                k = KeyCode.JoystickButton6;
                break;
            case ButtonName.VolUp:
                k = KeyCode.JoystickButton7;
                break;
            case ButtonName.Ok:
                k = KeyCode.JoystickButton8;
                break;
            case ButtonName.Right:
                k = KeyCode.JoystickButton9;
                break;
            case ButtonName.Left:
                k = KeyCode.JoystickButton10;
                break;
            case ButtonName.Down:
                k = KeyCode.JoystickButton11;
                break;
            case ButtonName.Up:
                k = KeyCode.JoystickButton12;
                break;
            case ButtonName.Mic:
                k = KeyCode.JoystickButton13;
                break;
            case ButtonName.Home:
                k = KeyCode.JoystickButton14;
                break;
            case ButtonName.Back:
                k = KeyCode.JoystickButton15;
                break;
            default:
                k = KeyCode.JoystickButton0;
                break;
        }

        return Get(k);
    }
}
