Shader "VT_Terrain_Blit"
{
	Properties
	{ [HideInInspector] _Control("Control (RGBA)", 2D) = "red" {}
		_MainTex("Texture", 2D) = "white" {}

	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		
		Pass
		{
			cull off
			ztest always
		zwrite off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
          

			#include "UnityCG.cginc"
 
			sampler2D _Control0;
		float4 _Control0_ST;

		sampler2D _Control1;
		float4 _Control1_ST;
		sampler2D _Control2;
		float4 _Control2_ST;

		sampler2D _Control3;
		float4 _Control3_ST;
		float4  blitOffsetScale;
		float4  tileData[16];


		UNITY_DECLARE_TEX2DARRAY(albedoAtlas);
		UNITY_DECLARE_TEX2DARRAY(normalAtlas);
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
				float4 tangent : TANGENT;
			};
			struct PixelOutput {
				float4 col0 : COLOR0;
				float4 col1 : COLOR1;
			};

			struct v2f
			{
				float2 tc_Control0 : TEXCOORD0;
			 
				float4 vertex : SV_POSITION;
			};

			void SplatmapMix(sampler2D _Control, int passIndex, v2f IN, out half4 splat_control, out half weight, out fixed4 mixedDiffuse, out fixed3 mixedNormal)

			{
				splat_control = tex2D(_Control, IN.tc_Control0);

				weight = dot(splat_control, half4(1, 1, 1, 1));

#if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
				clip(weight == 0.0f ? -1 : 1);
#endif

				// Normalize weights before lighting and restore weights in final modifier functions so that the overal
				// lighting result can be correctly weighted.
				splat_control /= (weight + 1e-3f);

				mixedDiffuse = 0.0f;
				mixedNormal = 0.0f;
// #ifdef _BLIT_ALBEDO

				mixedDiffuse += splat_control.r * UNITY_SAMPLE_TEX2DARRAY(albedoAtlas, float3(IN.tc_Control0 * tileData[passIndex * 4].xy, passIndex * 4));
				mixedDiffuse += splat_control.g * UNITY_SAMPLE_TEX2DARRAY(albedoAtlas, float3(IN.tc_Control0 * tileData[passIndex * 4 + 1].xy, passIndex * 4 + 1));
				mixedDiffuse += splat_control.b * UNITY_SAMPLE_TEX2DARRAY(albedoAtlas, float3(IN.tc_Control0 * tileData[passIndex * 4 + 2].xy, passIndex * 4 + 2));
				mixedDiffuse += splat_control.a * UNITY_SAMPLE_TEX2DARRAY(albedoAtlas, float3(IN.tc_Control0 * tileData[passIndex * 4 + 3].xy, passIndex * 4 + 3));

 //#else


				fixed4 nrm = 0.0f;
				nrm += splat_control.r * UNITY_SAMPLE_TEX2DARRAY(normalAtlas, float3(IN.tc_Control0 * tileData[passIndex * 4].xy, passIndex * 4));
				nrm += splat_control.g * UNITY_SAMPLE_TEX2DARRAY(normalAtlas, float3(IN.tc_Control0 * tileData[passIndex * 4 + 1].xy, passIndex * 4 + 1));
				nrm += splat_control.b * UNITY_SAMPLE_TEX2DARRAY(normalAtlas, float3(IN.tc_Control0 * tileData[passIndex * 4 + 2].xy, passIndex * 4 + 2));
				nrm += splat_control.a * UNITY_SAMPLE_TEX2DARRAY(normalAtlas, float3(IN.tc_Control0 * tileData[passIndex * 4 + 3].xy, passIndex * 4 + 3));
				mixedNormal = UnpackNormal(nrm);


// #endif

			}

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				
				v.uv.xy *= blitOffsetScale.zw;
				v.uv.xy += blitOffsetScale.xy;
				o.tc_Control0 = TRANSFORM_TEX(v.uv, _Control0);
			 
			 

				v.normal = half3(0, 1, 0);

				v.tangent.xyz = cross(v.normal, float3(0, 0, 1));
				v.tangent.w = -1;
				return o;
			}
 

			PixelOutput frag(v2f IN) 
			{

				half4 splat_control;
			half weight;
			fixed4 mixedDiffuse;
			fixed3 mixedNormal;
			fixed4 DiffuseAll = 0;
			fixed3 NormalAll = 0;
			SplatmapMix(_Control0,0, IN, splat_control, weight, mixedDiffuse, mixedNormal);
			DiffuseAll += mixedDiffuse * weight;
			NormalAll += (mixedNormal * weight);
			SplatmapMix(_Control1,1, IN, splat_control, weight, mixedDiffuse, mixedNormal);
			DiffuseAll += mixedDiffuse * weight;
			NormalAll += (mixedNormal * weight);

			SplatmapMix(_Control2,2, IN, splat_control, weight, mixedDiffuse, mixedNormal);
			DiffuseAll += mixedDiffuse * weight;
			NormalAll += (mixedNormal * weight);

			//SplatmapMix(_Control3, 3, IN, splat_control, weight, mixedDiffuse, mixedNormal);
			//DiffuseAll += mixedDiffuse * weight;
			//NormalAll += (mixedNormal * weight);
			PixelOutput po;
			po.col0 =  DiffuseAll;
			po.col1 =   half4(NormalAll.xyz * 0.5 + 0.5, 1);
	 
		 
			return po;
			}
 
 
				ENDCG
			}
	}
}
