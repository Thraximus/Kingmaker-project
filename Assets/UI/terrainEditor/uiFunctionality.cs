using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class uiFunctionality : MonoBehaviour
{
    public TerrainEditor terrain;
    // Start is called before the first frame update
    private VisualElement root;

    private TextureManipulator.SplatHeights[] splatHeightsLocal;
    TextureManipulator textureManipulator = null;
    TerrainManipulator terrainManipulator = null;

    private void OnEnable() 
    {
        
        root = GetComponent<UIDocument>().rootVisualElement;
        terrainManipulator = terrain.GetComponent<TerrainManipulator>();
        textureManipulator = terrain.GetComponent<TextureManipulator>();
        handleTextures();
        handleSaveAndLoad();
        handleTerrain();

        

    }

    private void handleTerrain()
    {
        handleBrushPicker();
        handleBrushEffectPicker();
        handleEnableTerrainManipulation();
        handleBrushStrength();
        handleBrushSize();
        handleAutoTextureSetter();
    }
    private void handleBrushStrength()
    {
        SliderInt brushStrengthSlider = root.Q<SliderInt>("BrushStrength");
        terrainManipulator.brushStrength = brushStrengthSlider.value;
        brushStrengthSlider.RegisterValueChangedCallback((evt) =>
        {
            terrainManipulator.brushStrength = brushStrengthSlider.value;
        });
    }

    public void handleBrushSize()
    {
        Slider brushSizeSlider = root.Q<Slider>("BrushSize");
        terrainManipulator.brushSize = brushSizeSlider.value;
        brushSizeSlider.RegisterValueChangedCallback((evt) =>
        {
            Debug.Log(brushSizeSlider.value);
            terrainManipulator.brushSize = brushSizeSlider.value;
            terrainManipulator.ScaleBrush();
            terrain.GetComponent<IOHandler>().LoadBrushFromPngAndCalculateBrushPixels(ref terrainManipulator.getRealBrushStrengthRef(), ref terrainManipulator.getOriginalBrushRef(), ref terrainManipulator.getBrushForManipulationRef(), ref terrainManipulator.getLoadedBrushRef(), ref terrainManipulator.getComputedBrushRef(), false);
        });
    }

    private void handleEnableTerrainManipulation()
    {
        Toggle enableTerrainManipuation = root.Q<Toggle>("EnableTerrainManipuation");
        enableTerrainManipuation.value = terrain.terrainManipulationEnabled;
        enableTerrainManipuation.RegisterValueChangedCallback((evt) =>
        {
            terrain.terrainManipulationEnabled = enableTerrainManipuation.value;
        });
    }
    private void handleBrushPicker()
    {
        DropdownField brushPicker = root.Q<DropdownField>("BrushPicker");
        brushPicker.choices = new List<string> {"circleFullBrush", "circleEmptyBrush", "craterBrush", "squareBrush", "dotBrush"}; // TODO : LOAD  BRUSHES FROM FILES
        brushPicker.SetValueWithoutNotify("circleFullBrush");
        brushPicker.RegisterValueChangedCallback((evt) => {
             terrain.selectedBrush = brushPicker.value;
             terrain.GetComponent<IOHandler>().LoadBrushFromPngAndCalculateBrushPixels(ref terrainManipulator.getRealBrushStrengthRef(), ref terrainManipulator.getOriginalBrushRef(), ref terrainManipulator.getBrushForManipulationRef(), ref terrainManipulator.getLoadedBrushRef(), ref terrainManipulator.getComputedBrushRef(), true, terrain.selectedBrush);
        });
    }

    private void handleAutoTextureSetter()
    {
        
        Foldout textureCutoffs = root.Q<Foldout>("TextureCutoffs");   
        terrain.GetComponent<IOHandler>().loadTerrainTextures(terrain, ref terrain.terrainData, ref textureManipulator.allTextureVariants, ref terrain.terrain, ref textureManipulator.terrainTextures, ref terrain.heightmapSaveLoadBuffer );  // has to be done to be sure that textures are loaded
        splatHeightsLocal = new TextureManipulator.SplatHeights[textureManipulator.getNumOfUniqueTextures()];

        for(int i=0; i< textureManipulator.getNumOfUniqueTextures();i++)
        {
            textureCutoffs.Add(createTextureDataObject(i,textureManipulator.getNumOfUniqueTextures()));
        }
        textureCutoffs.value = false;
        textureManipulator.SetSplatHeights(splatHeightsLocal);

        foreach (VisualElement childSubElement in textureCutoffs.Children())
        {
            (childSubElement.ElementAt(3) as SliderInt).RegisterValueChangedCallback((evt)=>
            {
                (childSubElement.ElementAt(4) as Label).text = "Value: " + (childSubElement.ElementAt(3) as SliderInt).value;
                splatHeightsLocal[textureCutoffs.IndexOf(childSubElement)].startingHeight = (childSubElement.ElementAt(3) as SliderInt).value;
                textureManipulator.SetSplatHeights(splatHeightsLocal);
            });


            (childSubElement.ElementAt(1) as DropdownField).RegisterValueChangedCallback((evt)=>
            {
                splatHeightsLocal[textureCutoffs.IndexOf(childSubElement)].textureIndex = (childSubElement.ElementAt(1) as DropdownField).index;
                textureManipulator.SetSplatHeights(splatHeightsLocal);
            });
            
        }

    }

    private Foldout createTextureDataObject(int number, int total)
    {
        Foldout tmp = new Foldout();
        tmp.text = "Layer"+ number;
        tmp.Add(new Label("Texture"));
        List<string> choicesList = textureManipulator.getTextureNames();
        tmp.Add(new DropdownField(choicesList,choicesList[0]));
        (tmp.ElementAt(1) as DropdownField).index = number;
        tmp.Add(new Label("Height"));
        tmp.Add(new SliderInt(0,100,SliderDirection.Horizontal,1));
        // SliderInt tmpSlider = (tmp.ElementAt(3) as SliderInt);
        (tmp.ElementAt(3) as SliderInt).value = number*(90/(total-1));
        tmp.Add(new Label("Value: "+ (tmp.ElementAt(3) as SliderInt).value));
        tmp.value = false;
        splatHeightsLocal[number].startingHeight = (tmp.ElementAt(3) as SliderInt).value;
        splatHeightsLocal[number].textureIndex = (tmp.ElementAt(1) as DropdownField).index;
        return tmp;
    }

    private void handleBrushEffectPicker()
    {
        DropdownField brushEffectPicker = root.Q<DropdownField>("BrushEffectPicker");
        brushEffectPicker.choices = new List<string> {"SmoothManipultionTool", "HardManipultionTool", "JaggedManipultionTool", "NoiseManipultionTool"}; 
        brushEffectPicker.SetValueWithoutNotify("SmoothManipultionTool");
        terrain.brushEffect = brushEffectPicker.value;
        brushEffectPicker.RegisterValueChangedCallback((evt) => {
             terrain.brushEffect = brushEffectPicker.value;
        });
    }

    private void handleSaveAndLoad()
    {
        Button saveMapButton = root.Q<Button>("MapSaveButton");
        Button loadMapButton = root.Q<Button>("MapLoadButton");
        saveMapButton.clicked += () => terrain.GetComponent<IOHandler>().SaveTerrainHeightmapToFolder(terrain.mapNameForLoadSave, ref terrain.heightmapSaveLoadBuffer, ref terrainManipulator.getTerrainMeshRef(), ref terrain.terrain);  
        loadMapButton.clicked += () => terrain.GetComponent<IOHandler>().LoadTerrainfromFolder(terrain.mapNameForLoadSave, ref terrain.heightmapSaveLoadBuffer, ref terrainManipulator.getTerrainMeshRef(), ref terrain.terrain);
    }

    public void handleTextures()
    {
        
        Button autoTextureButton = root.Q<Button>("AutoTexture");
        autoTextureButton.clicked += () => textureManipulator.AutoTextureTerrain(terrain.terrainData);
    }


    private void Update()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        TextField mapName = root.Q<TextField>("MapName");
        terrain.mapNameForLoadSave = mapName.value;
    }
}
