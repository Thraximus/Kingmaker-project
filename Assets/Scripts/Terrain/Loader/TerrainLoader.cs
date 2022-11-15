using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainLoader : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private Vector3 DebugVect;
    [SerializeField] private int brushSize;
    [SerializeField] private Texture2D brush;
    [SerializeField] private float brushStrenght;   //  TODO: MAYBE inherit brush strength for every pixel from brush?
    // Start is called before the first frame update
    private float[,] mesh;

    private RaycastHit hit;
    private Ray ray;
    private float realBrushStrenght;
    private int[,]  placeholderBrush= new int[13,2] {{0,0},{0,1},{0,-1},{1,0},{-1,0},{0,2},{0,-2},{2,0},{-2,0},{1,1},{-1,-1},{1,-1},{-1,1}}; // TODO: implement loading custom brushes from png
    private List<int[]> loadedBrush = new List<int[]>();

    

    public int hitX;
    public int hitZ;
    private void Start()
    {
        realBrushStrenght = brushStrenght/500;
        mesh = new float[terrain.terrainData.heightmapResolution,terrain.terrainData.heightmapResolution];
        for( int i = 0; i < mesh.GetLength(0);i++ )
        {
            for( int j = 0; j < mesh.GetLength(1);j++ )
            {
                mesh[i,j] = 0.2f;                           //  set base height 
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

            // for(int i = 0; i < placeholderBrush.Rank;i++)
            // {
            //     if((mesh[hitZ+placeholderBrush[i,0],hitX+placeholderBrush[i,1]]+= realBrushStrenght * modifier) !> 1 || (mesh[hitZ+placeholderBrush[i,0],hitX+placeholderBrush[i,1]]+= realBrushStrenght * modifier) !< 0)
            //     {
            //         mesh[hitZ+placeholderBrush[i,0],hitX+placeholderBrush[i,1]] += realBrushStrenght * modifier;
            //     }
            // }

            foreach(int[] pixelPos in loadedBrush)
            {
                if((mesh[hitZ+pixelPos[0],hitX+pixelPos[1]] += realBrushStrenght * modifier) !> 1 || (mesh[hitZ+pixelPos[0],hitX+pixelPos[1]]+= realBrushStrenght * modifier) !< 0)
                {
                    mesh[hitZ+pixelPos[0],hitX+pixelPos[1]] += realBrushStrenght * modifier;
                }
            }
            this.terrain.terrainData.SetHeights(0,0,mesh);

        }

    }

    private void loadBrushFromPng()
    {
        //  = Resources.Load<Texture2D>("Assets/Brushes/TestBrush.png");  
        for (int i = 0; i < brush.width; i++)
        {
            for (int j = 0; j < brush.height; j++)
            { 
                Color pixel = brush.GetPixel(j, i);
                // if it's a white color then just debug...
                if (pixel == Color.black)
                {
                    this.loadedBrush.Add(new int[] {i-25,j-25});
                }
            }
        }
    }
}
