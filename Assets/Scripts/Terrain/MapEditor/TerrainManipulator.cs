using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainManipulator : MonoBehaviour
{
    private Ray ray;
    private RaycastHit hit;
    private int hitX;
    private int hitZ;
    [SerializeField] public Terrain terrain;
    [HideInInspector] public float realBrushStrength;
    [HideInInspector] public float brushStrength;                   //                             TODO: MAYBE inherit brush strength for every pixel from brush?
    [SerializeField] private ComputeShader brushDropoffShader;
    [SerializeField] public Texture2D brushForManipulation; 
    [HideInInspector] public BrushPixel[] loadedBrush;
    [HideInInspector] public BrushPixel[] computedBrush;
    [HideInInspector] public float[,] mesh;
    [HideInInspector] public bool terrainManipulationActive = false;
    public float brushSize;  
    [SerializeField] public Texture2D originalBrush;  
    public struct BrushPixel
    {
        public int xPos;
        public int yPos;
        public float pixelBrushStrength;
    }
    [HideInInspector] public string brushEffect = "SmoothManipultionTool";
    private int brushLength;

    // Start is called before the first frame update
    void Start()
    {
        realBrushStrength = brushStrength/1000;
        mesh = new float[terrain.terrainData.heightmapResolution,terrain.terrainData.heightmapResolution];
        for( int i = 0; i < terrain.terrainData.heightmapResolution;i++ )
        {
            for( int j = 0; j < terrain.terrainData.heightmapResolution;j++ )
            {
                mesh[i,j] = 0.4f;                                                                                   //  set base height 
            }                                                                                                       //  TODO: make custimisable / resetable
        }
        
        this.terrain.terrainData.SetHeights(0,0,mesh);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public float[,] returnCopyOfMesh(float[,] originalMesh)
    {
        var returnMesh = new float[originalMesh.GetLength(0),originalMesh.GetLength(1)];
        for( int i = 0; i < originalMesh.GetLength(0);i++ )
        {
            for( int j = 0; j < originalMesh.GetLength(1);j++ )
            {
                returnMesh[i,j] = originalMesh[i,j];                                                                                  //  set base height 
            }                                                                                                       //  TODO: make custimisable / resetable
        }

        return returnMesh;
    }


    /// <summary>
    /// Raises or loweres terrain at the mouse position according to the brush. 
    /// </summary>
    /// <param name="raise">Flag that determines if terains should be lowered or raised (True = raise) (False = lower)</param>
    public void RaiseOrLowerTerrain(bool raise)
    {
        var modifier = 1;
        if(!raise)
        {
            modifier = -1;
        }

        ray = Camera.main.ScreenPointToRay (Input.mousePosition);
        if (Physics.Raycast (ray, out hit)) 
        {
            hitZ = Mathf.RoundToInt((hit.point - terrain.GetPosition()).z/terrain.terrainData.size.z * terrain.terrainData.heightmapResolution);
            hitX = Mathf.RoundToInt((hit.point - terrain.GetPosition()).x/terrain.terrainData.size.x * terrain.terrainData.heightmapResolution);
            realBrushStrength = brushStrength/250;
            // calculate brush strenghts with compute shader
            brushDropoffShader.SetFloat("brushWidth",brushForManipulation.width);
            brushDropoffShader.SetFloat("brushHeight",brushForManipulation.height);
            ComputeBuffer buffer = new ComputeBuffer(loadedBrush.Length,sizeof(int)*2+sizeof(float));   
            buffer.SetData(loadedBrush);
            int kernel = brushDropoffShader.FindKernel(brushEffect);
            brushDropoffShader.SetBuffer(kernel, "loadedBrush", buffer);
            brushDropoffShader.Dispatch(kernel,(int)Mathf.Ceil(loadedBrush.Length/64f),1,1);
            brushLength = (int)Mathf.Ceil(loadedBrush.Length/64f);
            buffer.GetData(computedBrush);

            buffer.Dispose();
            
            for(int i=0; i< computedBrush.Length;i++)
            {
                if(hitZ+computedBrush[i].xPos > 0 && hitX+computedBrush[i].yPos > 0 && hitZ+computedBrush[i].xPos < terrain.terrainData.heightmapResolution && hitX+computedBrush[i].yPos < terrain.terrainData.heightmapResolution)
                {
                    
                    if( mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] + computedBrush[i].pixelBrushStrength * modifier * Time.deltaTime < 1  )
                    {
                        mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] += computedBrush[i].pixelBrushStrength * modifier * Time.deltaTime;
                    }
                    else
                    {
                        mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] = 1;
                    }
                    if( mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] + computedBrush[i].pixelBrushStrength * modifier * Time.deltaTime > 0 )
                    {
                        mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] += computedBrush[i].pixelBrushStrength * modifier * Time.deltaTime;
                    }
                    else
                    {
                        mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] = 0;
                    }
                }
                loadedBrush[i].pixelBrushStrength = realBrushStrength;
            }
            this.terrain.terrainData.SetHeights(0,0,mesh);
            terrainManipulationActive = true;
        }
    }


    /// <summary>
    /// Scales the loaded brush. 
    /// </summary>
    public void ScaleBrush()
    {
        int targetWidth = (int)Mathf.Round(originalBrush.width*brushSize);
        int targetHeight = (int)Mathf.Round(originalBrush.height*brushSize);
        Texture2D result=new Texture2D((int)Mathf.Round(originalBrush.width*brushSize),targetHeight,originalBrush.format,false);
        float incX=(1.0f / (float)targetWidth);
        float incY=(1.0f / (float)targetHeight);
        for (int i = 0; i < result.height; ++i) {
         for (int j = 0; j < result.width; ++j) {
             Color newColor = originalBrush.GetPixelBilinear((float)j / (float)result.width, (float)i / (float)result.height);
             result.SetPixel(j, i, newColor);
         }
     }
     result.Apply();
     brushForManipulation = result;
    }

    public ref float[,] getTerrainMeshRef()
    {
        return ref mesh;
    }

    public ref BrushPixel[] getComputedBrushRef()
    {
        return ref computedBrush;
    }

    public ref BrushPixel[] getLoadedBrushRef()
    {
        return ref loadedBrush;
    }

    public ref float getRealBrushStrengthRef()
    {
        return ref realBrushStrength;
    }

    public ref Texture2D getOriginalBrushRef()
    {
        return ref originalBrush;
    }

    public ref Texture2D getBrushForManipulationRef()
    {
        return ref brushForManipulation;
    }
    

}
