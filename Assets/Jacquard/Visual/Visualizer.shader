Shader "Jacquard/Visualizer"
{
    Properties
    {
    }

HLSLINCLUDE

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Nothing but the colour the mesh was built with. Everything about what is drawn —
// where a column of the waveform sits, how far a voice slot has risen, how far the
// ends of the trace are faded out — is decided on the CPU and arrives as vertices and
// vertex colours, because all of it is a reading of the mix rather than a shape: there
// is no parameter here a shader could interpolate that the geometry does not already
// carry.

void Vert(float4 position : POSITION,
          float4 color : COLOR,
          out float4 outPosition : SV_Position,
          out float4 outColor : COLOR)
{
    outPosition = TransformObjectToHClip(position.xyz);
    outColor = color;
}

float4 Frag(float4 position : SV_Position,
            float4 color : COLOR) : SV_Target
{
    return color;
}

ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline"
               "RenderType" = "Transparent"
               "Queue" = "Transparent" }

        Pass
        {
            Name "VisualizerPass"

            // Behind everything and in front of nothing: this is a background, so it
            // writes no depth, tests against none, and is simply laid over whatever the
            // camera cleared to.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
