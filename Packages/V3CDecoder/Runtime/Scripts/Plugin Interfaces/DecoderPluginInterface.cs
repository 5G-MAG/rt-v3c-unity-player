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
using System.Text;

public class DecoderPluginInterface
{

    public enum MediaType : int
    {
        Video = 0,
        MIV = 1,
        VPCC = 2
    }
    public enum CameraType : uint
    {
        Equirectangular = 0,
        Perspective = 1,
        Orthographic = 2
    }

    public enum QualityProfile : uint
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    //Wrapping GetMediaName to ensure we use the right capacity to avoid memory corruption
    public static void GetMediaName(uint mediaId, StringBuilder mediaName)
    {
        //StringBuilder Capacity management is not defined in the C# specifications and is implementation dependant
        //On Windows, the capacity is not updated after updating the string in the C++ 
        //On (some?) Android devices, it shrink to Length()+1
        
        //We force the capacity to never shrink here

        int c = mediaName.Capacity;
        GetMediaName(mediaId, mediaName, mediaName.Capacity);
        mediaName.Capacity = mediaName.Capacity < c ? c : mediaName.Capacity; 
    }

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern IntPtr GetRenderEventFunc();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern IntPtr GetGraphicsHandleSetterFunc();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern bool CheckPluginStatus();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void OnCreateEvent(string configFile);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void OnDestroyEvent();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void OnStartEvent(uint mediaId);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void OnPauseEvent(bool b);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void OnStopEvent();

    public delegate void ErrorsCallbackDelegate(uint errorLevel, uint errorId);
    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void SetOnErrorEventCallback(ErrorsCallbackDelegate ec);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void SetCanvasProperties(IntPtr handle, uint width, uint height, uint fmt);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void UpdateAudioExtrinsics(float tx, float ty, float tz, float qx, float qy, float qz, float qw);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void UpdateNumberOfJobs(uint nbJobs);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void UpdateViewport(uint jobId, uint w, uint h, uint left, uint bottom);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void UpdateCameraProjection(uint jobId, uint typeId);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void UpdateCameraResolution(uint jobId, uint w, uint h);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void UpdateCameraIntrinsics(uint jobId, float k1, float k2, float k3, float k4);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void UpdateCameraExtrinsics(uint jobId, float tx, float ty, float tz, float qx, float qy, float qz, float qw);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void UpdateMatricesVPCC(uint jobId, float[] model, float[] view, float[] proj);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void SetPointSizeVPCC(uint jobId, float size);
    
    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void SetShadowAlphaVPCC(uint jobId, float alpha);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void SetShadowOffsetVPCC(uint jobId, float offset);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void SetQualityProfile(uint profileId);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern uint GetNumberOfMedia();

    [DllImport("V3CImmersiveDecoderVideo")]
    private static extern void GetMediaName(uint mediaId, StringBuilder mediaName, int strCapacity);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern int GetMediaId();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern MediaType GetMediaType();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void OnMediaRequest(uint mediaId);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern bool IsViewingSpaceCameraIn(float x, float y, float z);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern float GetViewingSpaceInclusion(uint jobId);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern float GetViewingSpaceSize();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern float GetViewingSpaceSolidAngle();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern uint GetReferenceCameraType();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern float GetReferenceCameraAspectRatio();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern float GetReferenceCameraVerticalFoV();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void GetReferenceCameraClippingRange(ref float zMin, ref float zMax);

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void GetGenericData(   ref uint frameId,
                                            ref IntPtr metadataPtr,
                                            ref IntPtr occupancyMapId, ref uint occupancyMapWidth, ref uint occupancyMapHeight, ref uint occupancyMapFormat,
                                            ref IntPtr geometryMapId, ref uint geometryMapWidth, ref uint geometryMapHeight, ref uint geometryMapFormat,
                                            ref IntPtr textureMapId, ref uint textureMapWidth, ref uint textureMapHeight, ref uint textureMapFormat,
                                            ref IntPtr transparencyMapId, ref uint transparencyMapWidth, ref uint transparencyMapHeight, ref uint transparencyMapFormat);
    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern double GetDecoderFPS();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern void FlushFPSMeasures();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern bool SetGraphicsHandle();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern int GetAtlasFrameHeight();

    [DllImport("V3CImmersiveDecoderVideo")]
    public static extern int GetAtlasFrameWidth();

}