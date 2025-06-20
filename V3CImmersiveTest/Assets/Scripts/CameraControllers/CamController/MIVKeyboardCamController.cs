/*
* Copyright (c) 2024 InterDigital R&D France
* Licensed under the License terms of 5GMAG software (the "License").
* You may not use this file except in compliance with the License.
* You may obtain a copy of the License at https://www.5g-mag.com/license .
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and limitations under the License.
*/

using UnityEngine;

[CreateAssetMenu(fileName = "MIVKeyboardCamController", menuName = "Scriptable/CamControllers/MIV/Keyboard")]
public class MIVKeyboardCamController : CameraController
{
    public float translationSensitivity = 0.005f;
    public float rotationSensitivity = 1.0f;

    public override void UpdateCam()
    {
        if (enable) { 
        Transform transform = cam.transform;

            if (Input.GetKey(KeyCode.Keypad4))
            // Leftward
            transform.position -= translationSensitivity * transform.right;
        else if (Input.GetKey(KeyCode.Keypad6))
            // Rightward (X)
            transform.position += translationSensitivity * transform.right;
        else if (Input.GetKey(KeyCode.Keypad2))
            // Downward
            transform.position -= translationSensitivity * transform.up;
        else if (Input.GetKey(KeyCode.Keypad8))
            // Upward (Y)
            transform.position += translationSensitivity * transform.up;
        else if (Input.GetKey(KeyCode.Keypad3))
            // Backward
            transform.position -= translationSensitivity * transform.forward;
        else if(Input.GetKey(KeyCode.Keypad9))
            // Forward (Z)
            transform.position += translationSensitivity * transform.forward;
        else if (Input.GetKey(KeyCode.KeypadMinus))
            // Yaw (leftward)
            transform.RotateAround(transform.position, transform.up, -rotationSensitivity);
        else if (Input.GetKey(KeyCode.KeypadPlus))
            // Yaw (rightward)
            transform.RotateAround(transform.position, transform.up, rotationSensitivity);
        }
    }
}