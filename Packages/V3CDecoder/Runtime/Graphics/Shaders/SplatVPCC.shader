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

Shader "IDCC/SplatVPCC"
{
    Properties
    {
        _PosTex ("Pos Texture", 2D) = "white" {}
        _ColTex ("Col Texture", 2D) = "white" {}
        _PointSize("Point Size", Float) = 1.0
        _LinearCorrection("Linear Correction", Range(0.0,1.0)) = 0.0
        _InvSigmaSq("One over variance", Float) = 1.0
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
            };

            sampler2D _UV;
            sampler2D _PosTex;
            sampler2D _ColTex;
            float4x4 _LocalToWorld;
            uint _NumVertex;
            uint _Width;
            uint _Height;
            float _PointSize;
            uint _PointFilter;

            static float scale = 1.0/1024.0;
            static float3 tri[] = {float3(0.0,2.0,0.0), float3(sqrt(3.0),-1.0,0.0), float3(-sqrt(3.0),-1.0,0.0)};

            v2f vert (uint vertexID: SV_VertexID, uint ID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                v2f o;
                // uint instanceID = GetIndirectInstanceID(ID);
                
                    uint vert_id = (vertexID % _NumVertex);
                    uint linear_id = vertexID/_NumVertex;
                    // uint linear_id = vertexID - vert_id;
                    uint2 coord;
                    coord.x = linear_id % _Width;
                    coord.y = linear_id / _Width;
                    float2 uv = float2((float(coord.x)+0.5) / _Width, (float(coord.y) + 0.5) / _Height);
                    float4 true_uv = tex2Dlod(_UV, float4(uv.xy, 0.0, 0.0));// + float4(0.5f/_Width, 0.5f/_Height,0,0);

                    // float2 true_uv = float2((float(coord.x)+0.5) / _Width, (float(coord.y) + 0.5) / _Height);
                    

                    float4 pos = tex2Dlod(_PosTex, float4(true_uv.xy, 0.0, 0.0));
                
                    const float3x3 local_rot_scale = float3x3 (_LocalToWorld[0][0], _LocalToWorld[0][1], _LocalToWorld[0][2],
                        _LocalToWorld[1][0], _LocalToWorld[1][1], _LocalToWorld[1][2],
                        _LocalToWorld[2][0], _LocalToWorld[2][1], _LocalToWorld[2][2]);

                    const float local_scale = length(mul(local_rot_scale, float3(1, 1, 1)));
                    const float p_size =  _PointSize * local_scale;

                    float4 s_pos;
                    if (_NumVertex == 3)
                    {
                        float4 v_pos =  mul(UNITY_MATRIX_V, mul(_LocalToWorld, float4(pos.xyz, 1.0)));
                        v_pos = v_pos + float4(tri[vert_id]*scale*p_size, 0);
                        o.uv_splat = tri[vert_id].xy;
                        s_pos = mul(UNITY_MATRIX_P, v_pos);
                    }
                    else
                    {
                        s_pos = mul(UNITY_MATRIX_VP, mul(_LocalToWorld, float4(pos.xyz, 1.0)));
                        o.uv_splat = float2(0.0, 0.0);
                    }

                    o.pos = float4(s_pos.x * (pos.a > 0.5) - 100 * (pos.a < 0.5), s_pos.y, s_pos.z, s_pos.w);
                    o.uv = true_uv;
                    o.size = p_size/o.pos.w;
                    return o;
            }

            float _LinearCorrection;
            float _InvSigmaSq;

            fixed4 frag (v2f i) : SV_Target
            {
                float alpha = 1;
                if (_NumVertex == 3){
                    float sq_norm_uv = i.uv_splat.x*i.uv_splat.x + i.uv_splat.y*i.uv_splat.y;
                    if (sq_norm_uv>1){
                        discard;
                    }
                    else{
                        // alpha = 1 - sqrt(sq_norm_uv); //Linear
                        // alpha = 1 - sq_norm_uv; //Square
                        alpha = exp(-sq_norm_uv*_InvSigmaSq);
                    }
                }

                fixed4 sRGBA = tex2D(_ColTex, i.uv);
                fixed3 sRGB = sRGBA.rgb;
                float3 RGB = sRGB * (sRGB * (sRGB * 0.305306011 + 0.682171111) + 0.012522878);
                return fixed4((_LinearCorrection) * RGB + (1 - _LinearCorrection) * sRGBA.rgb, alpha);


                
            }
            ENDCG
        }
    }
}
