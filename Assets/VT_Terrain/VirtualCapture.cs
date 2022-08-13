using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VirtualCapture : MonoBehaviour {
	private Material[] captureMats;
	public Shader captureShader;
	public TerrainData terrainData;

	public Texture2DArray albedoAtlas;
	public Texture2DArray normalAtlas;
	public RenderTexture[] clipRTs;
	public int mipmapCount;
	public const int virtualTextArraySize = 512;
	// Use this for initialization
	void Awake () {
		
		mipmapCount = (int)Mathf.Log(virtualTextArraySize, 2);
		clipRTs = new RenderTexture[2];// 
		for (int i = 0; i < clipRTs.Length; i++)
		{

			clipRTs[i] = new RenderTexture(virtualTextArraySize, virtualTextArraySize, 0, i == 0 ? RenderTextureFormat.ARGB32 : RenderTextureFormat.ARGB32, i == 0 ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
			clipRTs[i].useMipMap = true;
			clipRTs[i].autoGenerateMips = false;
			clipRTs[i].Create();

		}



		captureMats = new Material[2];


		for (int i = 0; i < captureMats.Length; i++)
		{


			var mat = new Material(captureShader);
            for (int k = 0; k < terrainData.alphamapTextures.Length; k++)
            {
				mat.SetTexture("_Control"+k, terrainData.alphamapTextures[k]);
			}
			
 

			captureMats[i] = mat;
		}

		var tileData = new Vector4[terrainData.splatPrototypes.Length];
		for (int i = 0; i < tileData.Length; i++)
		{
			tileData[i] = new Vector4(terrainData.size.x / terrainData.splatPrototypes[i].tileSize.x, terrainData.size.z / terrainData.splatPrototypes[i].tileSize.y, 0, 0);


		}
		captureMats[0].EnableKeyword("_BLIT_ALBEDO");
		captureMats[1].DisableKeyword("_BLIT_ALBEDO");
		Shader.SetGlobalTexture("albedoAtlas", albedoAtlas);
		Shader.SetGlobalTexture("normalAtlas", normalAtlas);
		Shader.SetGlobalVectorArray("tileData", tileData);
		Shader.SetGlobalInt("virtualTextArraySize", virtualTextArraySize);



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
	public	RenderTexture virtualCapture(Vector2 center, float size,int mode)
	{

		 
		int terrainSize = (int)terrainData.size.x;

		Shader.SetGlobalVector("blitOffsetScale", new Vector4((center.x - size / 2) / terrainSize, (center.y - size / 2) / terrainSize, (size) / terrainSize, (size) / terrainSize));
		Graphics.Blit(null, clipRTs[mode], captureMats[mode], 0);
		clipRTs[mode].GenerateMips();
		return clipRTs[mode];

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
