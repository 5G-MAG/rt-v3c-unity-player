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

[CreateAssetMenu(fileName = "MIVMouseCamController", menuName = "Scriptable/CamControllers/MIV/Mouse")]
public class MIVMouseCamController : CameraController
{
    [SerializeField]
    public float slideSensitivity = 0.001f;
    [SerializeField]
    private float rotateSensitivity = 0.1f;

    [SerializeField]
    private float zoomSensitivity = 0.01f;

    [SerializeField]
    private float relativeFOVLow = 0.8f;

    [SerializeField]
    private float relativeFOVHigh = 1.2f;

    private Vector2 prevTransPos;
    private Vector2 prevRotPos;

    public override void UpdateCam()
    {
        if (Application.platform != RuntimePlatform.Android && enable)
        {
            UpdateTranslation(cam.transform);
            UpdateRotation(cam.transform);
            UpdateZoom();
        }
    }
    void UpdateTranslation(Transform transform)
    {
        if (Input.GetMouseButtonDown(0))
        {
            prevTransPos = Input.mousePosition;
        }
        if (Input.GetMouseButton(0))
        {
            Vector2 currentMousePos = Input.mousePosition;

            if (prevTransPos != currentMousePos)
            {
                var newPos = (currentMousePos - prevTransPos) * slideSensitivity;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    transform.Translate(newPos.x, 0, newPos.y, Space.Self);
                }
                else
                {
                    transform.Translate(newPos.x, newPos.y, 0, Space.Self);
                }
                prevTransPos = currentMousePos;
            }
        }
    }

    void UpdateRotation(Transform transform)
    {
        if (Input.GetMouseButtonDown(1))
        {
            prevRotPos = Input.mousePosition;
        }
        if (Input.GetMouseButton(1))
        {
            Vector2 currentMousePos = Input.mousePosition;

            if (prevRotPos != currentMousePos)
            {
                var newPos = (currentMousePos - prevRotPos) * rotateSensitivity;

                //For some reason, using transform.Rotate also affects the third axis of the rotation...
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    transform.eulerAngles += new Vector3(-newPos.y, 0, newPos.x);
                }
                else
                {
                    transform.eulerAngles += new Vector3(-newPos.y, newPos.x, 0);
                }

                prevRotPos = currentMousePos;
            }
        }
    }

    void UpdateZoom()
    {
        if (cam != null)
        {
            float scroll = Input.mouseScrollDelta.y;
            float FOV = cam.fieldOfView;
            FOV += -1 * zoomSensitivity * scroll;
            FOV = Mathf.Clamp(FOV, relativeFOVLow * referenceFOV, relativeFOVHigh * referenceFOV);
            cam.fieldOfView = FOV;
        }
    }
}



