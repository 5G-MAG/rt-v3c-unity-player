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

Shader "IDCC/VPCCRenderer"
{
    Properties
    {
        _PosTex ("Pos Texture", 2D) = "white" {}
        _ColTex ("Col Texture", 2D) = "white" {}
        _PointSize("Point Size", Float) = 1.0
        [Toggle] _ShowDecimationRanges ("Show Dynamic Decimation Ranges", Integer) = 0
        [Toggle] _UseLinearCorrection ("Use Linear Color Correction", Integer) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawArgs
            #include "UnityIndirect.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv_splat : TEXCOORD1;
                float4 pos : SV_POSITION;
                float size : PSIZE;
                nointerpolation float4 dec_col : TEXCOORD2;
            };

            sampler2D _PosTex;
            sampler2D _ColTex;
            float4x4 _LocalToWorld;
            float _LocalScale;
            uint _NumVertex;
            uint _Width;
            uint _Height;
            float _PointSize;
            int _ShowDecimationRanges;
            float _InvMaxBbox;

            v2f vert (uint vertexID: SV_VertexID, uint ID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                v2f o;
                
                uint vert_id = (vertexID % _NumVertex);
                uint linear_id = vertexID/_NumVertex;
                uint2 coord;
                coord.x = linear_id % _Width;
                coord.y = linear_id / _Width;
                float2 uv = float2((float(coord.x)+0.5) / _Width, (float(coord.y) + 0.5) / _Height);

                float4 pos = tex2Dlod(_PosTex, float4(uv.xy, 0.0, 0.0));
                float p_size =  _PointSize * _LocalScale * 1024 * _InvMaxBbox * pos.a ;


                float4 s_pos = mul(UNITY_MATRIX_VP, mul(_LocalToWorld, float4(pos.xyz, 1.0)));
                o.uv_splat = float2(0.0, 0.0);

                o.pos = float4(s_pos.x * (pos.a > 0.5) - 100 * (pos.a < 0.5), s_pos.y, s_pos.z, s_pos.w);
                o.uv = uv;
                o.size = p_size/o.pos.w;

                half4 col = tex2Dlod(_ColTex, float4(uv.xy, 0.0, 0.0));
                if(_ShowDecimationRanges){
                    o.dec_col = col * half4(pos.w < 1.5, pos.w > 1.5 && pos.w < 2.5, pos.w > 2.5 && pos.w < 4.5, 1.0 );
                }
                else{
                    o.dec_col = col;
                }
                return o;
            }

            int _UseLinearCorrection;

            half4 frag (v2f i) : SV_Target
            {
                half3 sRGB = i.dec_col;
                half4 col;
                if (_UseLinearCorrection){
                    half3 RGB = sRGB * (sRGB * (sRGB * 0.305306011 + 0.682171111) + 0.012522878);
                    col = half4(RGB, 1.0);
                }
                else{
                    col= half4(sRGB, 1.0);
                }
                return col;


            }
            ENDCG
        }
    }
}
