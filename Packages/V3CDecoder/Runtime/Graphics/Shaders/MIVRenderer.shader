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

Shader "IDCC/MIVRenderer"
{
	Properties
	{
		[NoScaleOffset] _MainTex("Texture", 2D) = "white" {}
		_MaskColor("Mask Color", Color) = (0,0,0,0)
		_AlphaMask("Alpha Mask", Float) = 1 
		_Ratio("Stream Ratio", Float) = 1
	}

		SubShader
		{
			Cull Off
			ZWrite Off

			Pass
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv     : TEXCOORD0;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float4 vertex : SV_POSITION;
				};

				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = float4(2. * v.uv - 1., 0. , 1.);
					o.uv = v.uv;
					return o;
				}

				sampler2D _MainTex;
				float4 _MainTex_TexelSize;
				uniform float _AlphaMask;
				uniform half4 _MaskColor;
                uniform float _Ratio;

				fixed4 frag(v2f i) : SV_Target
				{
					//float ImageAspectRatio = _MainTex_TexelSize.z / _MainTex_TexelSize.w;
					float ImageAspectRatio = _Ratio;
					float DisplayAspectRatio = _ScreenParams.x / _ScreenParams.y;
					float ar = (DisplayAspectRatio / ImageAspectRatio);

					float x = 0.5 + max(1., ar) * (i.uv.x - 0.5);
					float y = 0.5 + max(1., 1. / ar) * (i.uv.y - 0.5);

					fixed4 col;

					if ((x == clamp(x, 0, 1)) && (y == clamp(y, 0, 1)))
						//col = tex2D(_MainTex, float2(x, y));
						col = tex2D(_MainTex, i.uv);
					else
						col = fixed4(0.F, 0.F, 0.F, 0.F);

					return lerp(_MaskColor, col, _AlphaMask);
				}
				ENDCG
			}
		}
}