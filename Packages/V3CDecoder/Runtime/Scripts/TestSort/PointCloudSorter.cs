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
using UnityEngine;
using UnityEngine.Rendering;

public class PointCloudSorter
{
    ComputeShader m_sortCompute;
    Camera m_cam;
    Transform m_target;
    int m_range;
    
    int m_dispatchKernel;
    int m_modelRangeKernel;
    int m_histRangeKernel;
    int m_keyCountKernel;
    int m_computeUVKernel;

    RenderTexture m_input;
    RenderTexture m_keys;

    CommandBuffer m_buffer;

    [HideInInspector]
    public RenderTexture m_output;

    ComputeBuffer m_dispatch;
    uint[] m_dispatchData;

    ComputeBuffer m_modelInfo; //Store numPoints, minDepth and maxDepth
    uint[] m_modelInfoData;

    ComputeBuffer m_histogram;
    uint[] m_histogramData;

    ComputeBuffer m_keyCount;
    uint[] m_keyCountData;

    private bool m_isSetup = false;
    private bool m_isInit = false;
    private bool m_isBufferSet = false;

    public void Setup(ComputeShader compute, Camera cam, Transform target, int range)
    {
        m_sortCompute = compute;
        m_cam = cam;
        m_target = target;
        m_range = range;
        m_isSetup = true;
    }


    public void Init(RenderTexture input)
    {
        if (m_isSetup)
        {
            m_input = input;

            m_keys = new RenderTexture(input.width, input.height, 0, RenderTextureFormat.RInt);
            m_keys.filterMode = FilterMode.Point;
            m_keys.enableRandomWrite = true;
            m_keys.Create();

            m_output = new RenderTexture(input.width, input.height, 0, RenderTextureFormat.ARGBFloat);
            m_output.filterMode = FilterMode.Point;
            m_output.enableRandomWrite = true;
            m_output.Create();

            m_dispatchKernel = m_sortCompute.FindKernel("ComputeDispatch");
            m_modelRangeKernel = m_sortCompute.FindKernel("ComputeModelRange");
            m_histRangeKernel = m_sortCompute.FindKernel("ComputeHistModelRange");
            m_keyCountKernel = m_sortCompute.FindKernel("ComputeKeyCount");
            m_computeUVKernel = m_sortCompute.FindKernel("ComputeUV");

            m_sortCompute.SetInt("Width", input.width);
            m_sortCompute.SetInt("Height", input.height);
            m_sortCompute.SetFloat("InvWidth", 1.0f / input.width);
            m_sortCompute.SetFloat("InvHeight", 1.0f / input.height);

            m_sortCompute.SetTexture(m_modelRangeKernel, "Input", m_input);
            m_sortCompute.SetTexture(m_histRangeKernel, "Input", m_input);

            m_dispatchData = new uint[3];
            m_dispatch = new ComputeBuffer(3, sizeof(uint));
            m_dispatch.SetData(m_dispatchData);

            m_modelInfoData = new uint[3] { 0, uint.MaxValue, 0 }; //numpoints, mindepth, maxdepth
            m_modelInfo = new ComputeBuffer(3, sizeof(uint));
            m_modelInfo.SetData(m_modelInfoData);

            m_histogramData = new uint[m_range + 1];
            m_histogram = new ComputeBuffer(m_range + 1, sizeof(uint));
            m_histogram.SetData(m_histogramData);

            m_keyCountData = new uint[m_range + 1];
            m_keyCount = new ComputeBuffer(m_range + 1, sizeof(uint));
            m_keyCount.SetData(m_keyCountData);

            m_isInit = true;
        }
    }

    public void Reinit(RenderTexture input)
    {
        if (m_isSetup)
        {
            m_keys.Release();
            m_keys.width = input.width;
            m_keys.height = input.height;
            m_keys.Create();

            m_output.Release();
            m_output.width = input.width;
            m_output.height = input.height;
            m_output.Create();

            m_sortCompute.SetInt("Width", input.width);
            m_sortCompute.SetInt("Height", input.height);
            m_sortCompute.SetFloat("InvWidth", 1.0f / input.width);
            m_sortCompute.SetFloat("InvHeight", 1.0f / input.height);

            m_isInit = true;
        }
    }

    public void Clear()
    {
        if (m_isBufferSet)
        {
            m_cam.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, m_buffer);
            m_buffer.Release();
            m_isBufferSet = false;
        }

        if (m_isInit)
        {
            m_dispatch.Release();
            m_modelInfo.Release();
            m_histogram.Release();
            m_keyCount.Release();

            m_keys.Release();
            m_output.Release();
            m_isInit = false;
        }
    }

    public void Compute(GraphicsBuffer indirectArgs, int num_vert_per_points)//, int num_points)
    {
        if (m_isInit)
        {
            ClearOutRenderTexture(m_output);

            m_modelInfo.SetData(m_modelInfoData);

            m_sortCompute.SetBuffer(m_dispatchKernel, "IndirectArgs", indirectArgs);
            m_sortCompute.SetBuffer(m_dispatchKernel, "Dispatch", m_dispatch);
            m_sortCompute.SetBuffer(m_dispatchKernel, "ModelInfo", m_modelInfo);
            m_sortCompute.SetInt("NumVerts", num_vert_per_points);

            Vector3 cam_obj_pos = m_target.transform.worldToLocalMatrix.MultiplyPoint3x4(m_cam.transform.position);
            Vector3 cam_obj_dir = m_target.transform.worldToLocalMatrix.MultiplyVector(m_cam.transform.forward);

            m_sortCompute.SetVector("cam_pos", cam_obj_pos);
            m_sortCompute.SetVector("cam_dir", cam_obj_dir);
            m_sortCompute.SetFloat("zmin", m_cam.nearClipPlane);
            m_sortCompute.SetFloat("zmax", m_cam.farClipPlane);

            m_sortCompute.SetBuffer(m_modelRangeKernel, "ModelInfo", m_modelInfo);

            //Generate histogram for counting sort using the distance to the camera rescaled between min and max model values split in $Range slices
            m_histogram.SetData(m_histogramData);
            m_sortCompute.SetBuffer(m_histRangeKernel, "ModelInfo", m_modelInfo);
            m_sortCompute.SetBuffer(m_histRangeKernel, "Histogram", m_histogram);
            m_sortCompute.SetInt("Range", m_range);
            m_sortCompute.SetTexture(m_histRangeKernel, "KeyTex", m_keys);

            //Accumulate the histogram
            m_keyCount.SetData(m_keyCountData);
            m_sortCompute.SetBuffer(m_keyCountKernel, "Histogram", m_histogram);
            m_sortCompute.SetInt("Range", m_range);
            m_sortCompute.SetBuffer(m_keyCountKernel, "KeyCount", m_keyCount);

            //Sort the points. The output is a UV map: sample the UV map and use those coordinates to sample the color and position in the vertex/fragment shader.
            m_sortCompute.SetBuffer(m_computeUVKernel, "KeyCount", m_keyCount);
            m_sortCompute.SetBuffer(m_computeUVKernel, "Histogram", m_histogram);
            m_sortCompute.SetBuffer(m_computeUVKernel, "ModelInfo", m_modelInfo);
            m_sortCompute.SetTexture(m_computeUVKernel, "KeyTex", m_keys);
            m_sortCompute.SetTexture(m_computeUVKernel, "Output", m_output);

            SetCommandBuffer();
        }
    }

    private void SetCommandBuffer()
    {
        if (!m_isBufferSet)
        {
            m_buffer = new CommandBuffer();
            //m_buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
            m_buffer.SetExecutionFlags(CommandBufferExecutionFlags.None);
            m_buffer.DispatchCompute(m_sortCompute, m_dispatchKernel, 1, 1, 1);
            m_buffer.DispatchCompute(m_sortCompute, m_modelRangeKernel, m_dispatch, 0);
            m_buffer.DispatchCompute(m_sortCompute, m_histRangeKernel, m_dispatch, 0);
            m_buffer.DispatchCompute(m_sortCompute, m_keyCountKernel, 1, 1, 1);
            m_buffer.DispatchCompute(m_sortCompute, m_computeUVKernel, m_dispatch, 0);
            //m_cam.AddCommandBufferAsync(CameraEvent.BeforeForwardAlpha, m_buffer, ComputeQueueType.Urgent);
            m_cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, m_buffer);

            m_isBufferSet = true;
        }
    }

    public void ClearOutRenderTexture(RenderTexture renderTexture)
    {
        RenderTexture rt = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = rt;
    }

    float readUintAsFloat(uint uvalue)
    {
        float fvalue;
        if ((uvalue >> 31) == 0)
        {
            // The MSB is unset, so take the complement, then bitcast,
            // turning this back into a negative floating point value.
            uint val = ~uvalue;
            var val_b = BitConverter.GetBytes(val);
            fvalue = BitConverter.ToSingle(val_b);
        }
        else
        {
            // The MSB is set, so we started with a positive float.
            // Unset the MSB and bitcast.
            uint val = uvalue & ~(1u << 31);
            var val_b = BitConverter.GetBytes(val);
            fvalue = BitConverter.ToSingle(val_b);
        }
        return fvalue;
    }

    public void SetRange(int range)
    {
        m_range = range;

        m_histogramData = new uint[m_range + 1];
        m_histogram = new ComputeBuffer(m_range + 1, sizeof(uint));
        m_histogram.SetData(m_histogramData);

        m_keyCountData = new uint[m_range + 1];
        m_keyCount = new ComputeBuffer(m_range + 1, sizeof(uint));
        m_keyCount.SetData(m_keyCountData);
    }

    public void SetRangeString(string range)
    {
        var r = int.Parse(range);
        SetRange(r);
    }
}
