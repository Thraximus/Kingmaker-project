using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainLoader : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private ComputeShader brushDropoffShader;
    [SerializeField] private int brushSize;
    
    [SerializeField] private int brushLength;
    [SerializeField] private string mapNameForLoadSave;
    [SerializeField] private  Texture2D heightmapSaveLoadBuffer;
    [SerializeField] private Texture2D brush;
    [SerializeField] private float brushStrenght;   //  TODO: MAYBE inherit brush strength for every pixel from brush?
    // Start is called before the first frame update
    private float[,] mesh;

    private RaycastHit hit;
    private Ray ray;
    private float realBrushStrenght;
    // private int[,]  placeholderBrush= new int[13,2] {{0,0},{0,1},{0,-1},{1,0},{-1,0},{0,2},{0,-2},{2,0},{-2,0},{1,1},{-1,-1},{1,-1},{-1,1}};  TODO REMOVE
    private brushPixel[] loadedBrush;
    private brushPixel[] computedBrush;

    struct brushPixel
    {
        public int xPos;
        public int yPos;
        public float pixelBrushStrength;
    }


    private int hitX;
    private int hitZ;
    private void Start()
    {
        realBrushStrenght = brushStrenght/500;
        mesh = new float[terrain.terrainData.heightmapResolution,terrain.terrainData.heightmapResolution];
        for( int i = 0; i < terrain.terrainData.heightmapResolution;i++ )
        {
            for( int j = 0; j < terrain.terrainData.heightmapResolution;j++ )
            {
                mesh[i,j] = 0.4f;                           //  set base height 
            }                                               //  TODO: make custimisable / resetable
        }
        
        this.terrain.terrainData.SetHeights(0,0,mesh);
        loadBrushFromPng("squareBrush");
    }

    // Update is called once per frame
    private void Update()
    {
        if(Input.GetMouseButton(0))
        {
            raiseOrLowerTerrain(true);
        }

        if(Input.GetMouseButton(1))
        {
            raiseOrLowerTerrain(false);
        }

        if(Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))                              
        {
            undoTerrainManipulation();      
        }


        if(Input.GetKeyUp(KeyCode.K))                               // Temporary save key
        {
            saveTerrainHeightmapToFolder(mapNameForLoadSave);       // TODO place this function on GUI object
        }

        if(Input.GetKeyUp(KeyCode.L))                               // Temporary load key
        {
            loadTerrainfromFolder(mapNameForLoadSave);              // TODO place this function on GUI object
        }
    }


    private void undoTerrainManipulation()
    {

    }


    private void loadTerrainfromFolder(string mapName)
    {
        byte[] fileData;

        if (System.IO.File.Exists("Assets/ExportedHeightmaps/" + mapName + ".png"))
        {
            fileData = System.IO.File.ReadAllBytes("Assets/ExportedHeightmaps/" + mapName + ".png");
            heightmapSaveLoadBuffer = new Texture2D(2, 2);
            heightmapSaveLoadBuffer.LoadImage(fileData); //..this will auto-resize the texture dimensions.
        

            mesh = new float[terrain.terrainData.heightmapResolution,terrain.terrainData.heightmapResolution];
            for( int i = 0; i < terrain.terrainData.heightmapResolution;i++ )
            {
                for( int j = 0; j < terrain.terrainData.heightmapResolution;j++ )
                {
                    
                    mesh[i,j] = heightmapSaveLoadBuffer.GetPixel(j,i).g;
                }  
            }
            this.terrain.terrainData.SetHeights(0,0,mesh);
        }
        else
        {
            Debug.Log("Map Not Found - implement real error handling function");
        }

    }
    private void saveTerrainHeightmapToFolder(string mapName)
    {
        heightmapSaveLoadBuffer =  new Texture2D(terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution);
        for( int i = 0; i < terrain.terrainData.heightmapResolution;i++ )
        {
            for( int j = 0; j < terrain.terrainData.heightmapResolution;j++ )
            {
                Color pixel = new Color(mesh[j,i],mesh[j,i],mesh[j,i],1);
                heightmapSaveLoadBuffer.SetPixel(i,j,pixel);
            }     
        }
        heightmapSaveLoadBuffer.Apply();

        byte[] _bytes =heightmapSaveLoadBuffer.EncodeToPNG();
        System.IO.File.WriteAllBytes("Assets/ExportedHeightmaps/" + mapName + ".png", _bytes);

    }
    
    private void raiseOrLowerTerrain(bool raise)
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

            // calculate brush strenghts with compute shader
            brushDropoffShader.SetFloat("brushWidth",brush.width);
            brushDropoffShader.SetFloat("brushHeight",brush.height);
            ComputeBuffer buffer = new ComputeBuffer(loadedBrush.Length,sizeof(int)*2+sizeof(float));   
            buffer.SetData(loadedBrush);
            int kernel = brushDropoffShader.FindKernel("SmoothManipultionTool");
            brushDropoffShader.SetBuffer(kernel, "loadedBrush", buffer);
            brushDropoffShader.Dispatch(kernel,(int)Mathf.Ceil(loadedBrush.Length/64f),1,1);
            brushLength = (int)Mathf.Ceil(loadedBrush.Length/64f);
            buffer.GetData(computedBrush);

            buffer.Dispose();
            
            for(int i=0; i< computedBrush.Length;i++)
            {
                if(hitZ+computedBrush[i].xPos > 0 && hitX+computedBrush[i].yPos > 0 && hitZ+computedBrush[i].xPos < terrain.terrainData.heightmapResolution && hitX+computedBrush[i].yPos < terrain.terrainData.heightmapResolution)
                {
                    
                    if( mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] + computedBrush[i].pixelBrushStrength * modifier < 1  )
                    {
                        mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] += computedBrush[i].pixelBrushStrength * modifier;
                    }
                    else
                    {
                        mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] = 1;
                    }
                    if( mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] + computedBrush[i].pixelBrushStrength * modifier > 0 )
                    {
                        mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] += computedBrush[i].pixelBrushStrength * modifier;
                    }
                    else
                    {
                        mesh[hitZ+computedBrush[i].xPos,hitX+computedBrush[i].yPos] = 0;
                    }
                }
                loadedBrush[i].pixelBrushStrength = realBrushStrenght;
            }
            this.terrain.terrainData.SetHeights(0,0,mesh);
        }
    }

    private void loadBrushFromPng(string brushName)
    {
        brush = null;
        byte[] fileData;

        if (System.IO.File.Exists("Assets/Brushes/" + brushName + ".png"))
        {
            fileData = System.IO.File.ReadAllBytes("Assets/Brushes/" + brushName + ".png");
            brush = new Texture2D(2, 2);
            brush.LoadImage(fileData); //..this will auto-resize the texture dimensions.
        
            var count = 0;
            for (int i = 0; i < brush.width; i++)
            {
                for (int j = 0; j < brush.height; j++)
                { 
                    Color pixel = brush.GetPixel(j, i);
                    // if it's a white color then just debug...
                    if (pixel == Color.black)
                    {
                        count+=1;
                    }
                }
            }
            loadedBrush = new brushPixel[count];
            computedBrush = new brushPixel[count];
            count = 0;
            for (int i = 0; i < brush.width; i++)
            {
                for (int j = 0; j < brush.height; j++)
                { 
                    Color pixel = brush.GetPixel(j, i);
                    // if it's a white color then just debug...
                    if (pixel == Color.black)
                    {
                        loadedBrush[count].xPos = i-25;
                        loadedBrush[count].yPos = j-25;
                        loadedBrush[count].pixelBrushStrength = realBrushStrenght;
                        count+=1;
                    }
                }
            }
        }
        else
        {
            Debug.Log("Brush Not Found - implement real error handling function");
        }
    }
}
