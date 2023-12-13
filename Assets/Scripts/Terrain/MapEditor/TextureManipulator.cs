using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureManipulator : MonoBehaviour
{
    public int allTextureVariants = -0;
    [System.Serializable] public struct SplatHeights
    {
        public int textureIndex;
        public float startingHeight;
    };
    [HideInInspector] public SplatHeights[] splatHeights;
    public struct TerrainTexture
    {
        public string name;
        public int beginIndex;
        public int endIndex;
    }
    [HideInInspector]public TerrainTexture[] terrainTextures;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Changes the entire alpha chanel of input texture to 0 (Changes the original texture)
    /// </summary>
    /// <param name="texture">Texture whose alpha channel is changed.</param>
    public void ClearAlphaFromTexture(ref Texture2D texture) 
    {
        Color[] pixels = texture.GetPixels();
        for (int i=0 ; i < pixels.Length; i++)
        {
            pixels[i].a = 0;
        }
        texture.SetPixels(pixels);
        texture.Apply();
    }

    public void SetSplatHeights(SplatHeights[] splatHeightsData)
    {

        splatHeights = splatHeightsData;
    }

    public List<string> getTextureNames()
    {
        List<string> textureNames = new List<string>();
        foreach (TerrainTexture texture in terrainTextures)
        {
            textureNames.Add(texture.name);
        }

        return textureNames;
    }

    public int getNumOfUniqueTextures()
    {
        return terrainTextures.Length;
    }
    /// <summary>
    /// Call to automatically texture the entire map. Texturing is done based on the terrain height.
    /// </summary>
    public void AutoTextureTerrain(TerrainData terrainData)
    {

        float[,,] splatmapData = new float[terrainData.alphamapWidth,terrainData.alphamapHeight,terrainData.alphamapLayers];
        int tempTextureIndex = -1;

        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                float terrainHeight = terrainData.GetHeight(y,x);
                float[] splat = new float[allTextureVariants];
                
                for (int i = 0; i < splatHeights.Length; i++)
                {
                    tempTextureIndex = Random.Range(terrainTextures[splatHeights[i].textureIndex].beginIndex,terrainTextures[splatHeights[i].textureIndex].endIndex);
                    if(i == splatHeights.Length-1)
                    {
                        if(terrainHeight >= splatHeights[i].startingHeight/10)
                        {
                            splat[tempTextureIndex] = 1;
                        }
                    }
                    else if(i < splatHeights.Length-1)
                    {
                        if(terrainHeight >= splatHeights[i].startingHeight/10 && terrainHeight <= splatHeights[i+1].startingHeight/10)
                        {
                            splat[tempTextureIndex] = 1;
                        }
                    } 
                }

                for(int j=0; j < splat.Length;j++)
                {
                    splatmapData[x,y,j] = splat[j];
                }
            }
        }

        terrainData.SetAlphamaps(0,0,splatmapData);
    }

}
