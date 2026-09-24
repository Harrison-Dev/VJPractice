Shader "Hidden/VJ/LaserVenueComposite"
{
    Properties
    {
        _MainTex ("Live VJ", 2D) = "black" {}
        _VenueTex ("Venue", 2D) = "black" {}
        _Mix ("Venue mix", Range(0,1)) = 0.65
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _VenueTex;
            float _Mix;
            float4 _VenueScale;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float2 venueUv = (input.uv - 0.5) * _VenueScale.xy + 0.5;
                float3 live = tex2D(_MainTex, input.uv).rgb;
                float3 venue = tex2D(_VenueTex, venueUv).rgb;
                // Neutral live typography is kept opaque; saturated stage lights
                // remain visible only at their brightest points.
                float neutralLight = min(live.r, min(live.g, live.b));
                float peakLight = max(live.r, max(live.g, live.b));
                float preserve = max(smoothstep(0.28, 0.52, neutralLight),
                                     smoothstep(0.82, 0.98, peakLight));
                float blend = _Mix * (1.0 - preserve);
                return fixed4(lerp(live, venue, blend), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
