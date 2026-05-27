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
using System.Globalization;
using System.IO;
using System.Text;
using DataFeatures;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VolumeData;

/// <summary>
/// Plain C# class (not MonoBehaviour). Coordinates file-browsing and cube-load logic.
/// Subset bound state is delegated to SubsetBoundsController. Coroutines are scheduled
/// on the host MonoBehaviour. Decoupled from CanvassDesktop via two delegates.
/// </summary>
public class FileLoadPanelController
{
    // --- Injected dependencies ---
    private readonly MonoBehaviour                    _coroutineHost;
    private readonly Func<VolumeDataSetRenderer>      _getActiveRenderer;
    private readonly Action                           _refreshRenderers;
    private readonly GameObject                       _informationPanelContent;
    private readonly GameObject                       _fileLoadCanvassDesktop;
    private readonly GameObject                       _mainCanvassDesktop;
    private readonly GameObject                       _loadingText;
    private readonly TextMeshProUGUI                  _loadTextLabel;
    private readonly GameObject                       _progressBar;
    private readonly GameObject                       _cubeprefab;
    private readonly GameObject                       _volumePlayer;
    private readonly GameObject                       _welcomeMenu;
    private readonly MenuBarBehaviour                 _menuBarBehaviour;
    private readonly QuickMenuController              _quickMenuController;
    private readonly List<TMP_InputField>             _inputFields;

    // --- File state ---
    private string _imagePath = "";
    private string _maskPath  = "";
    private int    _hduSelectionIndex = 0;

    // --- FITS axis metadata ---
    private double                     _imageNAxis = 0;
    private double                     _imageSize  = 1;
    private double                     _maskNAxis  = 0;
    private double                     _maskSize   = 1;
    private Dictionary<double, double> _axisSize     = null;
    private Dictionary<double, double> _maskAxisSize = null;

    // --- Rendering ratio ---
    private int _ratioDropdownIndex = 0;

    // --- Popup state ---
    private bool   _showPopUp  = false;
    private string _textPopUp  = "";

    // --- UI refs resolved in Initialize ---
    private Toggle        _subsetToggle;
    private TMP_Dropdown  _zAxisDropdown;

    // --- Input field tab-cycling ---
    private int _inputIndex;

    // --- Coroutine handles ---
    private Coroutine _loadCubeCoroutine;
    private Coroutine _showLoadDialogCoroutine;

    // --- Controllers resolved at load time ---
    private VolumeInputController   _volumeInputController;
    private VolumeCommandController _volumeCommandController;

    // --- Sub-controller ---
    private SubsetBoundsController _subsetBounds;

    /// <summary>
    /// Raised after a cube finishes loading and the VolumeDataSetRenderer is ready.
    /// CanvassDesktop subscribes to perform post-load UI wiring.
    /// </summary>
    public event Action<VolumeDataSetRenderer> VolumeLoaded;

    public FileLoadPanelController(
        MonoBehaviour                coroutineHost,
        Func<VolumeDataSetRenderer>  getActiveRenderer,
        Action                       refreshRenderers,
        GameObject                   informationPanelContent,
        GameObject                   fileLoadCanvassDesktop,
        GameObject                   mainCanvassDesktop,
        GameObject                   loadingText,
        TextMeshProUGUI              loadTextLabel,
        GameObject                   progressBar,
        GameObject                   cubeprefab,
        GameObject                   volumePlayer,
        GameObject                   welcomeMenu,
        List<TMP_InputField>         inputFields,
        MenuBarBehaviour             menuBarBehaviour,
        QuickMenuController          quickMenuController)
    {
        _coroutineHost           = coroutineHost;
        _getActiveRenderer       = getActiveRenderer;
        _refreshRenderers        = refreshRenderers;
        _informationPanelContent = informationPanelContent;
        _fileLoadCanvassDesktop  = fileLoadCanvassDesktop;
        _mainCanvassDesktop      = mainCanvassDesktop;
        _loadingText             = loadingText;
        _loadTextLabel           = loadTextLabel;
        _progressBar             = progressBar;
        _cubeprefab              = cubeprefab;
        _volumePlayer            = volumePlayer;
        _welcomeMenu             = welcomeMenu;
        _inputFields             = inputFields;
        _menuBarBehaviour        = menuBarBehaviour;
        _quickMenuController     = quickMenuController;
    }

    /// <summary>
    /// Must be called from CanvassDesktop.Start() after all GameObjects are wired.
    /// </summary>
    public void Initialize()
    {
        _volumeInputController   = UnityEngine.Object.FindObjectOfType<VolumeInputController>();
        _volumeCommandController = UnityEngine.Object.FindObjectOfType<VolumeCommandController>();

        _subsetToggle = _informationPanelContent.transform
            .Find("SubsetSelection_container/LoadSubset_Toggle").GetComponent<Toggle>();

        _zAxisDropdown = _informationPanelContent.transform
            .Find("Axes_container/Z_Dropdown").GetComponent<TMP_Dropdown>();
        _zAxisDropdown.onValueChanged.AddListener(updateSubsetZMax);

        var xMin = _informationPanelContent.transform
            .Find("SubsetMin_container/SubsetX_min").GetComponent<TMP_InputField>();
        var yMin = _informationPanelContent.transform
            .Find("SubsetMin_container/SubsetY_min").GetComponent<TMP_InputField>();
        var zMin = _informationPanelContent.transform
            .Find("SubsetMin_container/SubsetZ_min").GetComponent<TMP_InputField>();
        var xMax = _informationPanelContent.transform
            .Find("SubsetMax_container/SubsetX_max").GetComponent<TMP_InputField>();
        var yMax = _informationPanelContent.transform
            .Find("SubsetMax_container/SubsetY_max").GetComponent<TMP_InputField>();
        var zMax = _informationPanelContent.transform
            .Find("SubsetMax_container/SubsetZ_max").GetComponent<TMP_InputField>();

        _subsetBounds = new SubsetBoundsController(xMin, xMax, yMin, yMax, zMin, zMax);
        _inputIndex = 0;
    }

    // -----------------------------------------------------------------------
    // Update — called each frame from CanvassDesktop.Update()
    // -----------------------------------------------------------------------

    public void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && _inputFields.Count > 1)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                if (_inputIndex <= 0)
                    _inputIndex = _inputFields.Count;
                _inputIndex--;
                _inputFields[_inputIndex].Select();
            }
            else
            {
                if (_inputFields.Count <= _inputIndex + 1)
                    _inputIndex = -1;
                _inputIndex++;
                _inputFields[_inputIndex].Select();
            }
        }
    }

    // -----------------------------------------------------------------------
    // OnGUI — called from CanvassDesktop.OnGUI()
    // -----------------------------------------------------------------------

    public void DrawPopup()
    {
        if (_showPopUp)
        {
            GUI.backgroundColor = new Color(1, 0, 0, 1f);
            GUI.Window(0,
                new Rect((Screen.width / 2) - 150, (Screen.height / 2) - 75, 300, 250),
                ShowGUI,
                "Invalid Cube");
        }
    }

    private void ShowGUI(int windowID)
    {
        GUI.Label(new Rect(65, 40, 300, 250), _textPopUp);
        if (GUI.Button(new Rect(50, 150, 75, 30), "OK"))
        {
            _showPopUp = false;
            _textPopUp = "";
        }
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void SetInputIndex(int newIndex)
    {
        _inputIndex = newIndex;
    }

    // -----------------------------------------------------------------------
    // Image browsing
    // -----------------------------------------------------------------------

    public void BrowseImageFile()
    {
        _loadingText.SetActive(false);
        string lastPath = PlayerPrefs.GetString("LastPath");
        if (!Directory.Exists(lastPath))
            lastPath = "";
        var extensions = new[]
        {
            new ExtensionFilter("Fits Files", "fits", "fit"),
            new ExtensionFilter("All Files", "*"),
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", lastPath, extensions, false, paths =>
        {
            if (paths.Length == 1)
            {
                PlayerPrefs.SetString("LastPath", Path.GetDirectoryName(paths[0]));
                PlayerPrefs.Save();
                _browseImageFile(paths[0]);
            }
        });
    }

    private void _browseImageFile(string path)
    {
        if (path != null)
        {
            _imageSize = 1;
            _imagePath = path;
            _maskPath  = "";

            _informationPanelContent.transform
                .Find("MaskFile_container/Button").GetComponent<Button>().interactable = false;
            _informationPanelContent.transform
                .Find("MaskFile_container/MaskFilePath_text").GetComponent<TextMeshProUGUI>().text = "...";
            _informationPanelContent.transform
                .Find("Loading_container/Button").GetComponent<Button>().interactable = false;

            IntPtr fptr;
            int status = 0;
            if (FitsReader.FitsOpenFile(out fptr, _imagePath, out status, true) != 0)
                Debug.Log("Fits open failure... code #" + status);

            // Enumerate HDU names and populate dropdown
            FitsReader.FitsGetHduCount(fptr, out int hduNum, out status);
            var hduNames = new List<string>();
            var hduName  = new StringBuilder(80);
            for (int i = 0; i < hduNum; i++)
            {
                FitsReader.FitsMovabsHdu(fptr, i + 1, out _, out status);
                hduName.Clear();
                if (FitsReader.FitsReadKey(fptr, (int)FitsReader.DataType.TSTRING, "EXTNAME",
                        hduName, IntPtr.Zero, out status) != 0)
                {
                    status = 0;
                    if (FitsReader.FitsReadKey(fptr, (int)FitsReader.DataType.TSTRING, "HDUNAME",
                            hduName, IntPtr.Zero, out status) != 0)
                    {
                        hduName.Append("HDU " + (i + 1));
                        status = 0;
                    }
                }
                hduNames.Add(hduName.ToString());
            }

            _hduSelectionIndex = 0;
            FitsReader.FitsMovabsHdu(fptr, _hduSelectionIndex + 1, out _, out status);

            var hduContainer = _informationPanelContent.transform
                .Find("HeaderTitle_container/Hdu_container").gameObject;
            hduContainer.transform.Find("Hdu_dropdown").GetComponent<TMP_Dropdown>().ClearOptions();
            hduContainer.transform.Find("Hdu_dropdown").GetComponent<TMP_Dropdown>().value = 0;

            if (hduNames.Count > 1)
            {
                hduContainer.SetActive(true);
                var dd = hduContainer.transform.Find("Hdu_dropdown").GetComponent<TMP_Dropdown>();
                for (int i = 0; i < hduNames.Count; i++)
                    dd.options.Add(new TMP_Dropdown.OptionData { text = (i + 1) + ": " + hduNames[i] });
                dd.RefreshShownValue();
            }
            else
            {
                hduContainer.SetActive(false);
            }

            _informationPanelContent.transform
                .Find("ImageFile_container/ImageFilePath_text").GetComponent<TextMeshProUGUI>().text =
                Path.GetFileName(_imagePath);

            UpdateHeaderFromFits(fptr);
            FitsReader.FitsCloseFile(fptr, out status);

            if (IsLoadable())
            {
                _informationPanelContent.transform
                    .Find("MaskFile_container/Button").GetComponent<Button>().interactable = true;
                _informationPanelContent.transform
                    .Find("Loading_container/Button").GetComponent<Button>().interactable = true;
                _informationPanelContent.transform
                    .Find("SubsetSelection_container").gameObject.SetActive(true);
            }
            else
            {
                _informationPanelContent.transform
                    .Find("MaskFile_container/Button").GetComponent<Button>().interactable = false;
                _informationPanelContent.transform
                    .Find("Loading_container/Button").GetComponent<Button>().interactable = false;
                _informationPanelContent.transform
                    .Find("SubsetSelection_container").gameObject.SetActive(false);
                _loadTextLabel.text = "Not enough dimensions in selected image";
                _loadingText.SetActive(true);
            }
        }

        if (_showLoadDialogCoroutine != null)
            _coroutineHost.StopCoroutine(_showLoadDialogCoroutine);
    }

    // -----------------------------------------------------------------------
    // Mask browsing
    // -----------------------------------------------------------------------

    public void BrowseMaskFile()
    {
        string lastPath = PlayerPrefs.GetString("LastPath");
        if (!Directory.Exists(lastPath))
            lastPath = "";
        var extensions = new[]
        {
            new ExtensionFilter("Fits Files", "fits", "fit"),
            new ExtensionFilter("All Files", "*"),
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", lastPath, extensions, false, paths =>
        {
            if (paths.Length == 1)
            {
                PlayerPrefs.SetString("LastPath", Path.GetDirectoryName(paths[0]));
                PlayerPrefs.Save();
                _browseMaskFile(paths[0]);
            }
        });
    }

    private void _browseMaskFile(string path)
    {
        bool loadable = false;

        if (_maskPath != null)
        {
            _informationPanelContent.transform
                .Find("Loading_container/Button").GetComponent<Button>().interactable = false;

            _maskSize = 1;
            _maskPath = path;

            IntPtr fptr;
            int status = 0;
            if (FitsReader.FitsOpenFile(out fptr, _maskPath, out status, true) != 0)
                Debug.Log("Fits open failure... code #" + status);

            _informationPanelContent.transform
                .Find("MaskFile_container/MaskFilePath_text").GetComponent<TextMeshProUGUI>().text =
                Path.GetFileName(_maskPath);

            var headers = FitsReader.ExtractHeaders(fptr, out status);
            FitsReader.FitsCloseFile(fptr, out status);

            ParseNAxisInfo(headers, out _maskNAxis, out _maskAxisSize);

            if (_maskNAxis > 2)
            {
                int i2 = int.Parse(_zAxisDropdown.options[_zAxisDropdown.value].text) - 1;
                if (_axisSize[1] == _maskAxisSize[1] && _axisSize[2] == _maskAxisSize[2] &&
                    _axisSize[i2 + 1] == _maskAxisSize[3])
                {
                    loadable = true;
                    _informationPanelContent.transform
                        .Find("Loading_container/Button").GetComponent<Button>().interactable = true;
                    _informationPanelContent.transform
                        .Find("SubsetSelection_container").gameObject.SetActive(true);
                }
            }

            if (!loadable)
            {
                _informationPanelContent.transform
                    .Find("MaskFile_container/MaskFilePath_text").GetComponent<TextMeshProUGUI>().text = "...";
                _maskPath  = "";
                _showPopUp = true;
                _textPopUp = "Selected Mask\ndoesn't match image file";
            }
        }

        if (_showLoadDialogCoroutine != null)
            _coroutineHost.StopCoroutine(_showLoadDialogCoroutine);
    }

    // -----------------------------------------------------------------------
    // Axis-size check
    // -----------------------------------------------------------------------

    public void CheckImgMaskAxisSize()
    {
        if (_maskPath == "") return;

        int i2 = int.Parse(_zAxisDropdown.options[_zAxisDropdown.value].text) - 1;
        if (_axisSize[1] != _maskAxisSize[1] || _axisSize[2] != _maskAxisSize[2] ||
            _axisSize[i2 + 1] != _maskAxisSize[3])
        {
            _informationPanelContent.transform
                .Find("MaskFile_container/MaskFilePath_text").GetComponent<TextMeshProUGUI>().text = "...";
            _showPopUp = true;
            _textPopUp = "Selected axis size \ndoesn't match mask axis size";
            _informationPanelContent.transform
                .Find("Loading_container/Button").GetComponent<Button>().interactable = false;
        }
        else
        {
            _informationPanelContent.transform
                .Find("Loading_container/Button").GetComponent<Button>().interactable = true;
            _informationPanelContent.transform
                .Find("SubsetSelection_container").gameObject.SetActive(true);
        }
    }

    // -----------------------------------------------------------------------
    // Load trigger
    // -----------------------------------------------------------------------

    public void LoadFileFromFileSystem()
    {
        _loadCubeCoroutine = _coroutineHost.StartCoroutine(
            CreateLoadCoroutine(_imagePath, _maskPath, _hduSelectionIndex + 1));
    }

    public IEnumerator CreateLoadCoroutine(string imagePath, string maskPath, int hduSelection = 1)
    {
        _loadingText.gameObject.SetActive(true);
        _progressBar.gameObject.SetActive(true);

        if (CheckMemSpaceForCubes(imagePath, maskPath))
        {
            _loadTextLabel.text = "Cube too large to fit into RAM! Using virtual memory!";
            yield return new WaitForSeconds(5.0f);
        }

        _loadTextLabel.text = "Loading...";
        Debug.Log("Loading image " + imagePath + " and mask " + maskPath + ".");
        _progressBar.GetComponent<Slider>().value = 0;
        yield return new WaitForSeconds(0.001f);

        float zScale = 1f;
        if (_ratioDropdownIndex == 1)
        {
            int i2 = int.Parse(_zAxisDropdown.options[_zAxisDropdown.value].text) - 1;
            if (_axisSize.TryGetValue(1, out double xDim) && _axisSize.TryGetValue(i2 + 1, out double zDim))
                zScale = (float)(zDim / xDim);
        }

        var firstActiveRenderer = _getActiveRenderer();
        _loadTextLabel.text = "Replacing old cube...";
        _progressBar.GetComponent<Slider>().value = 1;
        yield return new WaitForSeconds(0.001f);

        if (firstActiveRenderer != null)
        {
            Debug.Log("Replacing data cube...");
            firstActiveRenderer.transform.gameObject.SetActive(false);
            _volumeCommandController.RemoveDataSet(firstActiveRenderer);

            try
            {
                _volumeInputController = UnityEngine.Object.FindObjectOfType<VolumeInputController>();
                _volumeInputController.gameObject.SetActive(false);
                _volumeInputController.gameObject.SetActive(true);

                _volumeCommandController.DisablePaintMode();
                _volumeCommandController.endThresholdEditing();
                _volumeCommandController.endZAxisEditing();
            }
            catch (Exception)
            {
                // ignored
            }

            firstActiveRenderer.Data.CleanUp(firstActiveRenderer.RandomVolume);
            firstActiveRenderer.Mask?.CleanUp(false);
            UnityEngine.Object.Destroy(firstActiveRenderer);
        }

        _loadTextLabel.text = "Building new cube...";
        _progressBar.GetComponent<Slider>().value = 2;
        Debug.Log("Instantiating new cube prefab.");
        yield return new WaitForSeconds(0.001f);

        var volumeDataSetManager = GameObject.Find("VolumeDataSetManager");
        var newCube = UnityEngine.Object.Instantiate(_cubeprefab, Vector3.zero, Quaternion.identity);
        newCube.transform.localScale = new Vector3(1, 1, zScale);
        newCube.SetActive(true);
        newCube.transform.SetParent(volumeDataSetManager.transform, false);

        var volDSRender = newCube.GetComponent<VolumeDataSetRenderer>();
        volDSRender.subsetBounds    = _subsetBounds.Subset;
        volDSRender.trueBounds      = _subsetBounds.TrueBounds;
        volDSRender.FileName        = imagePath;
        volDSRender.MaskFileName    = maskPath;
        volDSRender.SelectedHdu     = hduSelection;
        volDSRender.loadText        = _loadTextLabel;
        volDSRender.progressBar     = _progressBar.GetComponent<Slider>();
        volDSRender.CubeDepthAxis   = int.Parse(_zAxisDropdown.options[_zAxisDropdown.value].text) - 1;
        volDSRender.FileChanged     = false;
        _zAxisDropdown.interactable = false;

        _refreshRenderers();

        _volumeInputController.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.001f);
        _volumeInputController.gameObject.SetActive(true);

        var featureMenu = UnityEngine.Object.FindObjectOfType<FeatureMenuController>();
        if (featureMenu?.gameObject?.activeSelf == true)
        {
            featureMenu.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.001f);
            featureMenu.gameObject.SetActive(true);
        }

        _volumeCommandController.AddDataSet(newCube.GetComponent<VolumeDataSetRenderer>());
        _coroutineHost.StartCoroutine(newCube.GetComponent<VolumeDataSetRenderer>()._startFunc());

        while (!newCube.GetComponent<VolumeDataSetRenderer>().started)
            yield return new WaitForSeconds(.1f);

        _loadTextLabel.text = "Loading complete!";
        string completeMessage = "Loading image " + imagePath;
        if (maskPath != "")
            completeMessage += " and mask " + maskPath;
        completeMessage += " complete!";
        Debug.Log(completeMessage);
        _progressBar.GetComponent<Slider>().value = 6;
        yield return new WaitForSeconds(0.001f);

        VolumeLoaded?.Invoke(newCube.GetComponent<VolumeDataSetRenderer>());
    }

    public bool CheckMemSpaceForCubes(string imagePath, string maskPath)
    {
        int   ramSizeMB = SystemInfo.systemMemorySize;
        long  x = _subsetBounds.Subset[1] - _subsetBounds.Subset[0] + 1;
        long  y = _subsetBounds.Subset[3] - _subsetBounds.Subset[2] + 1;
        long  z = _subsetBounds.Subset[5] - _subsetBounds.Subset[4] + 1;
        long  nelem = x * y * z;
        float imgSize  = nelem * sizeof(float) / 1024f / 1024f;
        float maskSize = string.IsNullOrEmpty(maskPath) ? 0 : nelem * sizeof(short) / 1024f / 1024f;
        float sumSizeMB = imgSize + maskSize;
        if (sumSizeMB >= ramSizeMB)
        {
            Debug.LogWarning("Cube and mask size (" + sumSizeMB.ToString("F2") +
                             " MB) exceed RAM size (" + ramSizeMB.ToString("F2") + " MB)!");
            return true;
        }
        Debug.Log("Loading cube and mask of size " + sumSizeMB.ToString("F2") +
                  " MB with RAM size " + ramSizeMB.ToString("F2") + " MB.");
        return false;
    }

    // -----------------------------------------------------------------------
    // Subset UI — delegates to SubsetBoundsController
    // -----------------------------------------------------------------------

    public void onSubsetToggleSelected(bool val)
    {
        bool on = _subsetToggle.isOn;
        _informationPanelContent.transform.Find("SubsetLabel_container").gameObject.SetActive(on);
        _informationPanelContent.transform.Find("SubsetMin_container").gameObject.SetActive(on);
        _informationPanelContent.transform.Find("SubsetMax_container").gameObject.SetActive(on);
        if (on)
            _inputFields[_inputIndex].Select();
    }

    public void setSubsetBounds() =>
        _subsetBounds.ResetToCurrentBounds();

    public void updateSubsetZMax(int val = 0)
    {
        if (!int.TryParse(_zAxisDropdown.options[_zAxisDropdown.value].text, out int axisKey)) return;
        if (!_axisSize.TryGetValue(axisKey, out double newMaxZD)) return;
        _subsetBounds.UpdateZMax((int)newMaxZD);
    }

    public void checkSubsetBounds(string val = "") =>
        _subsetBounds.Validate(val);

    // -----------------------------------------------------------------------
    // HDU selection
    // -----------------------------------------------------------------------

    public void ChangeHduSelection(TMP_Dropdown dropdown)
    {
        _loadingText.SetActive(false);
        IntPtr fptr;
        int status = 0;
        _hduSelectionIndex = dropdown.value;

        if (FitsReader.FitsOpenFile(out fptr, _imagePath, out status, true) != 0)
            Debug.Log("Fits open failure... code #" + status);

        FitsReader.FitsMovabsHdu(fptr, _hduSelectionIndex + 1, out _, out status);
        UpdateHeaderFromFits(fptr);
        FitsReader.FitsCloseFile(fptr, out status);

        if (IsLoadable())
        {
            _informationPanelContent.transform
                .Find("MaskFile_container/Button").GetComponent<Button>().interactable = true;
            _informationPanelContent.transform
                .Find("Loading_container/Button").GetComponent<Button>().interactable = true;
            _informationPanelContent.transform
                .Find("SubsetSelection_container").gameObject.SetActive(true);
        }
        else
        {
            _informationPanelContent.transform
                .Find("MaskFile_container/Button").GetComponent<Button>().interactable = false;
            _informationPanelContent.transform
                .Find("Loading_container/Button").GetComponent<Button>().interactable = false;
            _informationPanelContent.transform
                .Find("SubsetSelection_container").gameObject.SetActive(false);
            _loadTextLabel.text = "Not enough dimensions in selected image";
            _loadingText.SetActive(true);
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void UpdateHeaderFromFits(IntPtr fptr)
    {
        var headers = FitsReader.ExtractHeaders(fptr, out _);
        ParseNAxisInfo(headers, out _imageNAxis, out _axisSize);

        var headerText = new StringBuilder();
        foreach (var entry in headers)
            headerText.Append(entry.Key).Append("\t\t ").Append(entry.Value).Append('\n');

        _informationPanelContent.transform
            .Find("Header_container/Scroll View/Viewport/Content/Header")
            .GetComponent<TextMeshProUGUI>().text = headerText.ToString();

        _informationPanelContent.transform
            .Find("Header_container/Scroll View/Scrollbar Vertical")
            .GetComponent<Scrollbar>().value = 1;
    }

    private bool IsLoadable()
    {
        var    list     = new List<double>();
        bool   loadable = false;
        string localMsg = "";

        if (_imageNAxis > 2)
        {
            if (_imageNAxis == 3)
            {
                foreach (var axes in _axisSize)
                {
                    localMsg += "Axis[" + axes.Key + "]: " + axes.Value + "\n";
                    if (axes.Value > 1) { list.Add(axes.Key); _imageSize *= axes.Value; }
                }

                if (list.Count == 3)
                {
                    loadable = true;
                    _subsetBounds.SetBoundsAndReset(
                        (int)_axisSize[list[0]], (int)_axisSize[list[1]], (int)_axisSize[list[2]]);
                }
            }
            else
            {
                foreach (var axes in _axisSize)
                {
                    localMsg += "Axis[" + axes.Key + "]: " + axes.Value + "\n";
                    if (axes.Value > 1) { list.Add(axes.Key); _imageSize *= axes.Value; }
                }

                if (list.Count == 3)
                {
                    loadable = true;
                    _subsetBounds.SetBoundsAndReset(
                        (int)_axisSize[list[0]], (int)_axisSize[list[1]], (int)_axisSize[list[2]]);
                }
                else
                {
                    _informationPanelContent.transform
                        .Find("Axes_container").gameObject.SetActive(true);
                }
            }

            _zAxisDropdown.interactable = false;
            _zAxisDropdown.ClearOptions();
            foreach (var axes in _axisSize)
                if (axes.Value > 1 && axes.Key > 2)
                    _zAxisDropdown.options.Add(new TMP_Dropdown.OptionData { text = axes.Key.ToString() });
            _zAxisDropdown.RefreshShownValue();
            _zAxisDropdown.value = 0;

            if (!loadable && list.Count < 3)
            {
                _showPopUp = true;
                _textPopUp = "NAxis_ " + _imageNAxis + "\n" + localMsg;
            }
            else if (!loadable && list.Count > 3)
            {
                _zAxisDropdown.interactable = true;
                loadable = true;
                if (int.TryParse(_zAxisDropdown.options[_zAxisDropdown.value].text, out int zAxisIdx))
                {
                    zAxisIdx -= 1;
                    Debug.Log("The list has " + list.Count + " items, and the dropdown points to index " + zAxisIdx + "!");
                    _subsetBounds.SetBoundsAndReset(
                        (int)_axisSize[list[0]], (int)_axisSize[list[1]], (int)_axisSize[list[zAxisIdx]]);
                }
            }
        }
        else
        {
            loadable = false;
            localMsg = "Please select a valid cube!";
        }

        return loadable;
    }

    /// <summary>
    /// Extracts NAxis count and per-axis sizes from a FITS header dictionary.
    /// Shared by image and mask header parsing to avoid duplication.
    /// </summary>
    private static void ParseNAxisInfo(
        IDictionary<string, string>       headers,
        out double                         nAxis,
        out Dictionary<double, double>     axisSizes)
    {
        nAxis     = 0;
        axisSizes = new Dictionary<double, double>();

        foreach (var entry in headers)
        {
            if (entry.Key.Length <= 4 || entry.Key.Substring(0, 5) != "NAXIS") continue;
            string sub = entry.Key.Substring(5);
            if (sub == "")
                nAxis = Convert.ToDouble(entry.Value, CultureInfo.InvariantCulture);
            else
                axisSizes[Convert.ToDouble(sub, CultureInfo.InvariantCulture)] =
                    Convert.ToDouble(entry.Value, CultureInfo.InvariantCulture);
        }
    }
}
