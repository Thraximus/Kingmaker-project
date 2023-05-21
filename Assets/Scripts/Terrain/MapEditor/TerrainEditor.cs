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
    [SerializeField] private GameObject waterWaypointObject;
     public float brushSize;                       // Scale of brush (default 1)                                       TODO: unserialize
    [SerializeField] public string mapNameForLoadSave;             // Name under which the map will be saved                           TODO: unserialize
    [SerializeField] private  Texture2D heightmapSaveLoadBuffer;    // Memory buffer used to store the map to be loaded or to be saved  TODO: unserialize
    [SerializeField] private Texture2D originalBrush;               // Original brush loaded from file                                  TODO: unserialize
    [SerializeField] private Texture2D brushForManipulation;        // Brush stored in memory, and manipulated(scaled)                  TODO: unserialize
    [HideInInspector] public float brushStrength;                   //                                                                  TODO: MAYBE inherit brush strength for every pixel from brush?
    [SerializeField] private float brushScaleIncrement = 0.1f;      // Increment in which the brush gets scaled up
    [HideInInspector] public SplatHeights[] splatHeights;
    [HideInInspector] public string selectedBrush = "circleFullBrush" ;
    [HideInInspector] public bool terrainManipulationEnabled = false; 
    [HideInInspector] public bool waterCreationEnabled = true; 
    [HideInInspector] public string brushEffect = "SmoothManipultionTool";
    private List<WaterWaypoint> waypointsForGeneration = new List<WaterWaypoint>();

    private bool placeWaterWaypointEnabeled = false;
    class WaterWaypoint
    {
        public WaterWaypoint previousWaypoint;
        public GameObject currentWaypoint;
        public WaterWaypoint nextWaypoint;
        public Vector3 lookVector;
        public Vector3 yLockedLookVector;

        public WaterWaypoint(WaterWaypoint inputPreviousWaypoint, GameObject inputCurrentWaypoint, WaterWaypoint inputNextWaypoint)
        {
            currentWaypoint = inputCurrentWaypoint;
            nextWaypoint = inputNextWaypoint;
            previousWaypoint = inputPreviousWaypoint;
        }
    }
    private float WaterWaypointHeight=-2f;

    private float waterPrecision = 0.4f;
    struct WaterGrid
    {
        public WaterSquare[][] squareMatrix;
        public WaterWaypoint startingWaypoint;
        public WaterSquare startWaypointSquare;
        public Vector3 originPosition;
    }
    struct WaterSquare
    {
        public Vector3 position;
        public Vertice edgeDownLeft;
        public Vertice edgeDownRight;
        public Vertice edgeUpLeft;
        public Vertice edgeUpRight;
        public bool renderMesh;
        public Vector2 gridId;
    }
    struct Vertice
    {
        public Vector3 position;
        public bool isCalculated;
    }
    private WaterWaypoint pastWaterWaypoint = null;
    private WaterWaypoint currentWaterWaypoint = null;
    
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

        if(Input.GetKeyUp(KeyCode.R) && !placeWaterWaypointEnabeled)                                // Temporary save key
        {
            Debug.Log("enabeled");
            pastWaterWaypoint= null;
            currentWaterWaypoint = null;
            placeWaterWaypointEnabeled = true;    // TODO place this function on GUI object
        }else if(Input.GetKeyUp(KeyCode.R) && placeWaterWaypointEnabeled)                                // Temporary save key
        {
            Debug.Log("disabeled");
            placeWaterWaypointEnabeled = false;       // TODO place this function on GUI object
        }
        if(Input.GetKeyUp(KeyCode.E) && !placeWaterWaypointEnabeled)                                // Temporary save key
        {
            // GenerateWater(0.6f);        // TODO place this function on GUI object
            

            if(waypointsForGeneration.Count > 0)
            {
                foreach(WaterWaypoint startWaypoint in waypointsForGeneration)
                {
                    if(startWaypoint.previousWaypoint == null)
                    {
                        WaterGrid waterGrid = generateVerticeArray(startWaypoint,waterPrecision);
                        populateWaterGrid(ref waterGrid,0.05f);
                        calculateWaterGridHeights(ref waterGrid);
                        GenerateWaterFromWaterGrid(waterGrid);
                    }
                }
                // ClearWaypoints();
            }
        }
        
        if(Input.GetMouseButtonUp(0) && placeWaterWaypointEnabeled && Input.mousePosition.x < Screen.width - (Screen.width/100*22) )
        {
            Debug.Log("click");
            currentWaterWaypoint = CreateWaypointSingle(WaterWaypointHeight);
            WaterWaypointHeight += 0.9f;
            if(pastWaterWaypoint != null)
            {
                currentWaterWaypoint.previousWaypoint = pastWaterWaypoint;
                pastWaterWaypoint.nextWaypoint = currentWaterWaypoint;

                Vector3 lookPos = currentWaterWaypoint.currentWaypoint.transform.position - pastWaterWaypoint.currentWaypoint.transform.position;
                Quaternion newRotation = Quaternion.LookRotation(lookPos);
                pastWaterWaypoint.currentWaypoint.transform.rotation = newRotation;
                currentWaterWaypoint.currentWaypoint.transform.rotation = newRotation;

                currentWaterWaypoint.yLockedLookVector = new Vector3(lookPos.x,0,lookPos.y);
                pastWaterWaypoint.yLockedLookVector = new Vector3(lookPos.x,0,lookPos.y);
                currentWaterWaypoint.lookVector = lookPos;
                pastWaterWaypoint.lookVector = lookPos;

                LineRenderer lr = pastWaterWaypoint.currentWaypoint.AddComponent<LineRenderer>();
                lr.startWidth = 0.05f;
                lr.endWidth = 0.05f;

                lr.SetPosition(0, new Vector3(pastWaterWaypoint.currentWaypoint.transform.position.x,pastWaterWaypoint.currentWaypoint.transform.position.y,pastWaterWaypoint.currentWaypoint.transform.position.z));
                lr.SetPosition(1, new Vector3(currentWaterWaypoint.currentWaypoint.transform.position.x,currentWaterWaypoint.currentWaypoint.transform.position.y,currentWaterWaypoint.currentWaypoint.transform.position.z));
            }

            pastWaterWaypoint = currentWaterWaypoint;
            
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
        // locationX = locationX - width/2f ;
        // locationY = locationY - height/2f;
        // locationZ = 0.1f ; // TODO CHANGE TO BE VARIABLE

        GameObject plain = new GameObject("NAME"); //TODO CHANGE NAME TO DYNAMIC
        plain.transform.position = new Vector3(locationX,locationZ,locationY);
        MeshFilter mf = plain.AddComponent(typeof(MeshFilter)) as MeshFilter;
        MeshRenderer mr = plain.AddComponent(typeof(MeshRenderer)) as MeshRenderer;

        Mesh plainMesh = new Mesh();
        plainMesh.vertices = new Vector3[]
        {
            new Vector3(-(width/2),0,-(height/2)),
            new Vector3((width/2),0,-(height/2)),
            new Vector3((width/2),0,(height/2)),
            new Vector3(-(width/2),0,(height/2))
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
    /// Creates a 2 triangle quad plane with variying vertice heights with the water material 
    /// </summary>
    /// <param name="locationX">The location on the X axis</param>
    /// <param name="locationY">The location on the Y axis (Height)</param>
    /// <param name="locationZ">The location on the Z axis</param>
    /// <param name="width">Width of the plane(X)</param>
    /// <param name="length">Height of the plane (Z)</param>
    /// <param name="useColider">Flag that determines if the water has a colider</param>
    /// <param name="isRiver">Flag to determine if the water is a river or ocean</param>
    public GameObject createCustomWaterPlane(float locationX, float locationY, float locationZ,float width, float length, bool useColider, bool isRiver, float[] cornerHeightArray)
    {
        // locationX = locationX - width/2f ;
        // locationY = locationY - height/2f;
        // locationZ = 0.1f ; // TODO CHANGE TO BE VARIABLE
        // Debug.Log(" Location X: "+ locationX+" location Z : "+ locationZ+" location Y: "+ locationY+" cornerHeightArray[0]: "+ cornerHeightArray[0]+" cornerHeightArray[1]: "+ cornerHeightArray[1]+" cornerHeightArray[2]: "+ cornerHeightArray[2]+" cornerHeightArray[3]: "+ cornerHeightArray[3]);


        GameObject plain = new GameObject("NAME"); //TODO CHANGE NAME TO DYNAMIC
        plain.transform.position = new Vector3(locationX,locationZ,locationY);
        MeshFilter mf = plain.AddComponent(typeof(MeshFilter)) as MeshFilter;
        MeshRenderer mr = plain.AddComponent(typeof(MeshRenderer)) as MeshRenderer;

        Mesh plainMesh = new Mesh();
        plainMesh.vertices = new Vector3[]
        {
            new Vector3(-(width/2),cornerHeightArray[0],-(length/2)),
            new Vector3((width/2),cornerHeightArray[1],-(length/2)),
            new Vector3((width/2),cornerHeightArray[2],(length/2)),
            new Vector3(-(width/2),cornerHeightArray[3],(length/2)),
            new Vector3(0,0,0),

        };

        plainMesh.uv = new Vector2[]
        {
            new Vector2(1,0),
            new Vector2(1,1),
            new Vector2(0,1),
            new Vector2(0,0),
            new Vector2(0.5f,0.5f)
        };

        //plainMesh.triangles = new int[]{2,1,0,3,2,0};
        plainMesh.triangles = new int[]{3,2,4,2,1,4,1,0,4,0,3,4};

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


    public Mesh generateArrowMesh()
    {

        float stemLength=0.6f;
        float stemWidth=0.2f;
        float tipLength=0.4f;
        float tipWidth= 0.5f;
        List<Vector3> verticesList;
        List<int> trianglesList;
    
        Mesh mesh = new Mesh();
         //setup
        verticesList = new List<Vector3>();
        trianglesList = new List<int>();
 
        //stem setup
        Vector3 stemOrigin = new Vector3(0f,0f,-0.5f);
        float stemHalfWidth = stemWidth/2f;
        //Stem points
        verticesList.Add(stemOrigin+(stemHalfWidth*Vector3.right));
        verticesList.Add(stemOrigin+(stemHalfWidth*Vector3.left));
        verticesList.Add(verticesList[0]+(stemLength*Vector3.forward));
        verticesList.Add(verticesList[1]+(stemLength*Vector3.forward));
 
        //Stem triangles
        trianglesList.Add(0);
        trianglesList.Add(1);
        trianglesList.Add(3);
 
        trianglesList.Add(0);
        trianglesList.Add(3);
        trianglesList.Add(2);
        
        //tip setup
        Vector3 tipOrigin = stemLength*Vector3.forward - new Vector3(0f,0f,0.5f);
        float tipHalfWidth = tipWidth/2;
 
        //tip points
        verticesList.Add(tipOrigin+(tipHalfWidth*Vector3.left));
        verticesList.Add(tipOrigin+(tipHalfWidth*Vector3.right));
        verticesList.Add(tipOrigin+(tipLength*Vector3.forward));
 
        //tip triangle
        trianglesList.Add(4);
        trianglesList.Add(6);
        trianglesList.Add(5);
 
        //assign lists to mesh.
        mesh.vertices = verticesList.ToArray();
        mesh.triangles = trianglesList.ToArray();

        return mesh;
    }

    /// <summary>
    /// Combines multiple game objects into one (efectively creates a new meged mesh into a new object and deletes the existing objects)
    /// </summary>
    /// <param name="mergeObjects">List of all the objects whose meshes need to be merged</param>
    private GameObject combineMeshes(List<GameObject> mergeObjects )
    {
         CombineInstance[] combine = new CombineInstance[mergeObjects.Count];

        int i = 0;
        while (i < mergeObjects.Count)
        {
            combine[i].mesh = mergeObjects[i].GetComponent<MeshFilter>().sharedMesh;
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

        for(int j =0;j<mergeObjects.Count;j++)
        {
            Destroy(mergeObjects[j]);
        }
        return combinedMesh;
    }
    

    private void populateWaterGrid(ref WaterGrid gridForGeneration, float precision)
    {
        WaterWaypoint waypointForEvaluation = gridForGeneration.startingWaypoint;
        while(waypointForEvaluation.nextWaypoint != null)
        {
            float distanceBetweenWaypoints = Mathf.Ceil(Vector3.Distance(waypointForEvaluation.currentWaypoint.transform.position,waypointForEvaluation.nextWaypoint.currentWaypoint.transform.position));

            // Debug.Log("Angle between water line 1 and 2: ");
            // Debug.Log(Vector3.Angle(waypointForEvaluation.lookVector,waypointForEvaluation.nextWaypoint.lookVector));
            
            
            for(float j = 0; j< distanceBetweenWaypoints;j+=precision)
            {
                float posX = waypointForEvaluation.currentWaypoint.transform.position.x+j*(waypointForEvaluation.lookVector.normalized.x);
                float posY = waypointForEvaluation.currentWaypoint.transform.position.y+j*(waypointForEvaluation.lookVector.normalized.y);
                float posZ = waypointForEvaluation.currentWaypoint.transform.position.z+j*(waypointForEvaluation.lookVector.normalized.z);

                // Debug.Log("posY: " + posY);
                // Debug.Log("waypointForEvaluation.lookVector: " + waypointForEvaluation.lookVector);
                // Debug.Log("waypointForEvaluation.lookVector.normalized.y: " + waypointForEvaluation.lookVector.normalized.y);
                // Debug.Log("waypointForEvaluation.currentWaypoint.transform.position.y: " + waypointForEvaluation.currentWaypoint.transform.position.y);
                // Debug.Log("Y position at step: "+j+ " is : " + posY);
                // Debug.Log("posZ: " + posZ);

                // Vector3 startingWaypointSquarePos = new Vector3(Mathf.Round(posX-gridForGeneration.originPosition.x),0,Mathf.Round(posZ-gridForGeneration.originPosition.z));
                int squareIdX = Mathf.RoundToInt(posX-gridForGeneration.originPosition.x);
                int squareIdZ = Mathf.RoundToInt(posZ-gridForGeneration.originPosition.z);

                // if(gridForGeneration.squareMatrix[squareIdX][squareIdZ].renderMesh == false)
                // {
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].renderMesh = true;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].edgeDownLeft.position.y = posY;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].edgeDownRight.position.y = posY;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].edgeUpLeft.position.y = posY;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].edgeUpRight.position.y = posY;
                //     gridForGeneration.squareMatrix[squareIdX][squareIdZ].position.y = posY;
                // }

                RaycastHit distanceRay;
                int distanceToLeft = 0;
                int distanceToRight = 0;
                Vector3 traceOrigin = new Vector3(posX,posY,posZ);
                Vector3 rightLookVector;
                Vector3 leftLookVector;
                

                if(Physics.Raycast(traceOrigin, Quaternion.AngleAxis(-90, Vector3.up) * waypointForEvaluation.yLockedLookVector , out distanceRay,6f))
                {
                    // Debug.Log("Left distance between points: "+ Mathf.CeilToInt(Vector3.Distance(traceOrigin,distanceRay.point)));
                    distanceToLeft = Mathf.CeilToInt(Vector3.Distance(traceOrigin,distanceRay.point));     
                    leftLookVector = distanceRay.point - traceOrigin;
                };

                if(Physics.Raycast(traceOrigin, Quaternion.AngleAxis(90, Vector3.up) * waypointForEvaluation.yLockedLookVector, out distanceRay,6f))
                {
                    // Debug.Log("Right distance between points: "+ Mathf.CeilToInt(Vector3.Distance(traceOrigin,distanceRay.point)));
                    distanceToRight = Mathf.CeilToInt(Vector3.Distance(traceOrigin,distanceRay.point));
                    rightLookVector = distanceRay.point - traceOrigin;

                    Debug.Log("right point: "+distanceRay.point);
                };
                Debug.Log("Processing square X: "+(gridForGeneration.squareMatrix[squareIdX][squareIdZ].position.x)+" Y: "+ (gridForGeneration.squareMatrix[squareIdX][squareIdZ].position.z)+" "+ distanceToLeft+" "+distanceToRight);

                for (float i = precision; i< distanceToLeft;i+=precision)
                {
                    var lookVectorRotatedLeft = Quaternion.AngleAxis(-90, Vector3.up) * waypointForEvaluation.yLockedLookVector;
                    
                    float subPosX = posX+i*(lookVectorRotatedLeft.normalized.x);
                    float subPosY = posY+i*(lookVectorRotatedLeft.normalized.y);
                    float subPosZ = posZ+i*(lookVectorRotatedLeft.normalized.z);

                    int subSquareIdX = Mathf.RoundToInt(subPosX-gridForGeneration.originPosition.x);
                    int subSquareIdZ = Mathf.RoundToInt(subPosZ-gridForGeneration.originPosition.z);

                    if(gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh == false)
                    {
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh = true;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownLeft.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownRight.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpLeft.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpRight.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].position.y = posY;
                    }
                }
            
                for (float i = precision; i< distanceToRight;i+=precision)
                {

                    var lookVectorRotatedRight = Quaternion.AngleAxis(90, Vector3.up)*waypointForEvaluation.yLockedLookVector;



                    float subPosX = posX+i*(lookVectorRotatedRight.normalized.x);
                    float subPosY = posY+i*(lookVectorRotatedRight.normalized.y);
                    float subPosZ = posZ+i*(lookVectorRotatedRight.normalized.z);

                    int subSquareIdX = Mathf.RoundToInt(subPosX-gridForGeneration.originPosition.x);
                    int subSquareIdZ = Mathf.RoundToInt(subPosZ-gridForGeneration.originPosition.z);
                    

                    if(gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh == false)
                    {
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].renderMesh = true;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownLeft.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeDownRight.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpLeft.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].edgeUpRight.position.y = posY;
                        gridForGeneration.squareMatrix[subSquareIdX][subSquareIdZ].position.y = posY;

                    }
                }
                
            }
            waypointForEvaluation = waypointForEvaluation.nextWaypoint;
        }

    }

    private void calculateWaterGridHeights(ref WaterGrid gridForGeneration)
    {
        int vertexCount;
        WaterSquare[] squaresForProcessing;
        float vertexHeightValue;
        bool[] squareActiveStatus;
        for(int i=0; i<gridForGeneration.squareMatrix.Length;i++)
        {
            for(int j=0; j<gridForGeneration.squareMatrix[0].Length;j++)
            {
                if(gridForGeneration.squareMatrix[i][j].renderMesh == true)
                {
                    if(gridForGeneration.squareMatrix[i][j].edgeUpRight.isCalculated == false)
                    {
                        if(gridForGeneration.squareMatrix[i+1][j+1].renderMesh == false)
                        {
                            if(!((i+1)>gridForGeneration.squareMatrix.Length) && !((j+1)> gridForGeneration.squareMatrix[0].Length))
                            {
                                

                                vertexCount = 3;
                                squaresForProcessing = new WaterSquare[vertexCount];
                                squareActiveStatus = new bool[vertexCount];
                                squaresForProcessing[0] = gridForGeneration.squareMatrix[i][j];
                                if(gridForGeneration.squareMatrix[i][j].renderMesh == true)
                                {
                                    squareActiveStatus[0] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[0] = false; 
                                }
                                squaresForProcessing[1] = gridForGeneration.squareMatrix[i+1][j];
                                if(gridForGeneration.squareMatrix[i+1][j].renderMesh == true)
                                {
                                    squareActiveStatus[1] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[1] = false; 
                                }

                                squaresForProcessing[2] = gridForGeneration.squareMatrix[i][j+1];
                                if(gridForGeneration.squareMatrix[i][j+1].renderMesh == true)
                                {
                                    squareActiveStatus[2] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[2] = false; 
                                }

                                vertexHeightValue = averageEdgeHeightPerVertex(squaresForProcessing,squareActiveStatus,true,true);

                                gridForGeneration.squareMatrix[i][j].edgeUpRight.position.y = vertexHeightValue;
                                gridForGeneration.squareMatrix[i+1][j].edgeUpLeft.position.y = vertexHeightValue;
                                gridForGeneration.squareMatrix[i][j+1].edgeDownRight.position.y = vertexHeightValue;


                                gridForGeneration.squareMatrix[i][j].edgeUpRight.isCalculated = true;
                                gridForGeneration.squareMatrix[i+1][j].edgeUpLeft.isCalculated = true;
                                gridForGeneration.squareMatrix[i][j+1].edgeDownRight.isCalculated = true;
                            }
                        }
                    }

                    if(gridForGeneration.squareMatrix[i][j].edgeDownLeft.isCalculated == false)
                    {                          
                        if(i-1 < 0)
                        {   
                            if(j-1>0)
                            {
                                vertexCount = 2;
                                squareActiveStatus = new bool[vertexCount];
                                squaresForProcessing = new WaterSquare[vertexCount];
                                squaresForProcessing[0] = gridForGeneration.squareMatrix[i][j];
                                squaresForProcessing[1] = gridForGeneration.squareMatrix[i][j-1];

                                if(gridForGeneration.squareMatrix[i][j].renderMesh)
                                {
                                    squareActiveStatus[0] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[0] = false; 
                                }
                                if(gridForGeneration.squareMatrix[i][j-1].renderMesh)
                                {
                                    squareActiveStatus[1] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[1] = false; 
                                }

                                vertexHeightValue = averageEdgeHeightPerVertex(squaresForProcessing,squareActiveStatus,true,false);

                                gridForGeneration.squareMatrix[i][j].edgeDownLeft.position.y = vertexHeightValue;
                                gridForGeneration.squareMatrix[i][j-1].edgeUpLeft.position.y = vertexHeightValue;

                                gridForGeneration.squareMatrix[i][j].edgeDownLeft.isCalculated = true;
                                gridForGeneration.squareMatrix[i][j-1].edgeUpLeft.isCalculated = true;
                            }
                        }
                        else if(j-1 <0)
                        {
                            if(i-1>0)
                            {
                                vertexCount = 2;
                                squaresForProcessing = new WaterSquare[vertexCount];
                                squareActiveStatus = new bool[vertexCount];
                                squaresForProcessing[0] = gridForGeneration.squareMatrix[i][j];
                                squaresForProcessing[1] = gridForGeneration.squareMatrix[i-1][j];

                                if(gridForGeneration.squareMatrix[i][j].renderMesh)
                                {
                                    squareActiveStatus[0] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[0] = false; 
                                }
                                if(gridForGeneration.squareMatrix[i-1][j].renderMesh)
                                {
                                    squareActiveStatus[1] = true; 
                                }
                                else
                                {
                                    squareActiveStatus[1] = false; 
                                }

                                vertexHeightValue = averageEdgeHeightPerVertex(squaresForProcessing,squareActiveStatus,false,false);

                                gridForGeneration.squareMatrix[i][j].edgeDownLeft.position.y = vertexHeightValue;
                                gridForGeneration.squareMatrix[i-1][j].edgeDownRight.position.y = vertexHeightValue;

                                gridForGeneration.squareMatrix[i][j].edgeDownLeft.isCalculated = true;
                                gridForGeneration.squareMatrix[i-1][j].edgeDownRight.isCalculated = true;
                            }
                        }
                        else
                        {
                            vertexCount = 4;
                            squaresForProcessing = new WaterSquare[vertexCount];
                            squareActiveStatus = new bool[vertexCount];
                            squaresForProcessing[0] = gridForGeneration.squareMatrix[i][j];
                            if(gridForGeneration.squareMatrix[i][j].renderMesh == true)
                            {
                                squareActiveStatus[0] = true; 
                            }
                            else
                            {
                                squareActiveStatus[0] = false; 
                            }
                            squaresForProcessing[1] = gridForGeneration.squareMatrix[i][j-1];
                            if(gridForGeneration.squareMatrix[i][j-1].renderMesh == true)
                            {
                                squareActiveStatus[1] = true; 
                            }
                            else
                            {
                                squareActiveStatus[1] = false; 
                            }
                            squaresForProcessing[2] = gridForGeneration.squareMatrix[i-1][j-1];
                            if(gridForGeneration.squareMatrix[i-1][j-1].renderMesh == true)
                            {
                                squareActiveStatus[2] = true; 
                            }
                            else
                            {
                                squareActiveStatus[2] = false; 
                            }
                            squaresForProcessing[3] = gridForGeneration.squareMatrix[i-1][j];
                            if(gridForGeneration.squareMatrix[i-1][j].renderMesh == true)
                            {
                                squareActiveStatus[3] = true; 
                            }
                            else
                            {
                                squareActiveStatus[3] = false; 
                            }

                            vertexHeightValue = averageEdgeHeightPerVertex(squaresForProcessing,squareActiveStatus,true,false);

                            gridForGeneration.squareMatrix[i][j].edgeDownLeft.position.y = vertexHeightValue;
                            gridForGeneration.squareMatrix[i][j-1].edgeUpLeft.position.y = vertexHeightValue;
                            gridForGeneration.squareMatrix[i-1][j-1].edgeUpRight.position.y = vertexHeightValue;
                            gridForGeneration.squareMatrix[i-1][j].edgeDownRight.position.y = vertexHeightValue;


                            gridForGeneration.squareMatrix[i][j].edgeDownLeft.isCalculated = true;
                            gridForGeneration.squareMatrix[i][j-1].edgeUpLeft.isCalculated = true;
                            gridForGeneration.squareMatrix[i-1][j-1].edgeUpRight.isCalculated = true;
                            gridForGeneration.squareMatrix[i-1][j].edgeDownRight.isCalculated = true;
                        }
                        
                    }
                }
            }
        }
    }

    private void GenerateWaterFromWaterGrid(WaterGrid gridForGeneration)
    {
        List<GameObject> meshes = new List<GameObject>();
        for(int i=0; i<gridForGeneration.squareMatrix.Length;i++)
        {
            for(int j=0; j<gridForGeneration.squareMatrix[0].Length;j++)
            {
                if(gridForGeneration.squareMatrix[i][j].renderMesh == true)
                {
                    float[] heightArray = {gridForGeneration.squareMatrix[i][j].edgeDownLeft.position.y-gridForGeneration.squareMatrix[i][j].position.y,
                    gridForGeneration.squareMatrix[i][j].edgeDownRight.position.y-gridForGeneration.squareMatrix[i][j].position.y,
                    gridForGeneration.squareMatrix[i][j].edgeUpRight.position.y-gridForGeneration.squareMatrix[i][j].position.y,
                    gridForGeneration.squareMatrix[i][j].edgeUpLeft.position.y-gridForGeneration.squareMatrix[i][j].position.y};
                    
                    float[] heightArrayDefault = {0,0,0,0};

                    meshes.Add(createCustomWaterPlane(gridForGeneration.squareMatrix[i][j].position.x,gridForGeneration.squareMatrix[i][j].position.z,gridForGeneration.squareMatrix[i][j].position.y,1,1,true,true,heightArray));
                }
            }
        }
        GameObject combinedWater = combineMeshes(meshes);
    }

    private float averageEdgeHeightPerVertex( WaterSquare[] squaresForProcessing, bool[] squareActiveStatus, bool vertical, bool topRightStitch)
    {
        float finalHeight=0f;
        int numOfSquares=0;
        if(topRightStitch == true)
        {
            if(squaresForProcessing.Length == 3)
            {
                if (squareActiveStatus[0] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[0].edgeUpRight.position.y;
                }
                if (squareActiveStatus[1] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[1].edgeUpLeft.position.y;
                }
                if (squareActiveStatus[2] == true )
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[2].edgeDownRight.position.y;
                }
                finalHeight = finalHeight/numOfSquares;
            }
            else
            {
                if(vertical)
                {
                    if (squareActiveStatus[0] == true)
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[0].edgeUpRight.position.y;
                    }
                    if (squareActiveStatus[1] == true)
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[1].edgeDownRight.position.y;
                    }
                    finalHeight = finalHeight/numOfSquares;
                }
                else
                {
                    if (squareActiveStatus[0] == true)
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[0].edgeUpRight.position.y;
                    }
                    if (squareActiveStatus[1] == true)
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[1].edgeUpLeft.position.y;
                    }
                    finalHeight = finalHeight/numOfSquares;
                }
            }
        }
        else
        {
            if(squaresForProcessing.Length == 4)
            {
                if (squareActiveStatus[0] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[0].edgeDownLeft.position.y;
                }
                if (squareActiveStatus[1] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[1].edgeUpLeft.position.y;
                }
                if (squareActiveStatus[2] == true )
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[2].edgeUpRight.position.y;
                }
                if (squareActiveStatus[3] == true)
                {
                    numOfSquares++;
                    finalHeight += squaresForProcessing[3].edgeDownRight.position.y;
                }
                finalHeight = finalHeight/numOfSquares;
            }
            else
            {
                if(vertical)
                {
                    if (squareActiveStatus[0])
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[0].edgeDownLeft.position.y;
                    }
                    if (squareActiveStatus[1])
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[1].edgeUpLeft.position.y;
                    }
                    finalHeight = finalHeight/numOfSquares;
                }
                else
                {
                    if (squareActiveStatus[0])
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[0].edgeDownLeft.position.y;
                    }
                    if (squareActiveStatus[1])
                    {
                        numOfSquares++;
                        finalHeight += squaresForProcessing[1].edgeDownRight.position.y;
                    }
                    finalHeight = finalHeight/numOfSquares;
                }
            }
        }
        

        return finalHeight;
    }

    private float[][] averageEdgeHeight( WaterSquare squareForAverageCentral, WaterSquare squareForAverageOutside, int xOffset, int zOffset)
    {
        float cornerOne = 0f;
        float cornerTwo = 0f;

        float[][] returnHeightTotal= new float[2][];

        switch((xOffset, zOffset,squareForAverageOutside.renderMesh))
        {
            case(-1,-1,true):
                // bottom left / top right 
                cornerOne = (squareForAverageOutside.edgeUpRight.position.y+squareForAverageCentral.edgeDownLeft.position.y) /2f;

                returnHeightTotal[0] = makeHieghtArray(cornerOne,squareForAverageCentral.edgeDownRight.position.y,squareForAverageCentral.edgeUpRight.position.y,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = makeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,squareForAverageOutside.edgeDownRight.position.y,cornerOne,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;


            case(0,-1,true):
                // bottom right, bottom left / top right, top left

                cornerOne = (squareForAverageOutside.edgeUpRight.position.y+squareForAverageCentral.edgeDownRight.position.y) / 2f;
                cornerTwo = (squareForAverageOutside.edgeUpLeft.position.y+squareForAverageCentral.edgeDownLeft.position.y) /2f;

                returnHeightTotal[0] = makeHieghtArray(cornerTwo,cornerOne,squareForAverageCentral.edgeUpRight.position.y,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = makeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,squareForAverageOutside.edgeDownRight.position.y,cornerOne,cornerTwo);
                
                return returnHeightTotal;


            case(1,-1,true):
                // bottom right /top left 
                cornerOne = (squareForAverageOutside.edgeUpLeft.position.y+squareForAverageCentral.edgeDownRight.position.y) /2f;

                returnHeightTotal[0] = makeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,cornerOne,squareForAverageCentral.edgeUpRight.position.y,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = makeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,squareForAverageOutside.edgeDownRight.position.y,squareForAverageOutside.edgeUpRight.position.y,cornerOne);

                return returnHeightTotal;


            case(-1,0,true):
                // top left bottom left / top right bottom right

                cornerOne = (squareForAverageOutside.edgeUpRight.position.y+squareForAverageCentral.edgeUpLeft.position.y) /2f;
                cornerTwo = (squareForAverageOutside.edgeDownRight.position.y+squareForAverageCentral.edgeDownLeft.position.y) /2f;

                returnHeightTotal[0] = makeHieghtArray(cornerTwo,squareForAverageCentral.edgeDownRight.position.y,squareForAverageCentral.edgeUpRight.position.y,cornerOne);
                returnHeightTotal[1] = makeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,cornerTwo,cornerOne,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;

            case(0,0,true):
                // ignore
            break;

            case(1,0,true):
                // top right bottom right / top left bottom left

                cornerOne = (squareForAverageOutside.edgeUpLeft.position.y+squareForAverageCentral.edgeUpRight.position.y) /2f;
                cornerTwo = (squareForAverageOutside.edgeDownLeft.position.y+squareForAverageCentral.edgeDownRight.position.y) /2f;

                returnHeightTotal[0] = makeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,cornerTwo,cornerOne,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = makeHieghtArray(cornerTwo,squareForAverageOutside.edgeDownRight.position.y,squareForAverageOutside.edgeUpRight.position.y,cornerOne);

                return returnHeightTotal;

            case(-1,1,true):
                //  top left / bottom right 

                cornerOne = (squareForAverageOutside.edgeDownRight.position.y+squareForAverageCentral.edgeUpLeft.position.y) /2f;

                returnHeightTotal[0] = makeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,squareForAverageCentral.edgeDownRight.position.y,squareForAverageCentral.edgeUpRight.position.y,cornerOne);
                returnHeightTotal[1] = makeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,cornerOne,squareForAverageOutside.edgeUpRight.position.y,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;

            case(0,1,true):
                // top left top right / bottom left  bottom right 

                cornerOne = (squareForAverageOutside.edgeDownLeft.position.y+squareForAverageCentral.edgeUpLeft.position.y) /2f;
                cornerTwo = (squareForAverageOutside.edgeDownRight.position.y+squareForAverageCentral.edgeUpRight.position.y) /2f;

                returnHeightTotal[0] = makeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,squareForAverageCentral.edgeDownRight.position.y,cornerTwo,cornerOne);
                returnHeightTotal[1] = makeHieghtArray(cornerOne,cornerTwo,squareForAverageOutside.edgeUpRight.position.y,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;

            case(1,1,true):
                //  top right / bottom left

                cornerOne = (squareForAverageOutside.edgeDownLeft.position.y+squareForAverageCentral.edgeUpRight.position.y) /2f;

                returnHeightTotal[0] = makeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,squareForAverageCentral.edgeDownRight.position.y,cornerOne,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = makeHieghtArray(cornerOne,squareForAverageOutside.edgeDownRight.position.y,squareForAverageOutside.edgeUpRight.position.y,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;
            default:
                Debug.Log("Borked!!!");

                returnHeightTotal[0] = makeHieghtArray(squareForAverageCentral.edgeDownLeft.position.y,squareForAverageCentral.edgeDownRight.position.y,squareForAverageCentral.edgeUpRight.position.y,squareForAverageCentral.edgeUpLeft.position.y);
                returnHeightTotal[1] = makeHieghtArray(squareForAverageOutside.edgeDownLeft.position.y,squareForAverageOutside.edgeDownRight.position.y,squareForAverageOutside.edgeUpRight.position.y,squareForAverageOutside.edgeUpLeft.position.y);

                return returnHeightTotal;
        }
        return null;
    }

    private float[] makeHieghtArray(float cornerOne, float cornerTwo, float cornerThree, float cornerFour)
    {
        float[] returnArray = new float[4];
        returnArray[0] = cornerTwo;
        returnArray[1] = cornerOne;
        returnArray[2] = cornerThree;
        returnArray[3] = cornerFour;

        return returnArray;
    }

    
    private WaterGrid generateVerticeArray(WaterWaypoint waypoint, float precision)
    {
        float maxWidth = waypoint.currentWaypoint.transform.position.x;
        float minWidth = waypoint.currentWaypoint.transform.position.x;
        float maxLength = waypoint.currentWaypoint.transform.position.z;
        float minLength = waypoint.currentWaypoint.transform.position.z;

        WaterGrid returnGrid = new WaterGrid();
        returnGrid.startingWaypoint = waypoint;

        WaterWaypoint minXWaypoint = waypoint;
        WaterWaypoint minYWaypoint = waypoint;

        while (waypoint != null)
        {
            // Debug.Log("Processing waypoint: ");
            if(maxWidth < waypoint.currentWaypoint.transform.position.x)
            {
                maxWidth = waypoint.currentWaypoint.transform.position.x;
            }
            else if (minWidth > waypoint.currentWaypoint.transform.position.x)
            {
                minWidth = waypoint.currentWaypoint.transform.position.x;
                minXWaypoint = waypoint;
            }

            if(maxLength < waypoint.currentWaypoint.transform.position.z)
            {
                maxLength = waypoint.currentWaypoint.transform.position.z;
            }
            else if (minLength > waypoint.currentWaypoint.transform.position.z)
            {
                minYWaypoint = waypoint;
                minLength = waypoint.currentWaypoint.transform.position.z;
            }
            
            waypoint = waypoint.nextWaypoint;
        }
       
        int arrayX = Mathf.CeilToInt(maxWidth)-Mathf.CeilToInt(minWidth)+20;
        int arrayY = Mathf.CeilToInt(maxLength)-Mathf.CeilToInt(minLength)+20;


        Vector3 gridStartingLocation = new Vector3(minXWaypoint.currentWaypoint.transform.position.x-10, 0, minYWaypoint.currentWaypoint.transform.position.z-10);
        Vector3 startingWaypointSquarePos = new Vector3(Mathf.Round(returnGrid.startingWaypoint.currentWaypoint.transform.position.x-gridStartingLocation.x),0,Mathf.Round(returnGrid.startingWaypoint.currentWaypoint.transform.position.z-gridStartingLocation.z));


        WaterSquare[][] squareMatrix = new WaterSquare[arrayX][];
        for (int i = 0; i < arrayX;i++)
        {
            squareMatrix[i] = new WaterSquare[arrayY];
            for (int j = 0; j < arrayY;j++)
            {
                squareMatrix[i][j] = new WaterSquare();
                squareMatrix[i][j].gridId = new Vector2(i,j);
                squareMatrix[i][j].position = new Vector3(gridStartingLocation.x+i,0,gridStartingLocation.z+j);
                squareMatrix[i][j].edgeDownLeft.position = new Vector3(squareMatrix[i][j].position.x-0.5f,0,squareMatrix[i][j].position.z-0.5f);
                squareMatrix[i][j].edgeDownRight.position = new Vector3(squareMatrix[i][j].position.x+0.5f,0,squareMatrix[i][j].position.z-0.5f);
                squareMatrix[i][j].edgeUpLeft.position = new Vector3(squareMatrix[i][j].position.x-0.5f,0,squareMatrix[i][j].position.z+0.5f);
                squareMatrix[i][j].edgeUpRight.position = new Vector3(squareMatrix[i][j].position.x+0.5f,0,squareMatrix[i][j].position.z+0.5f);
                squareMatrix[i][j].renderMesh = false;
            }
        }

        returnGrid.startWaypointSquare = squareMatrix[Mathf.RoundToInt(startingWaypointSquarePos.x)][Mathf.RoundToInt(startingWaypointSquarePos.z)];
        returnGrid.originPosition = gridStartingLocation;


        returnGrid.squareMatrix = squareMatrix;
        return returnGrid;
    }

    
    
    
    private WaterWaypoint CreateWaypointSingle(float height) //TODO in dev
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

            GameObject waterStartWaypoint = Instantiate(waterWaypointObject, new Vector3(0,0,0),Quaternion.identity);
            waterStartWaypoint.GetComponent<MeshFilter>().mesh = generateArrowMesh();
            waterStartWaypoint.transform.position = new Vector3(mouseX,height,mouseZ);

            WaterWaypoint waypoint = new WaterWaypoint(null,waterStartWaypoint,null);
            // Debug.Log(waypoint.currentWaypoint);
            // Debug.Log(waypoint.nextWaypoint);
            waypointsForGeneration.Add(waypoint);
            return waypoint;
            
        }
        return null;
    }

    private void ClearWaypoints()
    {
        foreach(WaterWaypoint waypoint in waypointsForGeneration)
        {
            Destroy(waypoint.currentWaypoint);
        }
        waypointsForGeneration.Clear();
    }

    // --------------------------------------------------------------- WATER END-------------------------------------------------------------------------------------------
}

