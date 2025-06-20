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


[CreateAssetMenu(fileName = "VPCCTouchCamController", menuName = "Scriptable/CamControllers/VPCC/Touch")]
public class VPCCTouchCamController : CameraControllerVPCC
{
    private Transform m_target;

    public float moveTowardSpeed = 0.1f;
    public float rotateSpeedX = 0.1f;
    public float rotateSpeedY = 0.1f;
    
    public float maxLat = 80.0f;
    public float minDist = 0.1f;

    private Vector2 mem_pos = Vector2.zero;


    public override void SetTarget(Transform target)
    {
        m_target = target;
    }

    public override void UpdateCam()
    {   if (enable)
        {
            if (!cam)
            {
                Debug.LogError("Cam no set");
            }
            if (!m_target)
            {
                Debug.LogError("Target no set");
            }
            UpdateMoveAround();
            UpdateMoveTowards();
        }
    }
    void UpdateMoveAround()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase > UnityEngine.TouchPhase.Began && touch.phase < UnityEngine.TouchPhase.Ended)
            {
                Vector2 delta_pos = touch.deltaPosition;
                float rot_y = delta_pos.x * rotateSpeedX;
                cam.transform.RotateAround(m_target.position, Vector3.up, rot_y);

                Vector3 dist = m_target.position - cam.transform.position;
                Vector3 flat_dist = dist - dist.y * Vector3.up;
                Vector3 rot_axis = Vector3.Cross(flat_dist.normalized, Vector3.up);

                float rot_x = delta_pos.y * rotateSpeedY;

                float latitude = Vector3.SignedAngle(flat_dist, dist, rot_axis);
                if (Mathf.Abs(latitude + rot_x) > maxLat)
                {
                    rot_x = (maxLat - Mathf.Abs(latitude)) * Mathf.Sign(latitude);
                }

                cam.transform.RotateAround(m_target.position, rot_axis, rot_x);
            }
        }

        //if (Mouse.current.leftButton.wasPressedThisFrame)
        //{
        //    mem_pos = Mouse.current.position.ReadValue();
        //}
        //if (Mouse.current.leftButton.isPressed)
        //{
        //    Vector2 pos = Mouse.current.position.ReadValue();
           
        //}
    }
    
    void UpdateMoveTowards()
    {
        if (cam != null)
        {
            float scroll = TouchScreenUtils.GetPinch();//Mouse.current.scroll.y.ReadValue();
            //scroll = scroll == 0 ? 0 : Mathf.Sign(scroll);
            //Debug.Log($"Scroll value: {scroll}");
            Vector3 move_dir = m_target.position - cam.transform.position;
            Vector3 move_vec = (scroll * moveTowardSpeed * move_dir.normalized);
            if (Vector3.Distance(cam.transform.position + move_vec, m_target.position) < minDist)
            {
                move_vec = move_dir * (Vector3.Distance(cam.transform.position, m_target.position) - minDist);
            }
            cam.transform.position += move_vec;
        }
    }

    //void UpdateRotation()
    //{
    //    if (Mouse.current.rightButton.wasPressedThisFrame)
    //    {
    //        prevRotPos = Mouse.current.position.ReadValue();
    //    }
    //    if (Mouse.current.rightButton.isPressed)
    //    {
    //        Vector2 currentMousePos = Mouse.current.position.ReadValue();

    //        if (prevRotPos != currentMousePos)
    //        {
    //            var newPos = (currentMousePos - prevRotPos) * rotateSensitivity;

    //            //For some reason, using transform.Rotate also affects the third axis of the rotation...
    //            if (Keyboard.current.ctrlKey.isPressed)
    //            {
    //                transform.eulerAngles += new Vector3(-newPos.y, 0, newPos.x);
    //            }
    //            else
    //            {
    //                transform.eulerAngles += new Vector3(-newPos.y, newPos.x, 0);
    //            }

    //            prevRotPos = currentMousePos;
    //        }
    //    }
    //}

    //void UpdateZoom()
    //{
    //    if (cam != null)
    //    {
    //        float scroll = Mouse.current.scroll.y.ReadValue();
    //        float FOV = cam.fieldOfView;
    //        FOV += -1 * zoomSensitivity * scroll;
    //        FOV = Mathf.Clamp(FOV, relativeFOVLow * referenceFOV, relativeFOVHigh * referenceFOV);
    //        cam.fieldOfView = FOV;
    //    }
    //}
}
