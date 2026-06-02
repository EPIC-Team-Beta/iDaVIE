/*
 * iDaVIE (immersive Data Visualisation Interactive Explorer)
 * Copyright (C) 2024 IDIA, INAF-OACT
 *
 * This file is part of the iDaVIE project.
 *
 * iDaVIE is free software: you can redistribute it and/or modify it under the terms
 * of the GNU Lesser General Public License (LGPL) as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version.
 *
 * iDaVIE is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 * without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR
 * PURPOSE. See the GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License along with
 * iDaVIE in the LICENSE file. If not, see <https://www.gnu.org/licenses/>.
 *
 * Additional information and disclaimers regarding liability and third-party
 * components can be found in the DISCLAIMER and NOTICE files included with this project.
 *
 */
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VolumeData;

public class DesktopPaintController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{

    List<Vector2> selectionPolyList = new List<Vector2>{};  //List for the polygon selection
    public GameObject markerPrefab;  //marker prefab to show where the user clicks
    public GameObject volumeDatasetManager;   //for assigning the cameras (might have to do this in other class)
    private VolumeDataSetRenderer dataRenderer;
    private VolumeDataSet dataSet;  //the dataset that has been loaded fromm the file
    private VolumeDataSet maskSet;  //mask
    private Texture3D regionCube;  //texture3D cube of texture rfloat (used to set greyscale)
    private Texture2D currentRegionSlice;  //to find the coordinates of the selection
    private float[,] currentMaskSlice;
    private Texture3D maskCube;  //texture3d cube of texture r16 (value of mask i think)
    //Actual VolumeDataSet mask cube - for writing mask
    public Dictionary<int, DataAnalysis.SourceStats> SourceStatsDict { get; private set; }

    public GameObject sliceCameraPrefab;
    private GameObject sliceCamera;
    public GameObject iDaVIELogo;
    public GameObject selectionContainer;
    public GameObject waitingContainer;
    private CameraTransform cameraX = new CameraTransform();
    private CameraTransform cameraY = new CameraTransform();
    private CameraTransform cameraZ = new CameraTransform();

    public Text sliceText;  //the text displaying the current slice
    private RawImage rawImage;
    private int prevIndex = 0;
    private CanvassDesktop canvassDesktop;  //could be changed to public
    public GameObject colorMapDropdown;
    public GameObject sliceSlider;
    public GameObject axisDropdown;
    public GameObject additiveToggle;
    public GameObject subtractiveToggle;
    private Image selectionModeImage;
    private Image selectionModeImage2;

    // Cached components — populated once in StartPaintSelection() to avoid per-frame GetComponent calls.
    private Slider _sliceSliderComp;
    private TMP_Dropdown _colorMapDropdownComp;
    private TMP_Dropdown _axisDropdownComp;
    private Toggle _additiveToggleComp;
    private Toggle _subtractiveToggleComp;

    private int axis;  //x = 0, y = 1, z = 0
    private int sliceIndex;  //of the region cube
    private float maxVal;  //max and min value of region cube
    private float minVal;
    private short sourceID = 1000;
    private short maxID = 1000;
    private List<Vector3Int> maskVoxels = new List<Vector3Int>{};
    private List<Vector3Int> lastMaskVoxels = new List<Vector3Int>{};
    private bool subtracted = false;
    private int maskCount = 0;  //no point in removing mask if there are no mask voxels
    private bool additive;
    private bool painted = false;
    private bool firstEnable = true;

    //of the region cube. For ensuring slices are bound
    private int cubeWidth;
    private int cubeHeight;
    private int cubeDepth;

    private bool isDrawing = false;

    public Button clearAllButton;
    public Button resetButton;  //Reset temp selection button
    public Button selectionButton;  //make temp selection button
    public TextMeshProUGUI selectionButtonText;
    public GameObject saveMessage;
    public TMP_Dropdown sourceIDDropdown;

    public GameObject sliceIndicatorPrefab;
    private GameObject sliceIndicator;

    //Color Map
    private ColorMapEnum colorMapEnum;
    public Texture2D colormap;
    public int colormapWidth = 1080;
    public int colormapHeight = 800;
    public int colorMapRowHeight = 10;

    //Zooming
    public RectTransform imageRect;
    public float zoomSpeed = 0.1f;
    public float minZoom = 0.5f;
    public float maxZoom = 3f;

    private Rect originalUVRect;
    private float currentZoom = 1f;

    struct CameraTransform
    {
        public Vector3 position;
        public Quaternion rotation;
    }


    public void StartPaintSelection()
    {
        if(canvassDesktop == null)
        {
            canvassDesktop = FindObjectOfType<CanvassDesktop>();
        }

        rawImage = GetComponent<RawImage>();

        if (imageRect == null)
            imageRect = GetComponent<RectTransform>();

        // Cache UI components once to avoid repeated GetComponent calls.
        _sliceSliderComp = sliceSlider.GetComponent<Slider>();
        _colorMapDropdownComp = colorMapDropdown.GetComponent<TMP_Dropdown>();
        _axisDropdownComp = axisDropdown.GetComponent<TMP_Dropdown>();
        _additiveToggleComp = additiveToggle.GetComponent<Toggle>();
        _subtractiveToggleComp = subtractiveToggle.GetComponent<Toggle>();

        originalUVRect = rawImage.uvRect;

        dataRenderer = canvassDesktop.GetFirstActiveRenderer();

        dataSet = dataRenderer.Data;
        regionCube = dataSet.RegionCube;

        var effectiveMin = dataRenderer.ScaleMin + dataRenderer.ThresholdMin
                * (dataRenderer.ScaleMax - dataRenderer.ScaleMin);
        var effectiveMax = dataRenderer.ScaleMin + dataRenderer.ThresholdMax
                * (dataRenderer.ScaleMax - dataRenderer.ScaleMin);

        maxVal = effectiveMax;
        minVal = effectiveMin;

        maskSet = dataRenderer.Mask;
        maskCube = maskSet.RegionCube;

        cubeWidth = regionCube.width;
        cubeHeight = regionCube.height;
        cubeDepth = regionCube.depth;

        axis = 2;
        sliceIndex = 0;
        additive = true;
        selectionModeImage = additiveToggle.transform.GetChild(0).gameObject.GetComponent<Image>();
        selectionModeImage.color = Color.green;
        selectionModeImage2 = subtractiveToggle.transform.GetChild(0).gameObject.GetComponent<Image>();
        selectionModeImage2.color = Color.gray;

        colorMapEnum = dataRenderer.ColorMap;

        SpawnCameras();
        SetColorMap();
        SetSliceSlider();
        SpawnSliceIndicator();
        ResetSlice();  //Call texture straight away
        iDaVIELogo.SetActive(false);
        sourceIDDropdown.onValueChanged.AddListener(OnDropDownFieldValueChanged);

        var sourceArray = DataAnalysis.GetMaskedSourceArray(maskSet.FitsData, maskSet.XDim, maskSet.YDim, maskSet.ZDim);
        if(sourceArray.Count > 0) {
            sourceIDDropdown.options.Clear();
            foreach (var source in sourceArray) {
                sourceIDDropdown.options.Add(new TMP_Dropdown.OptionData(""+source.maskVal));
                if(source.maskVal > maxID) maxID = source.maskVal;
            }
            sourceIDDropdown.value = 0;
            sourceID = short.Parse(sourceIDDropdown.options[0].text);
            sourceIDDropdown.RefreshShownValue();
        }
    }

    void Update()
    {
        sliceIndex = (int)_sliceSliderComp.value;
        if(maskCount > 0) clearAllButton.interactable = true;
        else clearAllButton.interactable = false;

        if(dataRenderer.IsFullResolution) {
            selectionContainer.SetActive(true);
            waitingContainer.SetActive(false);
        }
        else {
            selectionContainer.SetActive(false);
            waitingContainer.SetActive(true);
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            NextSlice();
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            PreviousSlice();
        }

        if(Input.GetKeyDown(KeyCode.Space))
        {
            if(IsPolygonClosed(selectionPolyList))
            {
                if(additive) ApplyMask(true);
                else SubtractiveSelection(true);
            }
            CompletePolygon();
        }

        if(Input.GetKeyDown(KeyCode.C))
        {
            UndoButton();
        }

        if(Input.GetKeyDown(KeyCode.P))
        {
            GetPrevMask();
        }

        if(Input.GetKeyDown(KeyCode.R))
        {
            ResetSelectionButton();
        }

        if(Input.GetKeyDown(KeyCode.X))
        {
            ChangeAxis(0);
            _axisDropdownComp.value = 0;
        }

        if(Input.GetKeyDown(KeyCode.Y))
        {
            ChangeAxis(1);
            _axisDropdownComp.value = 1;
        }

        if(Input.GetKeyDown(KeyCode.Z))
        {
            ChangeAxis(2);
            _axisDropdownComp.value = 2;
        }

        //Used to manage zoom functionality
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            if (!IsMouseOverImage()) return;

            scroll = -scroll;
            Vector2 mousePosition = Input.mousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rawImage.rectTransform, mousePosition, null, out Vector2 localMousePos);

            // Normalize the mouse position to UV space (0 to 1 range)
            float uvMouseX = Mathf.InverseLerp(-rawImage.rectTransform.rect.width / 2, rawImage.rectTransform.rect.width / 2, localMousePos.x);
            float uvMouseY = Mathf.InverseLerp(-rawImage.rectTransform.rect.height / 2, rawImage.rectTransform.rect.height / 2, localMousePos.y);

            // Update zoom level while preventing zooming out beyond the original size
            float newZoom = Mathf.Clamp(currentZoom - scroll * zoomSpeed, 1f, maxZoom);
            float zoomFactor = newZoom / currentZoom;
            currentZoom = newZoom;

            // Adjust the UV Rect to zoom in at the mouse position
            float newWidth = rawImage.uvRect.width / zoomFactor;
            float newHeight = rawImage.uvRect.height / zoomFactor;

            // Prevent zooming out beyond the original image bounds
            if (newWidth > 1f) newWidth = 1f;
            if (newHeight > 1f) newHeight = 1f;

            float newX = rawImage.uvRect.x + (rawImage.uvRect.width - newWidth) * uvMouseX;
            float newY = rawImage.uvRect.y + (rawImage.uvRect.height - newHeight) * uvMouseY;

            // Ensure the UV rect stays within (0,0,1,1)
            newX = Mathf.Clamp(newX, 0f, 1f - newWidth);
            newY = Mathf.Clamp(newY, 0f, 1f - newHeight);

            rawImage.uvRect = new Rect(newX, newY, newWidth, newHeight);
        }

        if(ColorMapUtils.FromHashCode(_colorMapDropdownComp.value) != colorMapEnum) {
            _colorMapDropdownComp.value = (int)colorMapEnum;
        }

    }

    public void UpdateMinValue(float value) {
        minVal = value;
    }

    public void UpdateMaxValue(float value) {
        maxVal = value;
    }

    private void OnDropDownFieldValueChanged(int index)
    {
        sourceID = short.Parse(sourceIDDropdown.options[index].text);
        HighlightMask();
    }

    //Add new source ID to dropdown
    public void AddSource() {
        maxID++;
        sourceID = maxID;
        sourceIDDropdown.options.Add(new TMP_Dropdown.OptionData(""+maxID));
        sourceIDDropdown.value = sourceIDDropdown.options.Count - 1;
        sourceIDDropdown.RefreshShownValue();
        HighlightMask();
    }

    private bool IsMouseOverImage()
    {
        RectTransform rectTransform = rawImage.rectTransform;
        Vector2 localMousePos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, Input.mousePosition, null, out localMousePos);

        return rectTransform.rect.Contains(localMousePos);
    }

    void OnDisable()
    {
        Destroy(sliceIndicator);
        Destroy(sliceCamera);
        _sliceSliderComp?.SetValueWithoutNotify(0);
    }


//Texture updates

    //For initially creating the texture - called when tab is clicked
    public void StartRegionCubeTexture()
    {
        UpdateTexture();

    }

    //Updates the displayed texture with the current settings
    public void UpdateTexture()
    {
        if (regionCube is null)
        {
            Debug.LogWarning("UpdateTexture called but regionCube is null");
            rawImage.texture = null;
            sliceText.text = "";
            currentRegionSlice = null;
            currentMaskSlice = null;
            return;
        }

        currentRegionSlice = GetSlice(regionCube, axis, sliceIndex);
        currentMaskSlice = GetFloatSlice(maskCube, axis, sliceIndex);
        rawImage.texture = currentRegionSlice;
        if (currentRegionSlice is null)
        {
            HighlightMask();
            sliceText.text = "" + (sliceIndex + 1); //+1 so it does not start on 0
        }
        else
        {
            sliceText.text = "";
        }
    }

    //return the slice from a texture3d based on the selected axis and index
    public Texture2D GetSlice(Texture3D texture3D, int axis, int sliceIndex)
    {
        return PaintSliceTextureHelper.GetSlice(texture3D, axis, sliceIndex,
            minVal, maxVal, colorMapEnum, colormap, colormapWidth, colorMapRowHeight);
    }

    //Get pixel values of a slice
    public float[,] GetFloatSlice(Texture3D texture3D, int axis, int sliceIndex)
    {
        return PaintSliceTextureHelper.GetFloatSlice(texture3D, axis, sliceIndex);
    }

    public void HighlightMask()
    {
        int arrayX = 0;
        int arrayY = 0;

        if(axis == 0)
        {
            arrayX = cubeHeight;
            arrayY = cubeDepth;
        }

        if(axis == 1)
        {
            arrayX = cubeWidth;
            arrayY = cubeDepth;
        }

        if(axis == 2)
        {
            arrayX = cubeWidth;
            arrayY = cubeHeight;
        }

        maskCount = 0;
        for(int i = 0; i < arrayX; i++)
        {
            for(int j = 0; j < arrayY; j++)
            {
                if(currentMaskSlice[i,j] > 0)
                {
                    maskCount++;
                }
            }
        }

        if(maskCount > 0)
        {
            Texture2D overlayTexture = MaskOverlayBuilder.CreateOverlayTexture(
                currentRegionSlice, currentMaskSlice,
                currentRegionSlice.width, currentRegionSlice.height,
                maskSet, axis, sliceIndex, sourceID);
            currentRegionSlice = overlayTexture;
            rawImage.texture = currentRegionSlice;
        }

    }

    public void GetPrevMask()
    {
        //have a prev slice variable (set to 0)
        float[,] prevMask = GetFloatSlice(maskCube, axis, prevIndex);
        for(int x = 0; x < currentRegionSlice.width; x++)
        {
            for(int y = 0; y < currentRegionSlice.height; y++)
            {
                if(prevMask[x,y] > 0)
                {
                    if(axis == 0) //x axis
                    {
                        Vector3Int pixel = new Vector3Int(sliceIndex, x, y); //Down the x axis - the actual x = slice, actual y = x, actual z = y
                        maskSet.PaintMaskVoxel(pixel, maskSet.GetMaskValue2(prevIndex,x,y));  //set to 0 to remove mask
                    }

                    if(axis == 1)
                    {
                        Vector3Int pixel = new Vector3Int(x, sliceIndex, y);
                        maskSet.PaintMaskVoxel(pixel, maskSet.GetMaskValue2(x,prevIndex,y));
                    }

                    if(axis == 2)
                    {
                        Vector3Int pixel = new Vector3Int(x, y, sliceIndex);
                        maskSet.PaintMaskVoxel(pixel, maskSet.GetMaskValue2(x,y,prevIndex));
                    }
                }

            }
        }
        maskSet.ConsolidateMaskEntries();
        ResetSlice();
    }

    //Updates source colours in scene to match current source ID
    public void UpdateSourceColours() {
        float[,] slice = GetFloatSlice(maskCube, axis, sliceIndex);
        Color maskColor = new Color(0.8018868f, 0.5030705f, 0.5030705f);
        Color currentSourceColor = Color.yellow;
        for(int x = 0; x < currentRegionSlice.width; x++)
        {
            for(int y = 0; y < currentRegionSlice.height; y++)
            {
                if(slice[x,y] > 0)
                {
                    if(axis == 0) //x axis
                    {
                        if(maskSet.GetMaskValue2(sliceIndex,x,y) == sourceID) {
                                currentRegionSlice.SetPixel(x,y,currentSourceColor);
                        }
                        else currentRegionSlice.SetPixel(x,y,maskColor);
                    }

                    if(axis == 1)
                    {
                        if(maskSet.GetMaskValue2(x,sliceIndex,y) == sourceID) {
                            currentRegionSlice.SetPixel(x,y,currentSourceColor);
                        }
                        else currentRegionSlice.SetPixel(x,y,maskColor);
                    }

                    if(axis == 2)
                    {
                        if(maskSet.GetMaskValue2(x,y,sliceIndex) == sourceID){
                            currentRegionSlice.SetPixel(x,y,currentSourceColor);
                        }
                        else currentRegionSlice.SetPixel(x,y,maskColor);
                    }
                }

            }
        }

    }

    public Color GetColorFromColormap(int rowIndex, float value)
    {
        return PaintSliceTextureHelper.GetColorFromColormap(colormap, rowIndex, value, colormapWidth, colorMapRowHeight);
    }

    public void SpawnCameras()
    {
        if(sliceCamera != null)
        {
            return;
        }
        axis = 2; //remove after testing
        SetCameraTransforms();

        Transform parentTransform = volumeDatasetManager.transform;
        Transform renderedCube = parentTransform.GetChild(0);  //assigns the parent of the object to the datacube

        sliceCamera = Instantiate(sliceCameraPrefab, renderedCube);
        ResetCamera();
    }

    public void SetCameraTransforms()
    {
        cameraZ.position = new Vector3(1.3f, 0, 0);
        cameraZ.rotation = Quaternion.Euler(0, -90f, 0);

        cameraX.position = new Vector3(0, 1.3f, 0);
        cameraX.rotation = Quaternion.Euler(90, 0, 0);

        cameraY.position = new Vector3(-1.3f, 0, 0);
        cameraY.rotation = Quaternion.Euler(0, 90f, 90f);
    }

    public void SpawnSliceIndicator()
    {
        Transform parentTransform = volumeDatasetManager.transform;
        Transform renderedCube = parentTransform.GetChild(0);  //assigns the parent of the object to the datacube

        sliceIndicator = Instantiate(sliceIndicatorPrefab, renderedCube);
        ResetSliceIndicator();
    }

//User manipulation and feedback

    public void OnPointerDown(PointerEventData eventData)
    {
        resetButton.interactable = true;
        selectionButton.interactable = true;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (selectionPolyList.Count > 0)
            {
                UndoPoint();
            }
            return;
        }

        isDrawing = true;
        AddPoint(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDrawing = false;
    }

    //Adds points for polygon as user drags
    public void OnDrag(PointerEventData eventData)
    {
        if (isDrawing)
        {
            AddPoint(eventData);
            subtracted = false;
        }
    }

    //Gets the local cursor value and the local pixel value of point
    private void AddPoint(PointerEventData eventData)
    {
        // Transformation from cursor position to the correct pixel in the texture
        Vector2 localCursor;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rawImage.rectTransform, eventData.position, eventData.pressEventCamera, out localCursor))
            return;

        Rect rect = rawImage.rectTransform.rect;

        // Normalize localCursor to (0,1) range within the visible rect
        float normalizedX = (localCursor.x - rect.x) / rect.width;
        float normalizedY = (localCursor.y - rect.y) / rect.height;

        // Get the uvRect to adjust for zoom/panning
        Rect uvRect = rawImage.uvRect;

        // Map normalized coordinates to UV coordinates
        float uvX = uvRect.x + normalizedX * uvRect.width;
        float uvY = uvRect.y + normalizedY * uvRect.height;

        // Ensure we're within valid UV range
        if (uvX >= 0 && uvX <= 1 && uvY >= 0 && uvY <= 1)
        {
            int textureX = Mathf.FloorToInt(uvX * currentRegionSlice.width);
            int textureY = Mathf.FloorToInt(uvY * currentRegionSlice.height);

            // Ensure pixel is inside bounds
            textureX = Mathf.Clamp(textureX, 0, currentRegionSlice.width - 1);
            textureY = Mathf.Clamp(textureY, 0, currentRegionSlice.height - 1);

            Vector2 texturePoint = new Vector2(textureX, textureY);
            AddPointToList(localCursor, texturePoint);
        }
    }

    //Adds the marker to the image and the pixel location to the polygon list
    public void AddPointToList(Vector2 localPosition, Vector2 localPixel)
    {
        selectionPolyList.Add(localPixel);
        CheckForPolygonCompletion();
        GameObject circleInstance = Instantiate(markerPrefab, transform);
        circleInstance.transform.localPosition = localPosition;
    }

    //Undo last action
    public void UndoButton()
    {
        if(selectionPolyList.Count > 0) {
            ResetSlice();
            return;
        }

        if(subtracted) {
            maskVoxels = lastMaskVoxels;
            ApplyMask(false);
            painted = false;
            subtracted = false;
            return;
        }

        if(maskCount < 1)
        {
            return;
        }

        if(painted)
        {
            UpdateMaskVoxels(true);
            SubtractiveSelection(false);
            maskVoxels = lastMaskVoxels;
            ApplyMask(false);
            painted = false;
        }

        maskSet.ConsolidateMaskEntries();

        ResetSlice();
    }

    //Used to clear all sources matchinf the source ID in the scene
    public void ClearAllButton() {
        if(maskCount < 1)
        {
            return;
        }

        UpdateLastMaskVoxels();
        float[,] slice = GetFloatSlice(maskCube, axis, sliceIndex);
        for(int x = 0; x < currentRegionSlice.width; x++)
        {
            for(int y = 0; y < currentRegionSlice.height; y++)
            {
                if(slice[x,y] > 0) {

                    if(axis == 0) //x axis
                    {
                        Vector3Int pixel = new Vector3Int(sliceIndex, x, y); //Down the x axis - the actual x = slice, actual y = x, actual z = y
                        if(maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) == sourceID)  maskSet.PaintMaskVoxel(pixel, 0);
                    }

                    if(axis == 1)
                    {
                        Vector3Int pixel = new Vector3Int(x, sliceIndex, y);
                        if(maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) == sourceID)  maskSet.PaintMaskVoxel(pixel, 0);
                    }

                    if(axis == 2)
                    {
                        Vector3Int pixel = new Vector3Int(x, y, sliceIndex);
                        if(maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) == sourceID)  maskSet.PaintMaskVoxel(pixel, 0);
                    }
                    maskCount--;
                }
            }
        }
        maskSet.ConsolidateMaskEntries();
        ResetSlice();

    }

    //Make a subtractive selection on the slice, param update is used if the last mask voxels arr needs to be updated
    private void SubtractiveSelection(bool update) {
        if(update) UpdateLastMaskVoxels();
        Debug.Log("Mask Voxel Count (in clear mask button): " + maskVoxels.Count);
        if(maskVoxels.Count > 0)
        {
            for(int i = 0; i < maskVoxels.Count; i++)
            {
                maskSet.PaintMaskVoxel(maskVoxels[i], 0);
            }

            maskSet.ConsolidateMaskEntries();
            subtracted = true;
            ResetSlice();
            return;
        }
    }

    //clear the modifications made to the texture (not to the slice)
    public void ResetSelectionButton()
    {
        ResetSlice();
    }

    //Clear all markers
    public void RemoveMarkers()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    //Apply the selection as a mask
    public void ApplyMask(bool update)
    {
        if(update) UpdateLastMaskVoxels();
        //must make sure polygon is complete (list is populated)
        if(maskVoxels.Count == 0)
        {
            return;
        }

        Debug.Log("Mask voxels: " + maskVoxels.Count);
        for(int i = 0; i < maskVoxels.Count; i++)
        {
            Int16 maskVal = maskSet.GetMaskValue2(maskVoxels[i].x,maskVoxels[i].y,maskVoxels[i].z);
            if(maskVal == 0) maskSet.PaintMaskVoxel(maskVoxels[i], sourceID);
        }
        maskSet.ConsolidateMaskEntries();
        UpdateSourceColours();
        ResetSlice();
        painted = true;
    }

    //Removes the last point that was added (will need modifications if other children are added to the display)
    public void UndoPoint()
    {
        selectionPolyList.RemoveAt(selectionPolyList.Count - 1);
        Destroy(transform.GetChild(transform.childCount - 1).gameObject);
    }

    //Handles the resets when a new slice is selected (markers)
    public void ResetSlice()
    {
        maskVoxels = new List<Vector3Int>();
        UpdateTexture();  //go get the original slice without temp modifications (shading showing where masking would happen)
        RemoveMarkers();
        ClearSelectionPoly();
        selectionButton.interactable = false;
        selectionButtonText.text = "Fill \n(Space Bar)";
    }

//Polygon creation and masking methods

    //As name suggests and if completed the calls FillPolygon to show where mask will be applied
    public void CheckForPolygonCompletion()
    {
        if (selectionPolyList.Count >= 3 && IsPolygonClosed(selectionPolyList))
        {
            RemoveMarkers();
            FillPolygon();
        }
    }

    //Shows where mask will be applied and stores those position in the mask list (by calling inside out method)
    public void FillPolygon()
    {
        if(selectionPolyList.Count > 10)  //stop the drawing if a polygone has been made (fill polygone is called too early due to first points being so close together)
        {
            isDrawing = false;
        }

        if (currentRegionSlice == null || selectionPolyList == null || selectionPolyList.Count < 3)
        {
            Debug.LogError("Texture or polygon points not properly assigned.");
            return;
        }

        Color fillColor = new Color(0.6941177f, 0.7113449f, 0.8392157f, 0.75f);  //0.5f alpha for future layer blending
        if(additive) fillColor = Color.green;
        else fillColor = Color.red;

        // Calculate the bounding box of the polygon
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var point in selectionPolyList)
        {
            if (point.x < minX) minX = point.x;
            if (point.y < minY) minY = point.y;
            if (point.x > maxX) maxX = point.x;
            if (point.y > maxY) maxY = point.y;
        }

        // Ensure the bounding box is within the texture bounds
        minX = Mathf.Clamp(minX, 0, currentRegionSlice.width - 1);
        minY = Mathf.Clamp(minY, 0, currentRegionSlice.height - 1);
        maxX = Mathf.Clamp(maxX, 0, currentRegionSlice.width - 1);
        maxY = Mathf.Clamp(maxY, 0, currentRegionSlice.height - 1);

        int pixelsChanged = 0;
        bool incorrectSourceCrossed = false;
        Color[] originalPixels = currentRegionSlice.GetPixels();

        for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
        {
            for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
            {
                if (IsPointInPolygon(new Vector2(x, y), selectionPolyList)) {
                    if(axis == 0) //x axis
                    {
                        Vector3Int pixel = new Vector3Int(sliceIndex, x, y); //Down the x axis - the actual x = slice, actual y = x, actual z = y
                        if(maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) != sourceID && maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) != 0) {
                            incorrectSourceCrossed = true;
                            continue;
                        }
                        maskVoxels.Add(pixel);
                    }

                    if(axis == 1)
                    {
                        Vector3Int pixel = new Vector3Int(x, sliceIndex, y);
                        if(maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) != sourceID && maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) != 0) {
                            incorrectSourceCrossed = true;
                            continue;
                        }
                        maskVoxels.Add(pixel);
                    }

                    if(axis == 2)
                    {
                        Vector3Int pixel = new Vector3Int(x, y, sliceIndex);
                        if(maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) != sourceID && maskSet.GetMaskValue2(pixel.x,pixel.y,pixel.z) != 0) {
                            incorrectSourceCrossed = true;
                            continue;
                        }
                        maskVoxels.Add(pixel);
                    }
                }
                if(selectionPolyList.Contains(new Vector2(x, y)))
                {

                    currentRegionSlice.SetPixel(x, y, fillColor); //future improvement - make the colour layer separate and combine it witht his layer (so temp mask can be semi transparent)
                    pixelsChanged++;
                }
            }
        }
        if(incorrectSourceCrossed) {
            selectionPolyList = new List<Vector2>();
            maskVoxels.Clear();
            StartCoroutine(ShowMessage("\tCannot paint over mask of different source. Please change source ID", 4.0f));
            currentRegionSlice.SetPixels(originalPixels);
            currentRegionSlice.Apply();
            return;
        }
        currentRegionSlice.Apply();
        selectionButtonText.text = "Apply Mask \n(Space Bar)";
    }

    //Inside out method to see if point is in the polygon
    public bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        return PolygonGeometry.IsPointInPolygon(point, polygon);
    }

    //adds a final point to the polygon equal to the first point and calls the check (so the correct logic can occur)
    public void CompletePolygon()
    {
        if(selectionPolyList.Count >= 3)
        {
            selectionPolyList.Add(selectionPolyList[0]);
        }
        CheckForPolygonCompletion();
    }

    //if the first and last point are within 5 pixels then close the polygon
    public bool IsPolygonClosed(List<Vector2> points)
    {
        return PolygonGeometry.IsPolygonClosed(points);
    }

    /// <summary>
    /// Updates the <c>maskVoxels</c> list with the coordinates of all voxels in the current mask slice
    /// that are nonzero (i.e., have a mask applied).
    /// If <paramref name="matchID"/> is true, only voxels whose mask value matches the current <c>sourceID</c> are included.
    /// The voxel coordinates are calculated based on the current axis and slice index.
    /// </summary>
    /// <param name="matchID">
    /// If true, only voxels with a mask value equal to <c>sourceID</c> are added to <c>maskVoxels</c>.
    /// If false, all nonzero voxels are added.
    /// </param>
    ///
    public void UpdateMaskVoxels(bool matchID)
    {
        maskVoxels.Clear();
        float[,] slice = GetFloatSlice(maskCube, axis, sliceIndex);
        for (int x = 0; x < currentRegionSlice.width; x++)
        {
            for (int y = 0; y < currentRegionSlice.height; y++)
            {
                if (slice[x, y] > 0)
                {
                    if (axis == 0) //x axis
                    {
                        Vector3Int pixel = new Vector3Int(sliceIndex, x, y);
                        if (matchID)
                        {
                            if (maskSet.GetMaskValue2(pixel.x, pixel.y, pixel.z) == sourceID) maskVoxels.Add(pixel);
                        }
                        else maskVoxels.Add(pixel);
                    }

                    if (axis == 1)
                    {
                        Vector3Int pixel = new Vector3Int(x, sliceIndex, y);
                        if (matchID)
                        {
                            if (maskSet.GetMaskValue2(pixel.x, pixel.y, pixel.z) == sourceID) maskVoxels.Add(pixel);
                        }
                        else maskVoxels.Add(pixel);
                    }

                    if (axis == 2)
                    {
                        Vector3Int pixel = new Vector3Int(x, y, sliceIndex);
                        if (matchID)
                        {
                            if (maskSet.GetMaskValue2(pixel.x, pixel.y, pixel.z) == sourceID) maskVoxels.Add(pixel);
                        }
                        else maskVoxels.Add(pixel);
                    }
                }

            }
        }
    }

    /// <summary>
    /// Updates the <c>lastMaskVoxels</c> list with the coordinates of all voxels in the current mask slice
    /// that are nonzero (i.e., have a mask applied), regardless of their mask value.
    /// The voxel coordinates are calculated based on the current axis and slice index.
    /// This function is typically used to store the state of the mask before a new operation,
    /// allowing for undo functionality or restoration of the previous mask state.
    /// </summary>
    public void UpdateLastMaskVoxels()
    {
        lastMaskVoxels.Clear();
        float[,] slice = GetFloatSlice(maskCube, axis, sliceIndex);
        for (int x = 0; x < currentRegionSlice.width; x++)
        {
            for (int y = 0; y < currentRegionSlice.height; y++)
            {
                if (slice[x, y] > 0)
                {
                    if (axis == 0) //x axis
                    {
                        Vector3Int pixel = new Vector3Int(sliceIndex, x, y);
                        lastMaskVoxels.Add(pixel);
                    }

                    if (axis == 1)
                    {
                        Vector3Int pixel = new Vector3Int(x, sliceIndex, y);
                        lastMaskVoxels.Add(pixel);
                    }

                    if (axis == 2)
                    {
                        Vector3Int pixel = new Vector3Int(x, y, sliceIndex);
                        lastMaskVoxels.Add(pixel);
                    }
                }

            }
        }
    }

    //clear the polygon selection
    private void ClearSelectionPoly()
    {
        selectionPolyList.Clear();
    }


//User Settings and Navigation

    //select the previous slice or go to end
    public void PreviousSlice()
    {
        prevIndex = sliceIndex;
        if(sliceIndex == 0)
        {
            //Go to the final slice
            if(axis == 0)
            {
                sliceIndex = cubeWidth;  //-1 below
            }

            if(axis == 1)
            {
                sliceIndex = cubeHeight;
            }

            if(axis == 2)
            {
                sliceIndex = cubeDepth;
            }
        }

        sliceIndex -= 1;
        _sliceSliderComp.value = sliceIndex;
        UpdateSourceColours();
        UpdateMaskVoxels(false);
        UpdateLastMaskVoxels();
        ResetSlice();
        painted = false;
    }

    //select next slice or go to start
    public void NextSlice()
    {
        //if the slice is out of range reset it back to one
        prevIndex = sliceIndex;
        sliceIndex += 1;
        if (sliceIndex < 0 || (axis == 0 && sliceIndex >= cubeWidth) || (axis == 1 && sliceIndex >= cubeHeight) || (axis == 2 && sliceIndex >= cubeDepth))
        {
            sliceIndex = 0;
        }

        _sliceSliderComp.value = sliceIndex;
        UpdateSourceColours();
        UpdateMaskVoxels(false);
        UpdateLastMaskVoxels();
        ResetSlice();
        painted = false;
    }

    //change the axis (being looked down) and call the new slice
    public void ChangeAxis(int axisIndex)
    {
        axis = axisIndex;
        sliceIndex = 0;
        prevIndex = 0;
        SetSliceSlider();
        _sliceSliderComp.value = 0;
        ResetSlice();
        ResetCamera();
        ResetSliceIndicator();
    }

    //Change the color map
    public void ChangeColorMap()
    {
        colorMapEnum = ColorMapUtils.FromHashCode(_colorMapDropdownComp.value);
        dataRenderer.ColorMap = colorMapEnum;
        ResetSlice();
    }

    public void SetColorMap()
    {
        _colorMapDropdownComp.options.Clear();

        foreach (var colorMap in Enum.GetValues(typeof(ColorMapEnum)))
        {
            _colorMapDropdownComp.options.Add(new TMP_Dropdown.OptionData() { text = colorMap.ToString() });
        }

        _colorMapDropdownComp.value = Config.Instance.defaultColorMap.GetHashCode();
    }

    private void SetSliceSlider()
    {
        if(axis == 0)
            {
                _sliceSliderComp.maxValue = cubeWidth - 1;
            }

            if(axis == 1)
            {
                _sliceSliderComp.maxValue = cubeHeight - 1;
            }

            if(axis == 2)
            {
                _sliceSliderComp.maxValue = cubeDepth - 1;
            }
    }

    public void SliceSliderChanged()
    {
        sliceIndex = (int) _sliceSliderComp.value;
        ResetSlice();
        SliderIndicatorChange();
    }

    public void ResetCamera()
    {
        if(axis == 0)
        {
            sliceCamera.transform.localPosition = cameraX.position;
            sliceCamera.transform.localRotation = cameraX.rotation;
        }

        if(axis == 1)
        {
            sliceCamera.transform.localPosition = cameraY.position;
            sliceCamera.transform.localRotation = cameraY.rotation;
        }

        if(axis == 2)
        {
            sliceCamera.transform.localPosition = cameraZ.position;
            sliceCamera.transform.localRotation = cameraZ.rotation;
        }
    }

    public void ResetSliceIndicator()
    {
        if(axis == 0)
        {
            sliceIndicator.transform.localPosition = new Vector3(-0.5f, 0, 0);
            sliceIndicator.transform.localRotation = Quaternion.Euler(0, -90f, 0f);
        }

        if(axis == 1)
        {
            sliceIndicator.transform.localPosition = new Vector3(0, -0.5f, 0);
            sliceIndicator.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        }

        if(axis == 2)
        {
            sliceIndicator.transform.localPosition = new Vector3(0, 0, -0.5f);
            sliceIndicator.transform.localRotation = Quaternion.Euler(0, 0, 0);
        }
    }

    public void SliderIndicatorChange()
    {
        if(axis == 0)
        {
            float localizedValue = Mathf.Lerp(-0.5f, 0.5f, (float) sliceIndex / (cubeWidth - 1));
            sliceIndicator.transform.localPosition = new Vector3(localizedValue, 0, 0);
        }

        if(axis == 1)
        {
            float localizedValue = Mathf.Lerp(-0.5f, 0.5f, (float) sliceIndex / (cubeHeight - 1));
            sliceIndicator.transform.localPosition = new Vector3(0, localizedValue, 0);
        }

        if(axis == 2)
        {
            float localizedValue = Mathf.Lerp(-0.5f, 0.5f, (float) sliceIndex / (cubeDepth - 1));
            sliceIndicator.transform.localPosition = new Vector3(0, 0, localizedValue);
        }
    }

    public void SaveMask(bool overwrite)
    {
        PaintMenuController _paintMenuController = FindObjectOfType<PaintMenuController>();
        if(overwrite)
        {
            _paintMenuController.SaveOverwriteMask();
            StartCoroutine(ShowMessage("\tMask written to disk", 2.0f));
        }
        else
        {
            _paintMenuController.SaveNewMask();
            StartCoroutine(ShowMessage("\tNew Mask saved",2.0f));
        }
    }

    public IEnumerator ShowMessage(string message, float time) {
        saveMessage.GetComponent<TextMeshProUGUI>().text = message;
        saveMessage.SetActive(true);
        yield return new WaitForSeconds(time);
        saveMessage.SetActive(false);
    }

    public void OnToggleChanged() {
        if(additive) {
            selectionModeImage.color = Color.gray;
            selectionModeImage2.color = Color.red;
            additive = false;
            _additiveToggleComp.interactable = true;
            _subtractiveToggleComp.interactable = false;
        }
        else {
            selectionModeImage2.color = Color.gray;
            selectionModeImage.color = Color.green;
            additive = true;
            _additiveToggleComp.interactable = false;
            _subtractiveToggleComp.interactable = true;
        }
    }

    public void SelectionButton() {
        if(IsPolygonClosed(selectionPolyList))
            {
                if(additive) ApplyMask(true);
                else SubtractiveSelection(true);
            }
        CompletePolygon();
    }
}
