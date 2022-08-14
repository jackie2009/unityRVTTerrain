using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VirtualCapture : MonoBehaviour {
	private Material captureMat;
	public Shader captureShader;
	public TerrainData terrainData;

	public Texture2DArray albedoAtlas;
	public Texture2DArray normalAtlas;
	public RenderTexture[] clipRTs;
	private RenderBuffer[] mrtRB = new RenderBuffer[2];
	public int mipmapCount;
	public const int virtualTextArraySize = 512;
	// Use this for initialization
	void Awake () {
		
		mipmapCount = (int)Mathf.Log(virtualTextArraySize, 2);
		clipRTs = new RenderTexture[2];// 
		for (int i = 0; i < clipRTs.Length; i++)
		{

			clipRTs[i] = new RenderTexture(virtualTextArraySize, virtualTextArraySize, 16, i == 0 ? RenderTextureFormat.ARGB32 : RenderTextureFormat.ARGB32, i == 0 ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
			clipRTs[i].useMipMap = true;
			clipRTs[i].autoGenerateMips = false;
			clipRTs[i].Create();

		}








		captureMat = new Material(captureShader);
            for (int k = 0; k < terrainData.alphamapTextures.Length; k++)
            {
			captureMat.SetTexture("_Control"+k, terrainData.alphamapTextures[k]);
			}
			
 

		 

		var tileData = new Vector4[terrainData.splatPrototypes.Length];
		for (int i = 0; i < tileData.Length; i++)
		{
			tileData[i] = new Vector4(terrainData.size.x / terrainData.splatPrototypes[i].tileSize.x, terrainData.size.z / terrainData.splatPrototypes[i].tileSize.y, 0, 0);


		}
 
		Shader.SetGlobalTexture("albedoAtlas", albedoAtlas);
		Shader.SetGlobalTexture("normalAtlas", normalAtlas);
		Shader.SetGlobalVectorArray("tileData", tileData);
		Shader.SetGlobalInt("virtualTextArraySize", virtualTextArraySize);

		//mrt mode
		mrtRB = new RenderBuffer[] { clipRTs[0].colorBuffer, clipRTs[1].colorBuffer };
		 

	}
	void OnDestroy()
	{
		if (clipRTs != null)
		{
			for (int i = 0; i < clipRTs.Length; i++)
			{

				clipRTs[i].Release();


			}
		}

	}
 
 
	public void  virtualCapture_MRT(Vector2 center, float size, out RenderTexture albedoRT,out RenderTexture normalRT)
	{

		int terrainSize = (int)terrainData.size.x;

		Shader.SetGlobalVector("blitOffsetScale", new Vector4((center.x - size / 2) / terrainSize, (center.y - size / 2) / terrainSize, (size) / terrainSize, (size) / terrainSize));

		RenderTexture oldRT = RenderTexture.active;

		Graphics.SetRenderTarget(mrtRB,clipRTs[0].depthBuffer);

		GL.Clear(false, true, Color.clear);

		GL.PushMatrix();
		GL.LoadOrtho();

		captureMat.SetPass(0);     //Pass 0 outputs 2 render textures.

		//Render the full screen quad manually.
		GL.Begin(GL.QUADS);
		GL.TexCoord2(0.0f, 0.0f); GL.Vertex3(0.0f, 0.0f, 0.1f);
		GL.TexCoord2(1.0f, 0.0f); GL.Vertex3(1.0f, 0.0f, 0.1f);
		GL.TexCoord2(1.0f, 1.0f); GL.Vertex3(1.0f, 1.0f, 0.1f);
		GL.TexCoord2(0.0f, 1.0f); GL.Vertex3(0.0f, 1.0f, 0.1f);
		GL.End();

		GL.PopMatrix();

		RenderTexture.active = oldRT;
		albedoRT = clipRTs[0];
		normalRT = clipRTs[1];
		albedoRT.GenerateMips();
		normalRT.GenerateMips();
	}

#if UNITY_EDITOR
	[ContextMenu("MakeAlbedoAtlas")]
	// Update is called once per frame
	void MakeAlbedoAtlas()
	{
		var arrayLen= terrainData.splatPrototypes.Length;
	 
		int wid = terrainData.splatPrototypes[0].texture.width;
		int hei = terrainData.splatPrototypes[0].texture.height;

		int widNormal = terrainData.splatPrototypes[0].normalMap.width;
		int heiNormal = terrainData.splatPrototypes[0].normalMap.height;
		albedoAtlas = new Texture2DArray(wid, hei, arrayLen, terrainData.splatPrototypes[0].texture.format, true, false);
		normalAtlas = new Texture2DArray(widNormal, heiNormal, arrayLen, terrainData.splatPrototypes[0].normalMap.format, true, true);

		for (int index = 0; index < arrayLen; index++)
		{
		  

				if (index >= terrainData.splatPrototypes.Length) break;
				print(index);
				for (int k = 0; k < terrainData.splatPrototypes[index].texture.mipmapCount; k++)
				{
					Graphics.CopyTexture(terrainData.splatPrototypes[index].texture, 0, k, albedoAtlas, index, k);

				}
				for (int k = 0; k < terrainData.splatPrototypes[index].normalMap.mipmapCount; k++)
				{
					Graphics.CopyTexture(terrainData.splatPrototypes[index].normalMap, 0, k, normalAtlas, index, k);

				}

			 
		}


	}
#endif
}
