using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class uiFunctionality : MonoBehaviour
{
    public TerrainEditor terrain;
    // Start is called before the first frame update
    private VisualElement root;
    private void OnEnable() 
    {
        
        root = GetComponent<UIDocument>().rootVisualElement;
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
    }
    private void handleBrushStrength()
    {
        SliderInt brushStrengthSlider = root.Q<SliderInt>("BrushStrength");
        terrain.brushStrength = brushStrengthSlider.value;
        brushStrengthSlider.RegisterValueChangedCallback((evt) =>
        {
            terrain.brushStrength = brushStrengthSlider.value;
        });
    }

    public void handleBrushSize()
    {
        Slider brushSizeSlider = root.Q<Slider>("BrushSize");
        terrain.brushSize = brushSizeSlider.value;
        brushSizeSlider.RegisterValueChangedCallback((evt) =>
        {
            Debug.Log(brushSizeSlider.value);
            terrain.brushSize = brushSizeSlider.value;
            terrain.ScaleBrush();
            terrain.LoadBrushFromPngAndCalculateBrushPixels(false);
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
             terrain.LoadBrushFromPngAndCalculateBrushPixels(true,terrain.selectedBrush);
        });
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
        saveMapButton.clicked += () => terrain.SaveTerrainHeightmapToFolder(terrain.mapNameForLoadSave);  
        loadMapButton.clicked += () => terrain.LoadTerrainfromFolder(terrain.mapNameForLoadSave);
    }

    private void handleTextures()
    {
        Button autoTextureButton = root.Q<Button>("AutoTexture");
        autoTextureButton.clicked += () => terrain.AutoTextureTerrain();
    }


    private void Update()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        TextField mapName = root.Q<TextField>("MapName");
        terrain.mapNameForLoadSave = mapName.value;
    }
}
