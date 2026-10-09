Shader "Lutra/Colorblind"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Blend Off
        Cull Off

        Pass
        {
            Name "Colorblind"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Daltonización (corrección), no simulación. En cada píxel:
            //   1. sim  = cómo lo ve la persona con daltonismo (matriz _Sim*, Machado et al., 2009)
            //   2. err  = color original − sim (la información que no percibe)
            //   3. out  = original + _Shift * err (esa información pasa a los canales que sí distingue)
            // Las matrices se aplican en RGB lineal: el proyecto usa espacio de color Linear.
            float3 _SimR;
            float3 _SimG;
            float3 _SimB;
            float3 _ShiftR;
            float3 _ShiftG;
            float3 _ShiftB;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                float3 sim = float3(dot(col.rgb, _SimR), dot(col.rgb, _SimG), dot(col.rgb, _SimB));
                float3 err = col.rgb - sim;
                float3 result = col.rgb + float3(dot(err, _ShiftR), dot(err, _ShiftG), dot(err, _ShiftB));

                return half4(saturate(result), col.a);
            }
            ENDHLSL
        }
    }
}
