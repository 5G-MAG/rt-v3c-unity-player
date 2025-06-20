Shader "Unlit/TestRenderPrimitives"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #define UNITY_INDIRECT_DRAW_ARGS IndirectDrawArgs
            #include "UnityIndirect.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            static float4 tri[] = {float4(0.0, 2.0, 0.0, 0.0), float4(sqrt(3.0), -1.0, 0.0, 0.0), float4(-sqrt(3.0), -1.0, 0.0, 0.0)};

            v2f vert(uint svVertexID: SV_VertexID, uint svInstanceID : SV_InstanceID)
            {
                InitIndirectDrawArgs(0);
                v2f o;
                
                o.pos = UnityObjectToClipPos(tri[svVertexID]);
                o.uv = tri[svVertexID].xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                if (i.uv.x*i.uv.x+i.uv.y*i.uv.y >1){
                    discard;
                }
                return float4(i.uv.xy, 0.0, 1.0);
            }
            ENDCG
        }
    }
}