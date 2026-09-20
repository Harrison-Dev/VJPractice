Shader "VJ/StageVisual" {
Properties { _MainTex("History",2D)="black"{} }
SubShader { Cull Off ZWrite Off ZTest Always
Pass {
HLSLPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;
float4 _Control, _Bands, _Clock, _Primary, _Accent, _Background;
float _Mode, _Aspect, _Echo;
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float glow(float d,float w){float g=w/(abs(d)+w);return g*g;}
float3 ink(float f){return lerp(_Primary.rgb,_Accent.rgb,saturate(f));}
float4 frag(v2f_img i):SV_Target {
 float2 uv=i.uv, p=(uv-.5)*2;p.x*=_Aspect;
 float t=_Clock.x,e=_Control.x,density=_Control.y, pulse=pow(1-frac(_Clock.y),5)*e*.3+_Bands.x*.35;
 float r=length(p),a=atan2(p.y,p.x);
 float3 col=_Background.rgb*(.12+uv.y*.10);
 if(_Mode<.5) {
   // An airy, asymmetrical field with a luminous orbit and drifting light petals.
   float2 center=p-float2(.55,.1);float rad=length(center);
   float orbit=glow(rad-(.67+pulse*.12),.005)+glow(rad-.73,.002)*.6;
   col+=ink(uv.y)*orbit*.7;
   for(int j=0;j<5;j++) {
     float f=j*.73, wave=.36*sin(p.x*1.6+t*.22+f)+.2*sin(p.x*3-t*.15+f)-.68;
     col+=ink(j*.2)*glow(p.y-wave,.005)*(.10+e*.17);
   }
   float2 grid=p*lerp(5,12,density)+float2(t*.13,-t*.22);float2 id=floor(grid),q=frac(grid)-.5;
   float h=hash(id); q+=float2(sin(h*30+t*.6),cos(h*17+t*.4))*.2;
   float petal=exp(-dot(q*float2(1,2.5),q*float2(1,2.5))*250);
   col+=ink(h)*petal*step(.55,h)*(.3+.7*e+.3*_Bands.z);
   col+=_Accent.rgb*.05*exp(-rad*rad*2);
 } else if(_Mode<1.5) {
   // Perspective rectangular gates: diagonal neon planes with a clear vanishing point.
   float angle=.15*sin(t*.1);float sn=sin(angle),cs=cos(angle);p=mul(float2x2(cs,-sn,sn,cs),p);
   for(int j=0;j<18;j++) {
     float z=frac(j/18.0+t*.055);float s=.08+z*z*3.7;
     float box=max(abs(p.x)/1.55,abs(p.y));float edge=glow(box-s,.002+z*.005);
     col+=ink(frac(j*.13+t*.015))*edge*(.10+z*.35+e*.24)*smoothstep(0,.1,z)*(1-z*.4);
   }
   col+=ink(uv.x)*glow(p.y-.32*sin(p.x*3+t+_Bands.y),.003)*(.18+e*.2);
   col+=_Accent.rgb*exp(-r*r*4)*(.05+pulse*.2);
 } else if(_Mode<2.5) {
   // Orbital interference: nested ellipses and sweeping filaments.
   for(int j=0;j<12;j++) {
     float rot=j*.2618+t*.07;float2 q=mul(float2x2(cos(rot),-sin(rot),sin(rot),cos(rot)),p);
     float ellipse=length(q*float2(.6,1.5))-(.68+pulse*.2);
     col+=ink(j/12.0)*glow(ellipse,.0035)*(.10+.2*e);
   }
   float spiral=sin(a*5-r*9+t*.6+_Bands.x);
   col+=ink(.4)*glow(spiral,.007)*smoothstep(.4,.9,r)*exp(-r*.8)*(.1+.22*e);
 }
 if(_Mode>=2.5&&_Mode<3.5){
   float depth=1/max(.08,r);float tunnel=abs(frac(depth*.7-t*(.25+_Bands.x*.12))-.5);float spokes=abs(sin(a*(6+floor(density*8))+t*.15));col+=ink(frac(depth*.1))*glow(tunnel,.016)*smoothstep(.05,.3,r)*(.15+e*.65);col+=_Accent.rgb*glow(spokes,.015)*r*(.08+_Bands.z*.2);col*=smoothstep(.03,.22,r);
 }else if(_Mode>=3.5&&_Mode<4.5){
   float2 grid=p*float2(14,10);float2 id=floor(grid),cell=frac(grid)-.5;float h=hash(id);float sweep=frac(t*.12+h*.8);float bar=step(abs(cell.x),.32)*step(abs(cell.y),.32);float on=step(.55-.35*_Bands.y,sin(id.x*.35+t*1.3+sin(id.y*.4))* .5+.5);col+=ink(h)*bar*on*(.12+e*.6)*(.3+.7*pow(sweep,3));float scan=glow(p.y-sin(t*.3)*1.2,.012);col+=_Accent.rgb*scan*.4;
 }else if(_Mode>=4.5){
   float2 q=p;for(int k=0;k<4;k++){q+=.15*sin(q.yx*(2.1+k*.7)+float2(t*.22,-t*.31)+k);}
   float field=sin(q.x*4+t*.5)+cos(q.y*5-t*.4)+sin((q.x+q.y)*3+_Bands.x*2);float stripe=glow(sin(field*(2+density*4)),.025);col+=ink(sin(field+t*.1)*.5+.5)*(stripe*(.25+e*.65)+.025*field*field);col+=_Accent.rgb*glow(field,.03)*(.15+_Bands.y*.5);
 }
 float2 historyUv=(uv-.5)*(.997-_Control.z*.001)+.5+float2(.0005*sin(t*.1),.0003);
 float3 history=tex2D(_MainTex,historyUv).rgb;
 float echo=saturate(_Echo)*.88;
 col*=.86+.14*(1-smoothstep(.2,1.8,r));
 col=1-exp(-col*1.65);
 col=max(col,history*echo);
 return float4(col,1);
}
ENDHLSL
}}}
