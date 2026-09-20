Shader "VJ/Geometry" {
Properties { _Blackout("Blackout",Float)=0 }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
Pass { ZWrite Off ZTest Always Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
CBUFFER_START(UnityPerMaterial)
float4 _Control, _Audio, _Clock;
float _Blackout;
CBUFFER_END
V vert(A i) { V o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.uv=i.uv; return o; }
float stroke(float d,float w) { return 1-smoothstep(w,w+0.004,abs(d)); }
half4 frag(V i):SV_Target {
 float2 p=(i.positionCS.xy/_ScreenParams.xy-.5)*2; p.x*=_ScreenParams.x/_ScreenParams.y;
 float t=_Clock.x, e=_Control.x, den=lerp(3,14,_Control.y);
 p*=lerp(1.6,.65,_Control.z);
 float a=t*.12, sn=sin(a), cs=cos(a); p=mul(float2x2(cs,-sn,sn,cs),p);
 float r=length(p), angle=atan2(p.y,p.x);
 float pulse=pow(1-frac(_Clock.y),4)*(.015+.055*e)+_Audio.x*.16;
 float warp=sin(angle*6+t*.7)*(.015+e*.05)*smoothstep(.1,.8,r);
 float rings=stroke(sin((r+warp-pulse)*den*3.14159-t*.8)/den,.006+e*.003);
 float spokes=stroke(sin(angle*6+t*.15)*r,.003)*smoothstep(.4,1.3,r)*e*.5;
 float3 cold=float3(.13,.72,.85), warm=float3(1,.25,.45);
 float3 ink=lerp(cold,warm,saturate(_Control.w+sin(r*2-t*.3)*.2));
 float fade=exp(-r*r*.55)*smoothstep(.08,.32,r);
 float3 color=float3(.009,.014,.035)+(rings+spokes)*ink*fade*(.45+e*.8+_Audio.y*.4);
 color+=float3(.6,.8,1)*stroke(r-.21-pulse,.004)*(.5+_Audio.z);
 return half4(color*(1-_Blackout),1);
}
ENDHLSL
}}}
