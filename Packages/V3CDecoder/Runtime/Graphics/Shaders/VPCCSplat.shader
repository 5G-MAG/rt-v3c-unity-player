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

Shader "IDCC/SplatVPCC"
{
    Properties
    {
        [Toggle] _ShowDecimationRanges ("Show Dynamic Decimation Ranges", Integer) = 0
        [Toggle] _UseLinearCorrection ("Use Linear Color Correction", Integer) = 0
        _PosTex ("Pos Texture", 2D) = "white" {}
        _ColTex ("Col Texture", 2D) = "white" {}
        _MaxSize ("Point Size Limit", Range(0.0,100)) = 0.005
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha, OneMinusDstAlpha One
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma only_renderers glcore gles3
            #pragma vertex vert
            #pragma fragment frag
            // #pragma shader_feature _ShowDecimationRanges_ON 
            // #pragma shader_feature _UseLinearCorrection_ON 
            
            
            #include "UnityCG.cginc"

            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawArgs
            #include "UnityIndirect.cginc"

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv_splat : TEXCOORD1;
                float4 pos : SV_POSITION;
                nointerpolation half4  dec_col : TEXCOORD2;
            };

            sampler2D _UV;
            sampler2D _PosTex;
            sampler2D _ColTex;
            float4x4 _LocalToWorld;
            float _LocalScale;
            uint _Width;
            uint _Height;
            float _PointSize;
            float _ShowRanges;
            float _MaxSize;
            float _InvMaxBbox;
            int _ShowDecimationRanges;

            static const float3 tri[3] = {float3(0.0,2.0,0.0), float3(sqrt(3.0),-1.0,0.0), float3(-sqrt(3.0),-1.0,0.0)};

            v2f vert (uint vertexID: SV_VertexID)
            {
                InitIndirectDrawArgs(0);
                v2f o;
                
                //Compute UV from vertex ID
                uint vert_id = (vertexID % 3); //Triangle vertex array (vertex_ID is (num points)*3 )
                uint linear_id = vertexID/3; //Point index in point cloud
                uint2 coord;
                coord.x = linear_id % _Width;
                coord.y = linear_id / _Width;
                float2 uv = float2((float(coord.x)+0.5) / _Width, (float(coord.y) + 0.5) / _Height); //center and normalize the UV coords
                float4 true_uv = tex2Dlod(_UV, float4(uv.xy, 0.0, 0.0)); //fetch the sorted coords
                float4 pos = tex2Dlod(_PosTex, float4(true_uv.xy, 0.0, 0.0)); //fetch the position in normalized point cloud bounding box coords (0->1)

                //Compute point size
                float4 v_pos =  mul(UNITY_MATRIX_V, mul(_LocalToWorld, float4(pos.xyz, 1.0))); //view space point position (reused later)
                float4 c_pos = mul(UNITY_MATRIX_P, v_pos);//point center position on screen
                float w_size =_PointSize / c_pos.w;
                float p_size = ((w_size > _MaxSize) * _MaxSize*c_pos.w + (w_size <= _MaxSize) * _PointSize)* _LocalScale * _InvMaxBbox * pos.w;

                //Generate triangle point
                v_pos = v_pos + float4(tri[vert_id]*p_size, 0.0);
                o.uv_splat = tri[vert_id].xy;
                float4 s_pos = mul(UNITY_MATRIX_P, v_pos)*2.0;

                //o.pos = float4(s_pos.x * (pos.a > 0.5) - 100 * (pos.a < 0.5), s_pos.y, s_pos.z, s_pos.w);
                o.pos = s_pos;
                o.uv = true_uv;
                half4 col = tex2Dlod(_ColTex, float4(true_uv.xy, 0.0, 0.0));
                if(_ShowDecimationRanges){
                    o.dec_col = col * half4(pos.w < 1.5, pos.w > 1.5 && pos.w < 2.5, pos.w > 2.5 && pos.w < 4.5, 1.0 );
                }
                else{
                    o.dec_col = col;
                }
                return o;
            }

            float _InvSigmaSq;
            int _UseLinearCorrection;
            half4 frag (v2f i) : SV_Target
            {
                half sq_norm_uv = i.uv_splat.x*i.uv_splat.x + i.uv_splat.y*i.uv_splat.y;
                half is_valid = (sq_norm_uv<1);
                half alpha = is_valid*(1 - clamp(sqrt(sq_norm_uv*_InvSigmaSq), 0.0, 1.0));

                half3 sRGB = i.dec_col;
                half4 col;
                if (_UseLinearCorrection){
                    half3 RGB = sRGB * (sRGB * (sRGB * 0.305306011 + 0.682171111) + 0.012522878);
                    col = half4(RGB, alpha);
                }
                else{
                    col= half4(sRGB, alpha);
                }
                return col;
            }
            ENDCG
        }
    }
}
