Shader "UI/Easy Progress Bar"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _MainColor ("Main Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _StartColor ("Start Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _EndColor ("End Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _BackColor ("Back Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Gradient ("Gradient", Range(0, 1)) = 0
        [HideInInspector] _Roundness ("Roundness", Range(0, 1)) = 0.5
        [HideInInspector] _BorderSize ("Border Size", Range(0, 1)) = 0.2
        [HideInInspector] _FillAmount ("Fill Amount", Range(0, 1)) = 0
        [HideInInspector] _Size ("Size", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

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
            float4 _MainColor;
            float4 _StartColor;
            float4 _EndColor;
            float4 _BackColor;
            fixed _Gradient;
            fixed _Roundness;
            fixed _BorderSize;
            fixed _FillAmount;
            float4 _Size;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed CalculateDistance(float2 position, float roundness, float2 halfSize)
            {
                float2 edge = abs(position) - (halfSize - roundness);
                float outDistance = length(max(edge, 0));
                float insDistance = min(max(edge.x, edge.y), 0);

                return outDistance + insDistance - roundness;
            }

            float ApplyAntialiasing(float distance)
            {
                float f = fwidth(distance) * 0.5;
                return smoothstep(f, -f, distance);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed uvPos;
                float axisHalfSize;

                if (_Size.x < _Size.y)
                {
                    uvPos = i.uv.y;
                    axisHalfSize = _Size.x * 0.5;
                }
                else
                {
                    uvPos = i.uv.x;
                    axisHalfSize = _Size.y * 0.5;
                }

                fixed frameDistance = CalculateDistance((i.uv - 0.5) * _Size, _Roundness * axisHalfSize, _Size * 0.5);
                fixed frameMask = ApplyAntialiasing(frameDistance);
                
                fixed borderDistance = frameDistance + _BorderSize * axisHalfSize;
                fixed borderMask = ApplyAntialiasing(borderDistance);

                fixed fillMask = _FillAmount > uvPos;

                fixed4 fill = lerp(_StartColor, _EndColor, uvPos * _Gradient + _FillAmount * (1 - _Gradient));
                fixed4 fillColor = fixed4(fill.xyz, fill.a * borderMask * fillMask);
                fixed4 backColor = fixed4(_BackColor.xyz, _BackColor.a * frameMask);

                fixed4 mainColor = tex2D(_MainTex, float2(i.uv.x, i.uv.y)) * _MainColor;
                fixed4 color = lerp(backColor, fillColor, max(fillColor.a, (_BorderSize <= 0 && fillMask) * fill.a));
                fixed alpha = max(backColor.a, fillColor.a);

                return mainColor * fixed4(color.xyz, alpha);
            }
            ENDCG
        }
    }
}