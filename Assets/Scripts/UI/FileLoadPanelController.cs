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
/// Plain C# class (not MonoBehaviour). Owns all file-browsing and cube-load logic
/// extracted from CanvassDesktop. Coroutines are scheduled on the host MonoBehaviour.
/// </summary>
public class FileLoadPanelController
{
    // --- Injected dependencies ---
    private readonly MonoBehaviour _coroutineHost;
    private readonly CanvassDesktop _shell;
    private readonly GameObject _informationPanelContent;
    private readonly GameObject _fileLoadCanvassDesktop;
    private readonly GameObject _mainCanvassDesktop;
    private readonly GameObject _loadingText;
    private readonly TextMeshProUGUI _loadTextLabel;
    private readonly GameObject _progressBar;
    private readonly GameObject _cubeprefab;
    private readonly GameObject _volumePlayer;
    private readonly GameObject _welcomeMenu;
    private readonly GameObject _sourceRowPrefab;
    private readonly GameObject _sourcesPanelContent;
    private readonly MenuBarBehaviour _menuBarBehaviour;
    private readonly QuickMenuController _quickMenuController;

    // --- File paths ---
    private string _imagePath = "";
    private string _maskPath = "";
    private int _hduSelectionIndex = 0;

    // --- Axis / size tracking ---
    private double _imageNAxis = 0;
    private double _imageSize = 1;
    private double _maskNAxis = 0;
    private double _maskSize = 1;

    // --- Subset bounds ---
    private int _subsetMin = 1;
    private int _subsetMax_X = 2;
    private int _subsetMax_Y = 2;
    private int _subsetMax_Z = 2;
    private int[] _subset;
    private int[] _trueBounds;

    // --- Axis size dictionaries ---
    private Dictionary<double, double> _axisSize = null;
    private Dictionary<double, double> _maskAxisSize = null;

    // --- Rendering ratio ---
    private int _ratioDropdownIndex = 0;

    // --- Popup state ---
    private bool _showPopUp = false;
    private string _textPopUp = "";

    // --- UI references (resolved in Initialize) ---
    private Toggle _subsetToggle;
    private TMP_InputField _subset_XMin_input;
    private TMP_InputField _subset_XMax_input;
    private TMP_InputField _subset_YMin_input;
    private TMP_InputField _subset_YMax_input;
    private TMP_InputField _subset_ZMin_input;
    private TMP_InputField _subset_ZMax_input;
    private TMP_Dropdown _zAxisDropdown;

    // --- Input field tab-cycling ---
    private readonly List<TMP_InputField> _inputFields;
    private int _inputIndex;

    // --- Coroutine handles (stored on host) ---
    private Coroutine _loadCubeCoroutine;
    private Coroutine _showLoadDialogCoroutine;

    // --- VolumeInputController / CommandController refs resolved at load time ---
    private VolumeInputController _volumeInputController;
    private VolumeCommandController _volumeCommandController;

    /// <summary>
    /// Raised after a cube finishes loading and the VolumeDataSetRenderer is ready.
    /// CanvassDesktop subscribes to this to perform post-load UI wiring.
    /// </summary>
    public event Action<VolumeDataSetRenderer> VolumeLoaded;

    public FileLoadPanelController(
        MonoBehaviour coroutineHost,
        CanvassDesktop shell,
        GameObject informationPanelContent,
        GameObject fileLoadCanvassDesktop,
        GameObject mainCanvassDesktop,
        GameObject loadingText,
        TextMeshProUGUI loadTextLabel,
        GameObject progressBar,
        GameObject cubeprefab,
        GameObject volumePlayer,
        GameObject welcomeMenu,
        GameObject sourceRowPrefab,
        GameObject sourcesPanelContent,
        List<TMP_InputField> inputFields,
        MenuBarBehaviour menuBarBehaviour,
        QuickMenuController quickMenuController)
    {
        _coroutineHost         = coroutineHost;
        _shell                 = shell;
        _informationPanelContent = informationPanelContent;
        _fileLoadCanvassDesktop  = fileLoadCanvassDesktop;
        _mainCanvassDesktop      = mainCanvassDesktop;
        _loadingText             = loadingText;
        _loadTextLabel           = loadTextLabel;
        _progressBar             = progressBar;
        _cubeprefab              = cubeprefab;
        _volumePlayer            = volumePlayer;
        _welcomeMenu             = welcomeMenu;
        _sourceRowPrefab         = sourceRowPrefab;
        _sourcesPanelContent     = sourcesPanelContent;
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

        _subsetToggle = _informationPanelContent.gameObject.transform
            .Find("SubsetSelection_container").gameObject.transform
            .Find("LoadSubset_Toggle").GetComponent<Toggle>();

        _subset_XMin_input = _informationPanelContent.gameObject.transform
            .Find("SubsetMin_container").gameObject.transform
            .Find("SubsetX_min").GetComponent<TMP_InputField>();
        _subset_XMin_input.onEndEdit.AddListener(checkSubsetBounds);

        _subset_YMin_input = _informationPanelContent.gameObject.transform
            .Find("SubsetMin_container").gameObject.transform
            .Find("SubsetY_min").GetComponent<TMP_InputField>();
        _subset_YMin_input.onEndEdit.AddListener(checkSubsetBounds);

        _subset_ZMin_input = _informationPanelContent.gameObject.transform
            .Find("SubsetMin_container").gameObject.transform
            .Find("SubsetZ_min").GetComponent<TMP_InputField>();
        _subset_ZMin_input.onEndEdit.AddListener(checkSubsetBounds);

        _subset_XMax_input = _informationPanelContent.gameObject.transform
            .Find("SubsetMax_container").gameObject.transform
            .Find("SubsetX_max").GetComponent<TMP_InputField>();
        _subset_XMax_input.onEndEdit.AddListener(checkSubsetBounds);

        _subset_YMax_input = _informationPanelContent.gameObject.transform
            .Find("SubsetMax_container").gameObject.transform
            .Find("SubsetY_max").GetComponent<TMP_InputField>();
        _subset_YMax_input.onEndEdit.AddListener(checkSubsetBounds);

        _subset_ZMax_input = _informationPanelContent.gameObject.transform
            .Find("SubsetMax_container").gameObject.transform
            .Find("SubsetZ_max").GetComponent<TMP_InputField>();
        _subset_ZMax_input.onEndEdit.AddListener(checkSubsetBounds);

        _zAxisDropdown = _informationPanelContent.gameObject.transform
            .Find("Axes_container").gameObject.transform
            .Find("Z_Dropdown").GetComponent<TMP_Dropdown>();
        _zAxisDropdown.onValueChanged.AddListener(updateSubsetZMax);

        _inputIndex = 0;

        _subset_XMin_input.text = _subsetMin.ToString();
        _subset_XMax_input.text = _subsetMax_X.ToString();
        _subset_YMin_input.text = _subsetMin.ToString();
        _subset_YMax_input.text = _subsetMax_Y.ToString();
        _subset_ZMin_input.text = _subsetMin.ToString();
        _subset_ZMax_input.text = _subsetMax_Z.ToString();

        _subset     = new int[6];
        _trueBounds = new int[6];
        _subset[0] = _subset[2] = _subset[4] = _subsetMin;
        _subset[1] = _subsetMax_X;
        _subset[3] = _subsetMax_Y;
        _subset[5] = _subsetMax_Z;
    }

    // -----------------------------------------------------------------------
    // Update — called each frame from CanvassDesktop.Update()
    // -----------------------------------------------------------------------

    /// <summary>
    /// Handles tab-key cycling among inputFields. Call from CanvassDesktop.Update().
    /// </summary>
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

    /// <summary>
    /// Draws the invalid-cube popup window. Call from CanvassDesktop.OnGUI().
    /// </summary>
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

    /// <summary>
    /// Updates _inputIndex when the user directly selects an input field.
    /// </summary>
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
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", lastPath, extensions, false, (string[] paths) =>
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

            // Each time a new image is selected, reset the mask and disable the load button
            _maskPath = "";
            _informationPanelContent.gameObject.transform
                .Find("MaskFile_container").gameObject.transform
                .Find("Button").GetComponent<Button>().interactable = false;
            _informationPanelContent.gameObject.transform
                .Find("MaskFile_container").gameObject.transform
                .Find("MaskFilePath_text").GetComponent<TextMeshProUGUI>().text = "...";
            _informationPanelContent.gameObject.transform
                .Find("Loading_container").gameObject.transform
                .Find("Button").GetComponent<Button>().interactable = false;

            IntPtr fptr;
            int status = 0;

            if (FitsReader.FitsOpenFile(out fptr, _imagePath, out status, true) != 0)
                Debug.Log("Fits open failure... code #" + status.ToString());

            _axisSize = new Dictionary<double, double>();

            // If there are more than 1 HDUs in the fits file, enable the dropdown and populate it
            FitsReader.FitsGetHduCount(fptr, out int hduNum, out status);
            var hduNames = new List<string>();
            var hduName = new StringBuilder(80);
            for (var i = 0; i < hduNum; i++)
            {
                FitsReader.FitsMovabsHdu(fptr, i + 1, out _, out status);
                hduName.Clear();
                if (FitsReader.FitsReadKey(fptr, (int)FitsReader.DataType.TSTRING, "EXTNAME", hduName,
                        IntPtr.Zero, out status) != 0)
                {
                    status = 0;
                    if (FitsReader.FitsReadKey(fptr, (int)FitsReader.DataType.TSTRING, "HDUNAME", hduName,
                            IntPtr.Zero, out status) != 0)
                    {
                        Debug.Log("Could not find EXTNAME or HDUNAME in HDU " + (i + 1) + "! Using default name.");
                        hduName.Append("HDU " + (i + 1));
                        status = 0;
                    }
                }
                hduNames.Add(hduName.ToString());
            }

            _hduSelectionIndex = 0;
            FitsReader.FitsMovabsHdu(fptr, _hduSelectionIndex + 1, out _, out status);

            var hduContainer = _informationPanelContent.gameObject.transform
                .Find("HeaderTitle_container").transform
                .Find("Hdu_container").gameObject;
            hduContainer.transform.Find("Hdu_dropdown").GetComponent<TMP_Dropdown>().ClearOptions();
            hduContainer.transform.Find("Hdu_dropdown").GetComponent<TMP_Dropdown>().value = 0;

            if (hduNames.Count > 1)
            {
                hduContainer.SetActive(true);
                for (int i = 0; i < hduNames.Count; i++)
                {
                    hduContainer.transform.Find("Hdu_dropdown").GetComponent<TMP_Dropdown>().options.Add(
                        new TMP_Dropdown.OptionData() { text = i + 1 + ": " + hduNames[i] });
                }
                hduContainer.transform.Find("Hdu_dropdown").GetComponent<TMP_Dropdown>().RefreshShownValue();
            }
            else
            {
                hduContainer.SetActive(false);
            }

            // Set the path of selected file to the UI
            _informationPanelContent.gameObject.transform
                .Find("ImageFile_container").gameObject.transform
                .Find("ImageFilePath_text").GetComponent<TextMeshProUGUI>().text =
                Path.GetFileName(_imagePath);

            UpdateHeaderFromFits(fptr);
            FitsReader.FitsCloseFile(fptr, out status);

            if (IsLoadable())
            {
                _informationPanelContent.gameObject.transform
                    .Find("MaskFile_container").gameObject.transform
                    .Find("Button").GetComponent<Button>().interactable = true;
                _informationPanelContent.gameObject.transform
                    .Find("Loading_container").gameObject.transform
                    .Find("Button").GetComponent<Button>().interactable = true;
                _informationPanelContent.gameObject.transform
                    .Find("SubsetSelection_container").gameObject.SetActive(true);
                setSubsetBounds();
            }
            else
            {
                _informationPanelContent.gameObject.transform
                    .Find("MaskFile_container").gameObject.transform
                    .Find("Button").GetComponent<Button>().interactable = false;
                _informationPanelContent.gameObject.transform
                    .Find("Loading_container").gameObject.transform
                    .Find("Button").GetComponent<Button>().interactable = false;
                _informationPanelContent.gameObject.transform
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
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", lastPath, extensions, false, (string[] paths) =>
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
            _informationPanelContent.gameObject.transform
                .Find("Loading_container").gameObject.transform
                .Find("Button").GetComponent<Button>().interactable = false;

            _maskSize = 1;
            _maskPath = path;

            IntPtr fptr;
            int status = 0;

            if (FitsReader.FitsOpenFile(out fptr, _maskPath, out status, true) != 0)
                Debug.Log("Fits open failure... code #" + status.ToString());

            _informationPanelContent.gameObject.transform
                .Find("MaskFile_container").gameObject.transform
                .Find("MaskFilePath_text").GetComponent<TextMeshProUGUI>().text =
                Path.GetFileName(_maskPath);

            _maskAxisSize = new Dictionary<double, double>();
            var list = new List<double>();

            IDictionary<string, string> headerDictionary = FitsReader.ExtractHeaders(fptr, out status);
            FitsReader.FitsCloseFile(fptr, out status);

            foreach (KeyValuePair<string, string> entry in headerDictionary)
            {
                if (entry.Key.Length > 4)
                    switch (entry.Key.Substring(0, 5))
                    {
                        case "NAXIS":
                            string sub = entry.Key.Substring(5);
                            if (sub == "")
                                _maskNAxis = Convert.ToDouble(entry.Value, CultureInfo.InvariantCulture);
                            else
                                _maskAxisSize.Add(Convert.ToDouble(sub, CultureInfo.InvariantCulture),
                                    Convert.ToDouble(entry.Value, CultureInfo.InvariantCulture));
                            break;
                    }
            }

            if (_maskNAxis > 2)
            {
                int i2 = int.Parse(_zAxisDropdown.options[_zAxisDropdown.value].text) - 1;
                if (_axisSize[1] == _maskAxisSize[1] && _axisSize[2] == _maskAxisSize[2] &&
                    _axisSize[i2 + 1] == _maskAxisSize[3])
                {
                    loadable = true;
                    _informationPanelContent.gameObject.transform
                        .Find("Loading_container").gameObject.transform
                        .Find("Button").GetComponent<Button>().interactable = true;
                    _informationPanelContent.gameObject.transform
                        .Find("SubsetSelection_container").gameObject.SetActive(true);
                }
                else
                {
                    loadable = false;
                }
            }

            if (!loadable)
            {
                _informationPanelContent.gameObject.transform
                    .Find("MaskFile_container").gameObject.transform
                    .Find("MaskFilePath_text").GetComponent<TextMeshProUGUI>().text = "...";
                _maskPath = "";
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
        if (_maskPath != "")
        {
            int i2 = int.Parse(_zAxisDropdown.options[_zAxisDropdown.value].text) - 1;

            if (_axisSize[1] != _maskAxisSize[1] || _axisSize[2] != _maskAxisSize[2] ||
                _axisSize[i2 + 1] != _maskAxisSize[3])
            {
                _informationPanelContent.gameObject.transform
                    .Find("MaskFile_container").gameObject.transform
                    .Find("MaskFilePath_text").GetComponent<TextMeshProUGUI>().text = "...";
                _showPopUp = true;
                _textPopUp = "Selected axis size \ndoesn't match mask axis size";
                _informationPanelContent.gameObject.transform
                    .Find("Loading_container").gameObject.transform
                    .Find("Button").GetComponent<Button>().interactable = false;
            }
            else
            {
                _informationPanelContent.gameObject.transform
                    .Find("Loading_container").gameObject.transform
                    .Find("Button").GetComponent<Button>().interactable = true;
                _informationPanelContent.gameObject.transform
                    .Find("SubsetSelection_container").gameObject.SetActive(true);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Load trigger
    // -----------------------------------------------------------------------

    /// <summary>
    /// Schedules the load coroutine on the host MonoBehaviour.
    /// </summary>
    public void LoadFileFromFileSystem()
    {
        _loadCubeCoroutine = _coroutineHost.StartCoroutine(
            CreateLoadCoroutine(_imagePath, _maskPath, _hduSelectionIndex + 1));
    }

    /// <summary>
    /// The IEnumerator that performs the full cube-load sequence. Fires VolumeLoaded on completion.
    /// StartCoroutine must be called on the host MonoBehaviour (see LoadFileFromFileSystem).
    /// </summary>
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
            double xDim, zDim;
            if (_axisSize.TryGetValue(1, out xDim) && _axisSize.TryGetValue(i2 + 1, out zDim))
                zScale = (float)(zDim / xDim);
        }

        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
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

        // Find the VolumeDataSetManager for parenting
        var volumeDataSetManager = GameObject.Find("VolumeDataSetManager");

        GameObject newCube = UnityEngine.Object.Instantiate(_cubeprefab, new Vector3(0, 0f, 0), Quaternion.identity);
        newCube.transform.localScale = new Vector3(1, 1, zScale);
        newCube.SetActive(true);
        newCube.transform.SetParent(volumeDataSetManager.transform, false);

        var volDSRender = newCube.GetComponent<VolumeDataSetRenderer>();
        volDSRender.subsetBounds    = _subset;
        volDSRender.trueBounds      = _trueBounds;
        volDSRender.FileName        = imagePath;
        volDSRender.MaskFileName    = maskPath;
        volDSRender.SelectedHdu     = hduSelection;
        volDSRender.loadText        = _loadTextLabel;
        volDSRender.progressBar     = _progressBar.GetComponent<Slider>();
        volDSRender.CubeDepthAxis   = int.Parse(_zAxisDropdown.options[_zAxisDropdown.value].text) - 1;
        volDSRender.FileChanged     = false;
        _zAxisDropdown.interactable = false;

        _shell.CheckCubesDataSet();

        // Toggle VolumeInputController to refresh its dataset list
        _volumeInputController.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.001f);
        _volumeInputController.gameObject.SetActive(true);

        // Toggle FeatureMenuController to reload the source list
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

        // Notify the shell (CanvassDesktop.OnVolumeLoaded) to finish wiring
        VolumeLoaded?.Invoke(newCube.GetComponent<VolumeDataSetRenderer>());
    }

    /// <summary>
    /// Checks available system RAM vs. the size of the cube and mask to be loaded.
    /// Returns true if the combined size exceeds available RAM (warning condition).
    /// </summary>
    public bool CheckMemSpaceForCubes(string imagePath, string maskPath)
    {
        int ramSizeMB = SystemInfo.systemMemorySize;
        float fileSize = new FileInfo(imagePath).Length;
        long x = _subset[1] - _subset[0] + 1;
        long y = _subset[3] - _subset[2] + 1;
        long z = _subset[5] - _subset[4] + 1;
        long nelem = x * y * z;
        float imgSize  = nelem * sizeof(float) / 1024f / 1024f;
        float maskSize = string.IsNullOrEmpty(maskPath) ? 0 : nelem * sizeof(short) / 1024f / 1024f;
        float sumSizeMB = imgSize + maskSize;
        if (sumSizeMB >= ramSizeMB)
        {
            Debug.LogWarning("Cube and mask size (" + sumSizeMB.ToString("F2") + " MB) exceed RAM size (" +
                             ramSizeMB.ToString("F2") + " MB)!");
            return true;
        }
        Debug.Log("Loading cube and mask of size " + sumSizeMB.ToString("F2") + " MB with RAM size " +
                  ramSizeMB.ToString("F2") + " MB.");
        return false;
    }

    // -----------------------------------------------------------------------
    // Subset UI
    // -----------------------------------------------------------------------

    /// <summary>
    /// Toggles visibility of the subset input rows.
    /// </summary>
    public void onSubsetToggleSelected(bool val)
    {
        if (_subsetToggle.isOn)
        {
            _informationPanelContent.gameObject.transform.Find("SubsetLabel_container").gameObject.SetActive(true);
            _informationPanelContent.gameObject.transform.Find("SubsetMin_container").gameObject.SetActive(true);
            _informationPanelContent.gameObject.transform.Find("SubsetMax_container").gameObject.SetActive(true);
            _inputFields[_inputIndex].Select();
        }
        else
        {
            _informationPanelContent.gameObject.transform.Find("SubsetLabel_container").gameObject.SetActive(false);
            _informationPanelContent.gameObject.transform.Find("SubsetMin_container").gameObject.SetActive(false);
            _informationPanelContent.gameObject.transform.Find("SubsetMax_container").gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Resets subset input fields to their initial (full-cube) values.
    /// </summary>
    public void setSubsetBounds()
    {
        _subset_XMin_input.text = _subsetMin.ToString();
        _subset_YMin_input.text = _subsetMin.ToString();
        _subset_ZMin_input.text = _subsetMin.ToString();
        _subset_XMax_input.text = _subsetMax_X.ToString();
        _subset_YMax_input.text = _subsetMax_Y.ToString();
        _subset_ZMax_input.text = _subsetMax_Z.ToString();

        _subset[0] = _subset[2] = _subset[4] = _trueBounds[0] = _trueBounds[2] = _trueBounds[4] = _subsetMin;
        _subset[1] = _trueBounds[1] = _subsetMax_X;
        _subset[3] = _trueBounds[3] = _subsetMax_Y;
        _subset[5] = _trueBounds[5] = _subsetMax_Z;
    }

    /// <summary>
    /// Updates the Z-axis max bound when the Z-axis dropdown selection changes.
    /// </summary>
    public void updateSubsetZMax(int val = 0)
    {
        int i2;
        int.TryParse(_zAxisDropdown.options[_zAxisDropdown.value].text, out i2);
        i2 -= 1;
        int oldMaxZ = _subsetMax_Z;
        _subsetMax_Z = (int)_axisSize[i2 + 1];
        string val1 = _subset_ZMax_input.text;
        int valInt = 0;
        if (Int32.TryParse(val1, out valInt))
        {
            if (valInt < _subsetMin)
                _subset_ZMax_input.text = _subsetMin.ToString();
            else if (valInt > _subsetMax_Z || valInt == oldMaxZ)
                _subset_ZMax_input.text = _subsetMax_Z.ToString();
        }

        _subset[0] = _subset[2] = _subset[4] = _subsetMin;
        _subset[1] = _subsetMax_X;
        _subset[3] = _subsetMax_Y;
        _subset[5] = _subsetMax_Z;
    }

    /// <summary>
    /// Validates and clamps all subset input fields. Called whenever an input field finishes editing.
    /// </summary>
    public void checkSubsetBounds(string val1 = "")
    {
        // --- X Max ---
        string val = _subset_XMax_input.text;
        int valInt = 0;
        if (Int32.TryParse(val, out valInt))
        {
            if (valInt < _subsetMin)
            {
                Debug.Log(val + " is less than the minimum which is " + _subsetMin + "!");
                _subset_XMax_input.text = _subset[0].ToString();
            }
            else if (valInt > _subsetMax_X)
            {
                Debug.Log(val + " is more than the maximum which is " + _subsetMax_X + "!");
                _subset_XMax_input.text = _subsetMax_X.ToString();
            }
            else if (valInt < _subset[0])
            {
                Debug.Log(val + " is less than the current chosen lower bound which is " + _subset[0] + "!");
                _subset_XMax_input.text = _subset[0].ToString();
            }
        }
        else
        {
            Debug.Log(val + " is not a number!");
            _subset_XMax_input.text = _subsetMax_X.ToString();
        }

        // --- Y Max ---
        val = _subset_YMax_input.text;
        valInt = 0;
        if (Int32.TryParse(val, out valInt))
        {
            if (valInt < _subsetMin)
            {
                Debug.Log(val + " is less than the minimum which is " + _subsetMin + "!");
                _subset_YMax_input.text = _subset[2].ToString();
            }
            else if (valInt > _subsetMax_Y)
            {
                Debug.Log(val + " is more than the maximum which is " + _subsetMax_Y + "!");
                _subset_YMax_input.text = _subsetMax_Y.ToString();
            }
            else if (valInt < _subset[2])
            {
                Debug.Log(val + " is less than the current chosen lower bound which is " + _subset[2] + "!");
                _subset_YMax_input.text = _subset[2].ToString();
            }
        }
        else
        {
            Debug.Log(val + " is not a number!");
            _subset_YMax_input.text = _subsetMax_Y.ToString();
        }

        // --- Z Max ---
        val = _subset_ZMax_input.text;
        valInt = 0;
        if (Int32.TryParse(val, out valInt))
        {
            if (valInt < _subsetMin)
            {
                Debug.Log(val + " is less than the minimum which is " + _subsetMin + "!");
                _subset_ZMax_input.text = _subset[4].ToString();
            }
            else if (valInt > _subsetMax_Z)
            {
                Debug.Log(val + " is more than the maximum which is " + _subsetMax_Z + "!");
                _subset_ZMax_input.text = _subsetMax_Z.ToString();
            }
            else if (valInt < _subset[4])
            {
                Debug.Log(val + " is less than the current chosen lower bound which is " + _subset[4] + "!");
                _subset_ZMax_input.text = _subset[4].ToString();
            }
        }
        else
        {
            Debug.Log(val + " is not a number!");
            _subset_ZMax_input.text = _subsetMax_Z.ToString();
        }

        // --- X Min ---
        val = _subset_XMin_input.text;
        valInt = 0;
        if (Int32.TryParse(val, out valInt))
        {
            if (valInt < _subsetMin)
            {
                Debug.Log(val + " is less than the minimum which is " + _subsetMin + "!");
                _subset_XMin_input.text = _subsetMin.ToString();
            }
            else if (valInt > _subsetMax_X)
            {
                Debug.Log(val + " is more than the maximum which is " + _subsetMax_X + "!");
                _subset_XMin_input.text = _subset[1].ToString();
            }
            else if (valInt > _subset[1])
            {
                Debug.Log(val + " is more than the current chosen upper bound which is " + _subset[1] + "!");
                _subset_XMin_input.text = _subset[1].ToString();
            }
        }
        else
        {
            Debug.Log(val + " is not a number!");
            _subset_XMin_input.text = _subsetMin.ToString();
        }

        // --- Y Min ---
        val = _subset_YMin_input.text;
        valInt = 0;
        if (Int32.TryParse(val, out valInt))
        {
            if (valInt < _subsetMin)
            {
                Debug.Log(val + " is less than the minimum which is " + _subsetMin + "!");
                _subset_YMin_input.text = _subsetMin.ToString();
            }
            else if (valInt > _subsetMax_Y)
            {
                Debug.Log(val + " is more than the maximum which is " + _subsetMax_Y + "!");
                _subset_YMin_input.text = _subset[3].ToString();
            }
            else if (valInt > _subset[3])
            {
                Debug.Log(val + " is more than the current chosen upper bound which is " + _subset[3] + "!");
                _subset_YMin_input.text = _subset[3].ToString();
            }
        }
        else
        {
            Debug.Log(val + " is not a number!");
            _subset_YMin_input.text = _subsetMin.ToString();
        }

        // --- Z Min ---
        val = _subset_ZMin_input.text;
        valInt = 0;
        if (Int32.TryParse(val, out valInt))
        {
            if (valInt < _subsetMin)
            {
                Debug.Log(val + " is less than the minimum which is " + _subsetMin + "!");
                _subset_ZMin_input.text = _subsetMin.ToString();
            }
            else if (valInt > _subsetMax_Z)
            {
                Debug.Log(val + " is more than the maximum which is " + _subsetMax_Z + "!");
                _subset_ZMin_input.text = _subset[5].ToString();
            }
            else if (valInt > _subset[5])
            {
                Debug.Log(val + " is more than the current chosen upper bound which is " + _subset[5] + "!");
                _subset_ZMin_input.text = _subset[5].ToString();
            }
        }
        else
        {
            Debug.Log(val + " is not a number!");
            _subset_ZMin_input.text = _subsetMin.ToString();
        }

        _subset[0] = Int32.Parse(_subset_XMin_input.text);
        _subset[1] = Int32.Parse(_subset_XMax_input.text);
        _subset[2] = Int32.Parse(_subset_YMin_input.text);
        _subset[3] = Int32.Parse(_subset_YMax_input.text);
        _subset[4] = Int32.Parse(_subset_ZMin_input.text);
        _subset[5] = Int32.Parse(_subset_ZMax_input.text);
    }

    // -----------------------------------------------------------------------
    // HDU selection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called when the user changes the HDU selection from the dropdown.
    /// Updates the fitsfile instance to point to the new HDU and refreshes the header display.
    /// </summary>
    public void ChangeHduSelection(TMP_Dropdown dropdown)
    {
        _loadingText.SetActive(false);
        IntPtr fptr;
        int status = 0;
        _hduSelectionIndex = dropdown.value;

        if (FitsReader.FitsOpenFile(out fptr, _imagePath, out status, true) != 0)
            Debug.Log("Fits open failure... code #" + status.ToString());

        FitsReader.FitsMovabsHdu(fptr, _hduSelectionIndex + 1, out int hdutype, out status);
        UpdateHeaderFromFits(fptr);
        FitsReader.FitsCloseFile(fptr, out status);

        if (IsLoadable())
        {
            _informationPanelContent.gameObject.transform
                .Find("MaskFile_container").gameObject.transform
                .Find("Button").GetComponent<Button>().interactable = true;
            _informationPanelContent.gameObject.transform
                .Find("Loading_container").gameObject.transform
                .Find("Button").GetComponent<Button>().interactable = true;
            _informationPanelContent.gameObject.transform
                .Find("SubsetSelection_container").gameObject.SetActive(true);
            setSubsetBounds();
        }
        else
        {
            _informationPanelContent.gameObject.transform
                .Find("MaskFile_container").gameObject.transform
                .Find("Button").GetComponent<Button>().interactable = false;
            _informationPanelContent.gameObject.transform
                .Find("Loading_container").gameObject.transform
                .Find("Button").GetComponent<Button>().interactable = false;
            _informationPanelContent.gameObject.transform
                .Find("SubsetSelection_container").gameObject.SetActive(false);
            _loadTextLabel.text = "Not enough dimensions in selected image";
            _loadingText.SetActive(true);
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes the FITS header to the scroll view in the information panel.
    /// Also populates _axisSize and _imageNAxis.
    /// </summary>
    private void UpdateHeaderFromFits(IntPtr fptr)
    {
        int status;
        string header = "";
        _axisSize.Clear();
        IDictionary<string, string> headerDictionary = FitsReader.ExtractHeaders(fptr, out status);

        foreach (KeyValuePair<string, string> entry in headerDictionary)
        {
            if (entry.Key.Length > 4)
                switch (entry.Key.Substring(0, 5))
                {
                    case "NAXIS":
                        string sub = entry.Key.Substring(5);
                        if (sub == "")
                            _imageNAxis = Convert.ToDouble(entry.Value, CultureInfo.InvariantCulture);
                        else
                            _axisSize.Add(Convert.ToDouble(sub, CultureInfo.InvariantCulture),
                                Convert.ToDouble(entry.Value, CultureInfo.InvariantCulture));
                        break;
                }

            header += entry.Key + "\t\t " + entry.Value + "\n";
        }

        _informationPanelContent.gameObject.transform
            .Find("Header_container").gameObject.transform
            .Find("Scroll View").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Header").GetComponent<TextMeshProUGUI>().text = header;

        _informationPanelContent.gameObject.transform
            .Find("Header_container").gameObject.transform
            .Find("Scroll View").gameObject.transform
            .Find("Scrollbar Vertical").GetComponent<Scrollbar>().value = 1;
    }

    /// <summary>
    /// Determines whether the currently selected FITS file can be loaded as a 3-D cube.
    /// Also updates _subsetMax_X/Y/Z and the Z-axis dropdown.
    /// </summary>
    private bool IsLoadable()
    {
        List<double> list = new List<double>();
        bool loadable = false;
        string localMsg = "";

        if (_imageNAxis > 2)
        {
            if (_imageNAxis == 3)
            {
                foreach (KeyValuePair<double, double> axes in _axisSize)
                {
                    localMsg += "Axis[" + axes.Key + "]: " + axes.Value + "\n";
                    if (axes.Value > 1)
                    {
                        list.Add(axes.Key);
                        _imageSize *= axes.Value;
                    }
                }

                if (list.Count == 3)
                {
                    loadable = true;
                    _subsetMax_X = (int)_axisSize[list[0]];
                    _subsetMax_Y = (int)_axisSize[list[1]];
                    _subsetMax_Z = (int)_axisSize[list[2]];
                }
            }
            else
            {
                foreach (KeyValuePair<double, double> axes in _axisSize)
                {
                    localMsg += "Axis[" + axes.Key + "]: " + axes.Value + "\n";
                    if (axes.Value > 1)
                    {
                        list.Add(axes.Key);
                        _imageSize *= axes.Value;
                    }
                }

                if (list.Count == 3)
                {
                    loadable = true;
                    _subsetMax_X = (int)_axisSize[list[0]];
                    _subsetMax_Y = (int)_axisSize[list[1]];
                    _subsetMax_Z = (int)_axisSize[list[2]];
                }
                else
                {
                    _informationPanelContent.gameObject.transform
                        .Find("Axes_container").gameObject.SetActive(true);
                }
            }

            // Update Z-axis dropdown
            _zAxisDropdown.interactable = false;
            _zAxisDropdown.ClearOptions();

            foreach (KeyValuePair<double, double> axes in _axisSize)
            {
                if (axes.Value > 1 && axes.Key > 2)
                {
                    _zAxisDropdown.options.Add(
                        new TMP_Dropdown.OptionData() { text = axes.Key.ToString() });
                }
            }

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
                _subsetMax_X = (int)_axisSize[list[0]];
                _subsetMax_Y = (int)_axisSize[list[1]];
                int zAxisIdx;
                Int32.TryParse(_zAxisDropdown.options[_zAxisDropdown.value].text, out zAxisIdx);
                zAxisIdx -= 1;
                Debug.Log("The list has " + list.Count + " items, and the dropdown points to index " + zAxisIdx + "!");
                _subsetMax_Z = (int)_axisSize[list[zAxisIdx]];
            }
        }
        else
        {
            loadable = false;
            localMsg = "Please select a valid cube!";
        }

        return loadable;
    }
}
