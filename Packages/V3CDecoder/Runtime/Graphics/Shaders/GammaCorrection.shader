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

Shader "IDCC/GammaCorrection"
{
    Properties
    {   
        _MainTex("Texture", 2D) = "white" {} 
        _UseGamma("Use sRBG to RGB correction (0 no corretion, 1 correction)", Range(0.0, 1.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float _UseGamma;

            fixed4 frag(v2f i): SV_Target
            {
                // sample the texture
                float4 sRGBA = tex2D(_MainTex, i.uv);
                float3 sRGB = sRGBA.rgb;

                //Better sRGB ot RGB conversion approximation (faster and more precise than the pow(sRGB, 2.2) gamma function)
                //Credits to http://chilliant.blogspot.com/2012/08/srgb-approximations-for-hlsl.html?m=1
                //(Found it while poking around in ARCore shaders, see the GammaToLinearSpace function in the ARCoreBackground.shader 
                // used in the Unity.XR.ARCore package)
                float3 RGB = sRGB * (sRGB * (sRGB * 0.305306011 + 0.682171111) + 0.012522878);
                
                return fixed4((_UseGamma) *RGB + (1 - _UseGamma) * sRGBA.rgb, sRGBA.a);
                
            }
            ENDCG
        }
    }
}
