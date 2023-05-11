using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainEditor : MonoBehaviour
{
    [System.Serializable] public struct SplatHeights
    {
        public int textureIndex;
        public float startingHeight;
    };
    [SerializeField] private Terrain terrain;
    [SerializeField] private Material riverMaterial;
    [SerializeField] private Material oceanMaterial;
    [SerializeField] private ComputeShader brushDropoffShader;
     public float brushSize;                       // Scale of brush (default 1)                                       TODO: unserialize
    [SerializeField] public string mapNameForLoadSave;             // Name under which the map will be saved                           TODO: unserialize
    [SerializeField] private  Texture2D heightmapSaveLoadBuffer;    // Memory buffer used to store the map to be loaded or to be saved  TODO: unserialize
    [SerializeField] private Texture2D originalBrush;               // Original brush loaded from file                                  TODO: unserialize
    [SerializeField] private Texture2D brushForManipulation;        // Brush stored in memory, and manipulated(scaled)                  TODO: unserialize
    [HideInInspector] public float brushStrength;                   //                                                                  TODO: MAYBE inherit brush strength for every pixel from brush?
    [SerializeField] private float brushScaleIncrement = 0.1f;      // Increment in which the brush gets scaled up
    [HideInInspector] public SplatHeights[] splatHeights;
    [HideInInspector] public string selectedBrush = "circleFullBrush" ;
    [HideInInspector] public bool terrainManipulationEnabled = true; 
    [HideInInspector] public string brushEffect = "SmoothManipultionTool";
    
    struct BrushPixel
    {
        public int xPos;
        public int yPos;
        public float pixelBrushStrength;
    }

    struct TerrainTexture
    {
        public string name;
        public int beginIndex;
        public int endIndex;
    }
    private TerrainTexture[] terrainTextures;

    private int brushLength;
    private float[,] mesh;
    private RaycastHit hit;
    private Ray ray;
    [HideInInspector] public float realBrushStrength;
    private BrushPixel[] loadedBrush;
    private BrushPixel[] computedBrush;
    private int hitX;
    private int hitZ;
    private List<float[,]> terrainUndoStack = new List<float[,]>();
    private List<float[,]> terrainRedoStack = new List<float[,]>();
    enum ActionType
    {
        TERRAIN,
        TEXTURE,
        OBJECT,
        WATER
    }
    private List<ActionType> undoMemoryStack = new List<ActionType>();
    private List<ActionType> redoMemoryStack = new List<ActionType>();
    private bool terrainManipulationActive = false;
    private TerrainData terrainData;
    private int allTextureVariants = -0;
    
  
    private void Start()
    {
        loadTerrainTextures();

        mapNameForLoadSave = "TerrainTextureTest"; // TEMPORARY TODO: REMOVE
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
        LoadBrushFromPngAndCalculateBrushPixels(true,selectedBrush);                                                // TODO: Runtime brush picker

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
            if(!Input.GetMouseButton(0) && !Input.GetMouseButton(1) )
            {
                ClearRedoStack();
                terrainManipulationActive = false;
            }
        }

        if(Input.GetMouseButton(0) && terrainManipulationEnabled && Input.mousePosition.x < Screen.width - (Screen.width/100*22) )
        {
            if(terrainManipulationActive == false)
            {
                AddToTerrainUndoStack();
            }
            RaiseOrLowerTerrain(true);
        }

        if(Input.GetMouseButton(1) && terrainManipulationEnabled && Input.mousePosition.x < Screen.width - (Screen.width/100*22))
        {
            if(terrainManipulationActive == false)
            {
                AddToTerrainUndoStack();
            }
            RaiseOrLowerTerrain(false);
        }

        if(Input.GetKeyUp(KeyCode.K))                                // Temporary save key
        {
            SaveTerrainHeightmapToFolder(mapNameForLoadSave);        // TODO place this function on GUI object
        }

        if(Input.GetKeyUp(KeyCode.L))                                // Temporary load key
        {
            LoadTerrainfromFolder(mapNameForLoadSave);               // TODO place this function on GUI object
        }

        if(Input.GetKeyUp(KeyCode.Comma))                            // Temporary scale down key
        {
            if((brushSize - brushScaleIncrement) < 4)
            {
                brushSize -= brushScaleIncrement;                     // TODO place this function on GUI object
                ScaleBrush();
                LoadBrushFromPngAndCalculateBrushPixels(false);
            }
        }

        if(Input.GetKeyUp(KeyCode.Period))                           // Temporary scale up key
        {
            if((brushSize + brushScaleIncrement) > 0)
            {
                brushSize += brushScaleIncrement;                     // TODO place this function on GUI object
                ScaleBrush();
                LoadBrushFromPngAndCalculateBrushPixels(false);
            }
        }

        if(Input.GetKeyUp(KeyCode.G))   // Temporary undo key       replace G with Input.GetKeyUp(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.Z)
        {
            UndoAction();               // TODO place this function on GUI object
        }
        if(Input.GetKeyUp(KeyCode.H))   // Temporary undo key       replace G with Input.GetKeyUp(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.Z)
        {
            RedoAction();               // TODO place this function on GUI object
        }

        if(Input.GetKeyUp(KeyCode.T))
        {
            AutoTextureTerrain();
        }

        if(Input.GetKeyUp(KeyCode.R))                                // Temporary save key
        {
            GenerateWater();        // TODO place this function on GUI object
        }
    }

    // -------------------------------- TEXTURE FUNCTIONALITY ------------------------------------------------------------------------------

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

    /// <summary>
    /// Call to automatically texture the entire map. Texturing is done based on the terrain height.
    /// </summary>
    public void AutoTextureTerrain()
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
    // -------------------------------- TEXTURE FUNCTIONALITY END ------------------------------------------------------------------------------

    // ------------------------------- REDO FUNCTIONALITY---------------------------------------------------------------
    private void ClearRedoStack()
    {
        redoMemoryStack.Clear();
        terrainRedoStack.Clear();
    }
    private void AddToTerrainRedoStack()
    {
        redoMemoryStack.Add(ActionType.TERRAIN);
        terrainRedoStack.Add(returnCopyOfMesh(mesh));
    }
    private void RedoAction()
    {
        ActionType previousUndoAction;
        
        if(redoMemoryStack.Count > 0)
        {
            previousUndoAction = redoMemoryStack[redoMemoryStack.Count-1];
            if(redoMemoryStack.Count > 0)
            {
                redoMemoryStack.RemoveAt(redoMemoryStack.Count-1);
            }
            
            if(previousUndoAction == ActionType.TERRAIN)
            {
                RedoTerrainManipulation();
            }
            else if (previousUndoAction == ActionType.TEXTURE)
            {
                RedoTextureManipulation();
            }
            else if (previousUndoAction == ActionType.OBJECT)
            {
                RedoObjectManipulation();
            }
        }
    }
    private void RedoTerrainManipulation()
    {
        AddToTerrainUndoStack();
        this.terrain.terrainData.SetHeights(0,0, terrainRedoStack[terrainRedoStack.Count-1]);
        mesh = returnCopyOfMesh(terrainRedoStack[terrainRedoStack.Count-1]);
        terrainRedoStack.RemoveAt(terrainRedoStack.Count-1);
    }

    private void RedoTextureManipulation()
    {
        // TODO texture undo logic
    }

    private void RedoObjectManipulation()
    {
        // TODO object undo logic
    }

    // ----------------------------------------------- REDO FUNCTIONALITY END ----------------------------------------------------


    // ------------------------------------------------ UNDO FUNCTIONALITY -------------------------------------------------------

    private void UndoAction()
    {
        ActionType previousAction;
        if(undoMemoryStack.Count > 0)
        {
            previousAction = undoMemoryStack[undoMemoryStack.Count-1];
            undoMemoryStack.RemoveAt(undoMemoryStack.Count-1);

            if(previousAction == ActionType.TERRAIN)
            {
                UndoTerrainManipulation();
            }
            else if (previousAction == ActionType.TEXTURE)
            {
                UndoTextureManipulation();
            }
            else if (previousAction == ActionType.OBJECT)
            {
                UndoObjectManipulation();
            }
        }
    }

    private void AddToTerrainUndoStack()
    {
        if (undoMemoryStack.Count > 50)
        {
            undoMemoryStack.RemoveAt(0);
            terrainUndoStack.RemoveAt(0);
        }
        terrainUndoStack.Add(returnCopyOfMesh(mesh));
        undoMemoryStack.Add(ActionType.TERRAIN);
    }


    private void UndoTerrainManipulation()
    {
        AddToTerrainRedoStack();
        this.terrain.terrainData.SetHeights(0,0, terrainUndoStack[terrainUndoStack.Count-1]);
        mesh = returnCopyOfMesh(terrainUndoStack[terrainUndoStack.Count-1]);
        terrainUndoStack.RemoveAt(terrainUndoStack.Count-1);
    }

    private void UndoTextureManipulation()
    {
        // TODO texture undo logic
    }

    private void UndoObjectManipulation()
    {
        // TODO object undo logic
    }

    
    // ----------------------------------------------------- UNDO FUNCTIONALITY END ------------------------------------------------



    // ---------------------------------------------------- LOAD AND SAVE FROM FILE ----------------------------------------------


    /// <summary>
    /// Loads terrain from heightmap in directory. 
    /// </summary>
    /// <param name="mapName">Name under which the map that is being loaded is saved under</param>
    public void LoadTerrainfromFolder(string mapName)
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
            AddToTerrainUndoStack();
        }
        else
        {
            Debug.Log("Map Not Found - implement real error handling function");
        }

    }

    /// <summary>
    /// Saves the heightmap of the map as a .png file for future loading
    /// </summary>
    /// <param name="mapName">The file name for the exported map</param>
    public void SaveTerrainHeightmapToFolder(string mapName)
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


    private void SaveSplatmapsToFolder(string mapName)
    {
        heightmapSaveLoadBuffer =  new Texture2D(terrain.terrainData.alphamapResolution, terrain.terrainData.alphamapResolution);
        for( int i = 0; i < terrain.terrainData.alphamapResolution;i++ )
        {
            for( int j = 0; j < terrain.terrainData.alphamapResolution;j++ )
            {
                Color pixel = new Color(terrain.terrainData.GetAlphamaps(0,0,terrain.terrainData.alphamapWidth,terrain.terrainData.alphamapHeight)[i,j,1],terrain.terrainData.GetAlphamaps(0,0,terrain.terrainData.alphamapWidth,terrain.terrainData.alphamapHeight)[i,j,1],terrain.terrainData.GetAlphamaps(0,0,terrain.terrainData.alphamapWidth,terrain.terrainData.alphamapHeight)[i,j,1],1);
                heightmapSaveLoadBuffer.SetPixel(i,j,pixel);
            }     
        }
        heightmapSaveLoadBuffer.Apply();

        byte[] _bytes =heightmapSaveLoadBuffer.EncodeToPNG();
        System.IO.File.WriteAllBytes("Assets/ExportedHeightmaps/" + mapName + "-alphamap.png", _bytes);

    }


    /// <summary>
    /// Loads brush (Optional).
    /// Calculates the position of black pixels on the loaded brush. 
    /// </summary>
    /// <param name="loadFromFile">Flag to indicate if it should be a new texture loaded from file or just recompute pixels for brush</param>
    /// <param name="brushName">Name of the file in the brush folder without the file extension (file needs to be .png)</param>
    public void LoadBrushFromPngAndCalculateBrushPixels(bool loadFromFile ,string brushName = "")
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
            loadedBrush = new BrushPixel[count];
            computedBrush = new BrushPixel[count];
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
                        loadedBrush[count].pixelBrushStrength = realBrushStrength;
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

    /// <summary>
    /// Loads all terrain textures from the Assets/TerrainTextures folder.   
    /// Each texture should be stored in its own folder.   
    /// Textures can consist of 1 or more variants (in .png format).
    /// </summary>
    public void loadTerrainTextures()
    {
        int uniqueTextures = 0;
        
        string [] directories = System.IO.Directory.GetDirectories("Assets/TerrainTextures");
        foreach (string dir in directories)
        {
            uniqueTextures +=1;
            string [] textures = System.IO.Directory.GetFiles(dir,"*.png");
            
            foreach (string texture in textures)
            {
                allTextureVariants +=1;                
            }
        }

        terrainData = terrain.terrainData;
        TerrainLayer[] terrainTexture = new TerrainLayer[allTextureVariants];
        terrainTextures = new TerrainTexture[uniqueTextures];
        allTextureVariants = 0;
        uniqueTextures = 0;

        foreach (string dir in directories)
        {
            terrainTextures[uniqueTextures].name = dir.Substring(dir.LastIndexOf('\\')+1);
            terrainTextures[uniqueTextures].beginIndex = allTextureVariants;
            int currentTextureNumOfVariations = 0;
            string [] textures = System.IO.Directory.GetFiles(dir,"*.png");
            
            foreach (string texture in textures)
            {     
                currentTextureNumOfVariations+=1;
                byte[] fileData = System.IO.File.ReadAllBytes(texture);
                heightmapSaveLoadBuffer = new Texture2D(2, 2);
                heightmapSaveLoadBuffer.LoadImage(fileData);
                if (true)                                                                               // TODO: add option to pick whether or not to use alpha channel (currently hardcoded to ignore)
                {
                    ClearAlphaFromTexture(heightmapSaveLoadBuffer);
                }
                terrainTexture[allTextureVariants] = new TerrainLayer();
                terrainTexture[allTextureVariants].diffuseTexture = heightmapSaveLoadBuffer;
                terrainTexture[allTextureVariants].name = texture.Substring(texture.LastIndexOf('\\')+1);

                terrainData.terrainLayers = terrainTexture;    
                allTextureVariants +=1;
                   
            }
            if(currentTextureNumOfVariations<2)
            {
                terrainTextures[uniqueTextures].endIndex = terrainTextures[uniqueTextures].beginIndex;
            }
            else if (currentTextureNumOfVariations > 1)
            {
                terrainTextures[uniqueTextures].endIndex = terrainTextures[uniqueTextures].beginIndex + currentTextureNumOfVariations;
            }
            uniqueTextures +=1;
        }
    }

    public int getNumOfUniqueTextures()
    {
        return terrainTextures.Length;
    }

    // --------------------------------------------- LOAD AND SAVE FROM FILE END ---------------------------------------------------------------------------------
    

    // --------------------------------------------- MANIPULATION AND CALCULATIONS -------------------------------------------------------------------------------


    /// <summary>
    /// Changes the entire alpha chanel of input texture to 0 (Changes the original texture)
    /// </summary>
    /// <param name="texture">Texture whose alpha channel is changed.</param>
    private void ClearAlphaFromTexture(Texture2D texture) 
    {
        Color[] pixels = texture.GetPixels();
        for (int i=0 ; i < pixels.Length; i++)
        {
            pixels[i].a = 0;
        }
        texture.SetPixels(pixels);
        texture.Apply();
    }

    /// <summary>
    /// Raises or loweres terrain at the mouse position according to the brush. 
    /// </summary>
    /// <param name="raise">Flag that determines if terains should be lowered or raised (True = raise) (False = lower)</param>
    private void RaiseOrLowerTerrain(bool raise)
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

    // -------------------------------------------------- MANIPULATION AND CALCULATIONS END -------------------------------------------------------------------------------

    // ----------------------------------------------------------------- WATER --------------------------------------------------------------------------------------------


    /// <summary>
    /// Creates a 2 triangle quad plane with the water material 
    /// </summary>
    /// <param name="locationX">The location on the X axis</param>
    /// <param name="locationY">The location on the Y axis (Height)</param>
    /// <param name="locationZ">The location on the Z axis</param>
    /// <param name="width">Width of the plane(X)</param>
    /// <param name="height">Height of the plane (Z)</param>
    /// <param name="useColider">Flag that determines if the water has a colider</param>
    /// <param name="isRiver">Flag to determine if the water is a river or ocean</param>
    public GameObject createWaterPlane(float locationX, float locationY, float locationZ,float width, float height, bool useColider, bool isRiver)
    {
        locationX = locationX - width/2f ;
        locationY = locationY - height/2f;
        locationZ = 0.1f ; // TODO CHANGE TO BE VARIABLE

        GameObject plain = new GameObject("NAME"); //TODO CHANGE NAME TO DYNAMIC
        plain.transform.position = new Vector3(locationX,locationZ,locationY);
        MeshFilter mf = plain.AddComponent(typeof(MeshFilter)) as MeshFilter;
        MeshRenderer mr = plain.AddComponent(typeof(MeshRenderer)) as MeshRenderer;

        Mesh plainMesh = new Mesh();
        plainMesh.vertices = new Vector3[]
        {
            new Vector3(0,0,0),
            new Vector3(width,0,0),
            new Vector3(width,0,height),
            new Vector3(0,0,height)
        };

        plainMesh.uv = new Vector2[]
        {
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1),
            new Vector2(0,0)
        };

        plainMesh.triangles = new int[]{2,1,0,3,2,0};

        mf.mesh = plainMesh;
        if(useColider == true)
        {
            (plain.AddComponent(typeof(MeshCollider)) as MeshCollider).sharedMesh = plainMesh;
        }
        
        if(isRiver)
        {
            mr.material = riverMaterial;
            mr.material.SetFloat("_WaveSpeed",0); // TODO REMOVE , Temporary while rivers dont have a custom material
        }
        else
        {
            mr.material = oceanMaterial;
        }
        plainMesh.RecalculateNormals();
        plainMesh.RecalculateBounds();

        return plain;
    }

    /// <summary>
    /// Combines multiple game objects into one (efectively creates a new meged mesh into a new object and deletes the existing objects)
    /// </summary>
    /// <param name="mergeObjects">List of all the objects whose meshes need to be merged</param>
    private GameObject combineMeshes(MeshFilter[] mergeObjects )
    {
         CombineInstance[] combine = new CombineInstance[mergeObjects.Length];

        int i = 0;
        while (i < mergeObjects.Length)
        {
            combine[i].mesh = mergeObjects[i].sharedMesh;
            combine[i].transform = mergeObjects[i].transform.localToWorldMatrix;
            mergeObjects[i].gameObject.SetActive(false);

            i++;
        }
       
        Mesh mesh = new Mesh();
        mesh.CombineMeshes(combine);
        GameObject combinedMesh = new GameObject("CombinedWater"); // TODO make the name dynamic
        MeshFilter mf = combinedMesh.AddComponent(typeof(MeshFilter)) as MeshFilter;
        MeshRenderer mr = combinedMesh.AddComponent(typeof(MeshRenderer)) as MeshRenderer;
        mr.material = mergeObjects[0].GetComponent<MeshRenderer>().material;

        combinedMesh.GetComponent<MeshFilter>().sharedMesh = mesh;
        combinedMesh.gameObject.SetActive(true);

        for(int j =0;j<mergeObjects.Length;j++)
        {
            Destroy(mergeObjects[j].gameObject);
        }
        return combinedMesh;
    }

     private void GenerateWater() //TODO in dev
    {
        ray = Camera.main.ScreenPointToRay (Input.mousePosition);
        if (Physics.Raycast (ray, out hit)) 
        {
            float mouseX = hit.point.x;
            float mouseY = hit.point.y;
            float mouseZ = hit.point.z;

            // Debug.Log("x"+mouseX);
            // Debug.Log("y"+mouseY);
            // Debug.Log("z"+mouseZ);


            GameObject plane1 = createWaterPlane(mouseX,mouseZ,mouseY,1,1,true,true);
            // GameObject plane2 = createWaterPlane(mouseX+1,mouseY,mouseZ,1,1,true,true);
            // GameObject plane3 = createWaterPlane(mouseX+1,mouseY,mouseZ+1,1,1,true,true);

            // MeshFilter[] meshes = new MeshFilter[]{plane1.GetComponent<MeshFilter>(),plane2.GetComponent<MeshFilter>(),plane3.GetComponent<MeshFilter>()};

            // GameObject combinedWater = combineMeshes(meshes);


            RaycastHit distanceRay;
            int distanceToLeft = 0;
            int distanceToRight = 0;

            if(Physics.Raycast(plane1.transform.position, Vector3.left, out distanceRay))
            {
                Debug.Log("Left distance between points: "+ Mathf.CeilToInt(Vector3.Distance(plane1.transform.position,distanceRay.point)));
                distanceToLeft = Mathf.CeilToInt(Vector3.Distance(plane1.transform.position,distanceRay.point));                
            };

            if(Physics.Raycast(plane1.transform.position, Vector3.right, out distanceRay))
            {
                Debug.Log("Right distance between points: "+ Mathf.CeilToInt(Vector3.Distance(plane1.transform.position,distanceRay.point)));
                distanceToRight = Mathf.CeilToInt(Vector3.Distance(plane1.transform.position,distanceRay.point));
            };


            MeshFilter[] meshes = new MeshFilter[distanceToLeft+distanceToRight+1];
            Debug.Log("total distance: "+ (distanceToLeft+distanceToRight));
            meshes[0] = plane1.GetComponent<MeshFilter>(); // TODO temporary remove later
            for (int i = 1; i< distanceToLeft+1;i++)
            {
                Debug.Log("left side: "+i);
                meshes[i] = createWaterPlane(mouseX-(i),mouseZ,mouseY,1,1,true,true).GetComponent<MeshFilter>();
            }
            for (int i = distanceToLeft; i< distanceToRight+distanceToLeft;i++)
            {
                Debug.Log("right side: "+i);
                meshes[i+1] = createWaterPlane(mouseX+(i-distanceToLeft),mouseZ,mouseY,1,1,true,true).GetComponent<MeshFilter>();
            }

            GameObject combinedWater = combineMeshes(meshes);
        }

    }

    // --------------------------------------------------------------- WATER END-------------------------------------------------------------------------------------------
}

