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
using System.Runtime.InteropServices;

public class VPCCSynthesizerInterface
{
    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void SetIndirectBufferPtr(IntPtr ptr);

    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void SetDecimationLevel(int level);

    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void SetNumVertexPerPoint(int num_vert_per_point);

    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void SetPositionProperties(IntPtr handle, uint width, uint height, uint fmt);

    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void SetShadowProperties(IntPtr handle, uint width, uint height, uint fmt);

    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void SetMaxBbox(float size);

    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void SetMVP(float[] MVP);

    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void setDynamicDecimation(bool useDD);

    [DllImport("V3CImmersiveSynthesizerVPCC")]
    public static extern void setDecimationRanges(float r1, float r2, float vp_cull_factor);


}
