Shader "Custom/DashLine" {
    Properties{
        _Color("Main Color", Color) = (1,1,1,1)
        _MainTex("Base Texture", 2D) = "white" {}
        _AnimSpeed("Anim Speed", Range(0,4)) = 0.63
    }

        SubShader{
            Tags {"Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"}
            LOD 200

        CGPROGRAM
        #pragma surface surf Lambert alpha

        sampler2D _MainTex;
        fixed4 _Color;
        float _AnimSpeed;

        struct Input {
            float2 uv_MainTex;
        };

        void surf(Input IN, inout SurfaceOutput o) {
            IN.uv_MainTex.x += _Time.y * _AnimSpeed;
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
        }

        Fallback "Transparent/VertexLit"
}
