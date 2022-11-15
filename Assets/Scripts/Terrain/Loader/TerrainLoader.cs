using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainLoader : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private Vector3 DebugVect;
    [SerializeField] private float  DebugHeight;
    [SerializeField] private ComputeShader brushDropoffShader;
    [SerializeField] private int brushSize;
    [SerializeField] private Texture2D brush;
    [SerializeField] private float brushStrenght;   //  TODO: MAYBE inherit brush strength for every pixel from brush?
    // Start is called before the first frame update
    private float[,] mesh;

    private RaycastHit hit;
    private Ray ray;
    private float realBrushStrenght;
    private int[,]  placeholderBrush= new int[13,2] {{0,0},{0,1},{0,-1},{1,0},{-1,0},{0,2},{0,-2},{2,0},{-2,0},{1,1},{-1,-1},{1,-1},{-1,1}}; // TODO: implement loading custom brushes from png
    private brushPixel[] loadedBrush;
    private brushPixel[] computedBrush;

    struct brushPixel
    {
        public int xPos;
        public int yPos;
        public float pixelBrushStrength;
    }

     

    public int hitX;
    public int hitZ;
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
        loadBrushFromPng();
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
            brushDropoffShader.Dispatch(kernel,loadedBrush.Length,1,1);
            buffer.GetData(computedBrush);

            buffer.Dispose();
            
            for(int i=0; i< computedBrush.Length;i++)
            {
                if(hitZ+computedBrush[i].xPos > 0 && hitX+computedBrush[i].yPos > 0)
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

    private void loadBrushFromPng()
    {
        //  = Resources.Load<Texture2D>("Assets/Brushes/TestBrush.png");  
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
}
