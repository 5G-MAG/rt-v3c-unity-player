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

using System.Collections.Generic;
using UnityEngine;
using IDCC.V3CDecoder;

public class V3CCameraController: MonoBehaviour
{

    public V3CDecoderManager m_decoderManager;
    public Transform m_VPCCCamTarget;

    public List<CameraController> MIVCameraControllers;
    public List<CameraControllerVPCC> VPCCCameraControllers;

    private Dictionary<string, CameraController> controllersMap = new Dictionary<string, CameraController>(); //Needed to enable/disable specific controllers

    private DecoderPluginInterface.MediaType contentType;
    private Camera m_cam;
    public bool controlsEnabled = false;
    private bool skip = false; //set to true when we activate the controls, ignore the inputs for 1 frame, to avoid switching MIV cam controls when selecting a stream

    private float touchSensitivity = 0.02f;
    private float mouseSensitivity = 0.002f;

    private void Awake()
    {
        m_decoderManager.OnMediaReady += OnMediaChange;
    }

    private void OnDestroy()
    {
        m_decoderManager.OnMediaReady -= OnMediaChange;
    }

    void Start()
    {
        m_cam = m_decoderManager.m_mainCamera;

        //Init MIV Camera Controllers
        foreach(var c in MIVCameraControllers)
        {
            c.Init(m_cam);
            controllersMap.Add(c.name, c);
        }

        //Init VPCC Camera Controllers
        if (m_decoderManager) {
            foreach (var c in VPCCCameraControllers)
            {
                c.Init(m_cam);
                c.SetTarget(m_VPCCCamTarget);
                controllersMap.Add(c.name, c);
            }
        }
        else
        {
            Debug.LogError("Couldn't find PCC Viewer");
        }
    }


    private void Update()
    {
        if (controlsEnabled)
        {
            //Ignore the inputs for the first activation frame, as we are still selecting content
            if (skip)
            {
                skip = false;
                return;
            }

            if (contentType == DecoderPluginInterface.MediaType.MIV)
            {
                foreach (var c in MIVCameraControllers)
                {
                    if (c is MIVMouseCamController)
                        ((MIVMouseCamController)c).slideSensitivity = mouseSensitivity;
                    c.UpdateCam();
                }
            }
            else if (contentType == DecoderPluginInterface.MediaType.VPCC)
            {
                foreach (var c in VPCCCameraControllers)
                {
                    c.UpdateCam();
                }
            }
        }
        //Else 2D content, need controller?
    }

    private void OnMediaChange(V3CDecoderManager.V3CRenderData data)
    {
        contentType = data.m_mediaType;
        touchSensitivity = 0.02f;
        mouseSensitivity = 0.002f;
        float fSize = getSEISize(data);
        if (fSize > 0)
        {
            touchSensitivity *= 2 * fSize;
            mouseSensitivity *= 2 * fSize;
        }
        ResetView();
    }

    private float getSEISize(V3CDecoderManager.V3CRenderData data)
    {
        float res = 0f;
        if (data.m_mediaType == DecoderPluginInterface.MediaType.MIV)
        {
            res = DecoderPluginInterface.GetViewingSpaceSize();
        }
        return res;
    }

    public void EnableControls(bool enable)
    {
        controlsEnabled = enable;
        skip = true;
    }

    public void EnableController(string control_name, bool enable)
    {
        foreach (var c in controllersMap)
        {
            if (c.Key == control_name || control_name == "*"){
                c.Value.enable = enable;
            }
        }
    }

    public void ReinitFOV()
    {
        float fov = Mathf.Rad2Deg * DecoderPluginInterface.GetReferenceCameraVerticalFoV();
        foreach (var c in MIVCameraControllers)
        {
            c.SetReferenceFOV(fov);   
        }
        m_cam.fieldOfView = fov;
        m_cam.ResetProjectionMatrix();
    }
    public void ResetView()
    {
        if (isActiveAndEnabled)
        {
            m_cam.transform.position = Vector3.zero;
            m_cam.transform.rotation = Quaternion.identity;

            ReinitFOV();
            m_cam.ResetProjectionMatrix();
        }
    }
}
