using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainEditor : MonoBehaviour
{
    [SerializeField] private Terrain terrain;
    [SerializeField] private ComputeShader brushDropoffShader;
    [SerializeField] private float brushSize;                       // Scale of brush (default 1)                                       TODO: unserialize
    [SerializeField] private string mapNameForLoadSave;             // Name under which the map will be saved                           TODO: unserialize
    [SerializeField] private  Texture2D heightmapSaveLoadBuffer;    // Memory buffer used to store the map to be loaded or to be saved  TODO: unserialize
    [SerializeField] private Texture2D originalBrush;               // Original brush loaded from file                                  TODO: unserialize
    [SerializeField] private Texture2D brushForManipulation;        // Brush stored in memory, and manipulated(scaled)                  TODO: unserialize
    [SerializeField] private float brushStrenght;                   //                                                                  TODO: MAYBE inherit brush strength for every pixel from brush?  TODO: unserialize
    [SerializeField] private float brushScaleIncrement = 0.1f;      // Increment in which the brush gets scaled up


    struct brushPixel
    {
        public int xPos;
        public int yPos;
        public float pixelBrushStrength;
    }

    private int brushLength;
    private float[,] mesh;
    private RaycastHit hit;
    private Ray ray;
    private float realBrushStrenght;
    private brushPixel[] loadedBrush;
    private brushPixel[] computedBrush;
    private int hitX;
    private int hitZ;
    private List<float[,]> terrainUndoStack = new List<float[,]>();
    private List<float[,]> terrainRedoStack = new List<float[,]>();
    enum actionType
    {
        TERRAIN,
        TEXTURE,
        OBJECT
    }
    private List<actionType> undoMemoryStack = new List<actionType>();
    private List<actionType> redoMemoryStack = new List<actionType>();
    private bool terrainManipulationActive = false;
    private TerrainData terrainData;
  
    private void Start()
    {
        mapNameForLoadSave = "TerrainTextureTest"; // TEMPORARY TODO: REMOVE
        realBrushStrenght = brushStrenght/1000;
        mesh = new float[terrain.terrainData.heightmapResolution,terrain.terrainData.heightmapResolution];
        for( int i = 0; i < terrain.terrainData.heightmapResolution;i++ )
        {
            for( int j = 0; j < terrain.terrainData.heightmapResolution;j++ )
            {
                mesh[i,j] = 0.4f;                                                                                   //  set base height 
            }                                                                                                       //  TODO: make custimisable / resetable
        }
        
        this.terrain.terrainData.SetHeights(0,0,mesh);
        loadBrushFromPngAndCalculateBrushPixels(true,"circleFullBrush");                                                // TODO: Runtime brush picker
    }

    private float[,] returnCopyOfMesh(float[,] originalMesh)
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

    // Update is called once per frame
    private void Update()
    {
        if (terrainManipulationActive == true)
        {
            if(!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                clearRedoStack();
                terrainManipulationActive = false;
            }
        }

        if(Input.GetMouseButton(0))
        {
            if(terrainManipulationActive == false)
            {
                addToTerrainUndoStack();
            }
            raiseOrLowerTerrain(true);
        }

        if(Input.GetMouseButton(1))
        {
            if(terrainManipulationActive == false)
            {
                addToTerrainUndoStack();
            }
            raiseOrLowerTerrain(false);
        }

        if(Input.GetKeyUp(KeyCode.K))                                // Temporary save key
        {
            saveTerrainHeightmapToFolder(mapNameForLoadSave);        // TODO place this function on GUI object
        }

        if(Input.GetKeyUp(KeyCode.L))                                // Temporary load key
        {
            loadTerrainfromFolder(mapNameForLoadSave);               // TODO place this function on GUI object
        }

        if(Input.GetKeyUp(KeyCode.Comma))                            // Temporary scale down key
        {
            if((brushSize -= brushScaleIncrement) < 4)
            {
                brushSize -= brushScaleIncrement;                     // TODO place this function on GUI object
                scaleBrush();
                loadBrushFromPngAndCalculateBrushPixels(false);
            }
        }

        if(Input.GetKeyUp(KeyCode.Period))                           // Temporary scale up key
        {
            if((brushSize += brushScaleIncrement) > 0)
            {
                brushSize += brushScaleIncrement;                     // TODO place this function on GUI object
                scaleBrush();
                loadBrushFromPngAndCalculateBrushPixels(false);
            }
        }

        if(Input.GetKeyUp(KeyCode.G))   // Temporary undo key       replace G with Input.GetKeyUp(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.Z)
        {
            undoAction();               // TODO place this function on GUI object
        }
        if(Input.GetKeyUp(KeyCode.H))   // Temporary undo key       replace G with Input.GetKeyUp(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.Z)
        {
            redoAction();               // TODO place this function on GUI object
        }
    }


    // ------------------------------- REDO ---------------------------------------------------------------
    private void clearRedoStack()
    {
        redoMemoryStack.Clear();
        terrainRedoStack.Clear();
    }
    private void addToTerrainRedoStack()
    {
        redoMemoryStack.Add(actionType.TERRAIN);
        terrainRedoStack.Add(returnCopyOfMesh(mesh));
    }
    private void redoAction()
    {
        actionType previousUndoAction;
        
        if(redoMemoryStack.Count > 0)
        {
            previousUndoAction = redoMemoryStack[redoMemoryStack.Count-1];
            if(redoMemoryStack.Count > 0)
            {
                redoMemoryStack.RemoveAt(redoMemoryStack.Count-1);
            }
            
            if(previousUndoAction == actionType.TERRAIN)
            {
                redoTerrainManipulation();
            }
            else if (previousUndoAction == actionType.TEXTURE)
            {
                redoTextureManipulation();
            }
            else if (previousUndoAction == actionType.OBJECT)
            {
                redoObjectManipulation();
            }
        }
    }
    private void redoTerrainManipulation()
    {
        addToTerrainUndoStack();
        this.terrain.terrainData.SetHeights(0,0, terrainRedoStack[terrainRedoStack.Count-1]);
        mesh = returnCopyOfMesh(terrainRedoStack[terrainRedoStack.Count-1]);
        terrainRedoStack.RemoveAt(terrainRedoStack.Count-1);
    }

    private void redoTextureManipulation()
    {
        // TODO texture undo logic
    }

    private void redoObjectManipulation()
    {
        // TODO object undo logic
    }

    // ----------------------------------------------- REDO END ----------------------------------------------------


    // ------------------------------------------------ UNDO -------------------------------------------------------

    private void undoAction()
    {
        actionType previousAction;
        if(undoMemoryStack.Count > 0)
        {
            previousAction = undoMemoryStack[undoMemoryStack.Count-1];
            undoMemoryStack.RemoveAt(undoMemoryStack.Count-1);

            if(previousAction == actionType.TERRAIN)
            {
                undoTerrainManipulation();
            }
            else if (previousAction == actionType.TEXTURE)
            {
                undoTextureManipulation();
            }
            else if (previousAction == actionType.OBJECT)
            {
                undoObjectManipulation();
            }
        }
    }

    private void addToTerrainUndoStack()
    {
        if (undoMemoryStack.Count > 50)
        {
            undoMemoryStack.RemoveAt(0);
            terrainUndoStack.RemoveAt(0);
        }
        terrainUndoStack.Add(returnCopyOfMesh(mesh));
        undoMemoryStack.Add(actionType.TERRAIN);
    }


    private void undoTerrainManipulation()
    {
        addToTerrainRedoStack();
        this.terrain.terrainData.SetHeights(0,0, terrainUndoStack[terrainUndoStack.Count-1]);
        mesh = returnCopyOfMesh(terrainUndoStack[terrainUndoStack.Count-1]);
        terrainUndoStack.RemoveAt(terrainUndoStack.Count-1);
    }

    private void undoTextureManipulation()
    {
        // TODO texture undo logic
    }

    private void undoObjectManipulation()
    {
        // TODO object undo logic
    }

    
    // ----------------------------------------------------- UNDO END ------------------------------------------------



    // ---------------------------------------------------- LOAD AND SAVE FROM FILE ----------------------------------------------


    /// <summary>
    /// Loads terrain from heightmap in directory. 
    /// </summary>
    /// <param name="mapName">Name under which the map that is being loaded is saved under</param>
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
            addToTerrainUndoStack();
        }
        else
        {
            Debug.Log("Map Not Found - implement real error handling function");
        }

    }

    /// <param name="mapName">The file name for the exported map</param>
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


    /// <summary>
    /// Loads brush (Optional).
    /// Calculates the position of black pixels on the loaded brush. 
    /// </summary>
    /// <param name="loadFromFile">Flag to indicate if it should be a new texture loaded from file or just recompute pixels for brush</param>
    /// <param name="brushName">Name of the file in the brush folder without the file extension (file needs to be .png)</param>
    private void loadBrushFromPngAndCalculateBrushPixels(bool loadFromFile ,string brushName = "")
    {
        byte[] fileData;
        Texture2D brushForLoading = null;


        if (System.IO.File.Exists("Assets/Brushes/" + brushName + ".png") || loadFromFile == false)
        {
            if(loadFromFile == true)
            {
                fileData = System.IO.File.ReadAllBytes("Assets/Brushes/" + brushName + ".png");
                originalBrush = new Texture2D(2, 2);
                originalBrush.LoadImage(fileData); //..this will auto-resize the texture dimensions.
                brushForLoading = originalBrush;
                brushForManipulation = originalBrush;
            }
            else
            {
                brushForLoading = brushForManipulation;
            }
            
        
            var count = 0;
            for (int i = 0; i < brushForLoading.width; i++)
            {
                for (int j = 0; j < brushForLoading.height; j++)
                { 
                    Color pixel = brushForLoading.GetPixel(j, i);
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
            for (int i = 0; i < brushForLoading.width; i++)
            {
                for (int j = 0; j < brushForLoading.height; j++)
                { 
                    Color pixel = brushForLoading.GetPixel(j, i);
                    // if it's a white color then just debug...
                    if (pixel == Color.black)
                    {
                        loadedBrush[count].xPos = i- (int)Mathf.Round(brushForLoading.width/2);
                        loadedBrush[count].yPos = j- (int)Mathf.Round(brushForLoading.height/2);
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

    // --------------------------------------------- LOAD AND SAVE FROM FILE END ---------------------------------------------------------------------------------
    

    // --------------------------------------------- MANIPULATION AND CALCULATIONS -------------------------------------------------------------------------------

    /// <summary>
    /// Raises or loweres terrain at the mouse position according to the brush. 
    /// </summary>
    /// <param name="raise">Flag that determines if terains should be lowered or raised (True = raise) (False = lower)</param>
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
            realBrushStrenght = brushStrenght/1000;
            // calculate brush strenghts with compute shader
            brushDropoffShader.SetFloat("brushWidth",brushForManipulation.width);
            brushDropoffShader.SetFloat("brushHeight",brushForManipulation.height);
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
            terrainManipulationActive = true;
        }
    }


    /// <summary>
    /// Scales the loaded brush. 
    /// </summary>
    private void scaleBrush()
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
}

// -------------------------------------------------- MANIPULATION AND CALCULATIONS END -------------------------------------------------------------------------------