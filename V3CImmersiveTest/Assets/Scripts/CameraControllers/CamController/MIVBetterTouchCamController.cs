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
//using UnityEngine.InputSystem;
//using UnityEngine.InputSystem.Controls;


[CreateAssetMenu(fileName = "MIVBetterTouchCamController", menuName = "Scriptable/CamControllers/MIV/BetterTouch")]
public class MIVBetterTouchCamController : CameraController
{
    public float translationSensitivity = 0.01f;
    public float rotationSensitivity = 0.02f;
    public float zoomSensitivity = 0.1f;


    [SerializeField]
    float relativeFOVLow = 0.8f;
    [SerializeField]
    float relativeFOVHigh = 1.2f;

    [SerializeField]
    float movementThreshold = 50.0f;  // pixels
    [SerializeField]
    double longPressTimeThreshold = 0.5;  // seconds

    [SerializeField]
    float zoomThresh = 50f / 1920f;

    public override void UpdateCam()
    {
        
        //TouchControl touch = Touchscreen.current.primaryTouch;
        if (enable)
        {
            //Do fov update each frame as AR mess with fov 
            bool fov_updated = UpdateFOV();
            
            if (Input.touchCount > 0) {
                Touch touch = Input.GetTouch(0);
                //if (touch.tapCount > 0 && touch.phase == TouchPhase.Began)
                //{
                //    //UpdateMovementMode();
                //}
                if (touch.phase < TouchPhase.Ended)
                {
                    TouchScreenUtils.UpdatePhase(movementThreshold, longPressTimeThreshold);
                    if (TouchScreenUtils.phase == TouchScreenUtils.Phase.DualTouch)
                    {
                        if (!fov_updated)
                        {
                            UpdateRotation();
                        }

                    }
                    else if (TouchScreenUtils.phase == TouchScreenUtils.Phase.Moved)
                    {
                        UpdateTranslation();
                    }

                }
            }
        }
    }

    //void UpdateMovementMode()
    //{
    //    if (Input.touchCount > 0)
    //    {
    //        Touch touch = Input.GetTouch(0);
    //        int tapCount = touch.tapCount;

    //        if (tapCount == 2 && tapCount != prevTapCount)
    //            moveModeForward = !moveModeForward;

    //        prevTapCount = tapCount;
    //    }
    //}

    void UpdateTranslation()
    {

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            Vector2 newPos = translationSensitivity * touch.deltaPosition * Time.deltaTime;

            //cam.transform.position += new Vector3(-newPos.x, -newPos.y, 0);
            cam.transform.position += (cam.transform.up * newPos.y + cam.transform.right * -newPos.x);
        }

        //if (Input.touchCount > 0)
        //{
        //    Touch touch = Input.GetTouch(0);

        //    Vector2 pos = touch.position;
        //    float fromCenterDiffx = pos.x - (Screen.width / 2);
        //    float fromCenterDiffy = pos.y - (Screen.height / 2);

        //    if (Mathf.Abs(fromCenterDiffx) > Mathf.Abs(fromCenterDiffy))
        //    {
        //        // Left / Right
        //        cam.transform.position += Mathf.Sign(fromCenterDiffx) * translationSensitivity * cam.transform.right;
        //    }
        //    else
        //    {
        //        // Up / Down or Forward / Backward
        //        Vector3 direction = (moveModeForward ? cam.transform.forward : cam.transform.up);
        //        cam.transform.position += Mathf.Sign(fromCenterDiffy) * translationSensitivity * direction;
        //    }
        //}
    }

    void UpdateRotation()
    {
        if (Input.touchCount > 1)
        {
            Touch touch = Input.GetTouch(0);

            Vector2 newPos = rotationSensitivity * touch.deltaPosition;

            cam.transform.eulerAngles += new Vector3(-newPos.y, newPos.x, 0);
        }
    }

    bool UpdateFOV()
    {
        bool res = false;
        if (cam != null)
        {
            float FOV = cam.fieldOfView;
            float zoomChange = TouchScreenUtils.GetPinch();
            if (Mathf.Abs(zoomChange) > zoomThresh)
            {
                FOV +=  zoomSensitivity * zoomChange;
                res = true;
            }
            FOV = Mathf.Clamp(FOV, relativeFOVLow * referenceFOV, relativeFOVHigh * referenceFOV);
            cam.fieldOfView = FOV;
        }
        return res;
    }

    
}
