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
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using VolumeData;
using iDaVIE.Kernel.Contracts;
using iDaVIE.UI;

/// <summary>
/// Reduced coordinator MonoBehaviour. Implements IDesktopShell (panel registry) and
/// IDesktopStateCapture (save/restore UI state). All business logic is delegated to
/// focused sub-controllers. Public methods are preserved for Unity Inspector button
/// wiring and external callers (HistogramHelper, SourceRow, TabsManager, DesktopPaintController).
/// </summary>
public class CanvassDesktop : MonoBehaviour, IDesktopShell, IDesktopStateCapture
{
    // -----------------------------------------------------------------------
    // Unity serialized fields — all kept for Inspector wiring
    // -----------------------------------------------------------------------
    [SerializeField] public GameObject cubeprefab;
    [SerializeField] public GameObject informationPanelContent;
    [SerializeField] public GameObject renderingPanelContent;
    [SerializeField] public GameObject statsPanelContent;
    [SerializeField] public GameObject sourcesPanelContent;
    [SerializeField] public GameObject mainCanvassDesktop;
    [SerializeField] public GameObject fileLoadCanvassDesktop;
    [SerializeField] public GameObject VolumePlayer;
    [SerializeField] public GameObject SourceRowPrefab;
    [SerializeField] public GameObject WelcomeMenu;
    [SerializeField] public GameObject LoadingText;
    [SerializeField] public TextMeshProUGUI loadTextLabel;
    [SerializeField] public TMP_Text versionText;
    [SerializeField] public GameObject progressBar;
    [SerializeField] public MenuBarBehaviour MenuBarBehaviour;
    [SerializeField] public GameObject vrMapDisplay;
    [SerializeField] public GameObject RegionCubeDisplay;
    [SerializeField] public GameObject PaintSelectionContainer;
    [SerializeField] public GameObject PaintWaitingContainer;
    [SerializeField] public QuickMenuController quickMenuController;
    [SerializeField] public PaintMenuController paintMenuController;
    [SerializeField] public TabsManager _tabsManager;
    [SerializeField] public List<TMP_InputField> inputFields;

    // -----------------------------------------------------------------------
    // Sub-controllers
    // -----------------------------------------------------------------------
    private FileLoadPanelController   _fileLoadController;
    private RenderingPanelController  _renderingController;
    private StatsPanelController      _statsController;
    private SourcesMappingController  _sourcesController;

    // -----------------------------------------------------------------------
    // IDesktopShell panel registry
    // -----------------------------------------------------------------------
    private readonly Dictionary<string, PanelRegistration> _panelRegistry = new();
    private bool _isFileLoadPanelOpen;

    // -----------------------------------------------------------------------
    // Volume renderer tracking
    // -----------------------------------------------------------------------
    private VolumeDataSetRenderer[] _volumeDataSetRenderers;
    private GameObject _volumeDataSetManager;

    // -----------------------------------------------------------------------
    // IDesktopShell events
    // -----------------------------------------------------------------------
    public event Action<string> PanelShown;
    public event Action<string> PanelHidden;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        System.Threading.Thread.CurrentThread.CurrentCulture   = CultureInfo.InvariantCulture;
        System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    void Start()
    {
        CheckCubesDataSet();
        versionText.SetText(Application.version);

        var histogramHelper = FindObjectOfType<HistogramHelper>();

        _fileLoadController = new FileLoadPanelController(
            this,
            GetFirstActiveRenderer,
            CheckCubesDataSet,
            informationPanelContent, fileLoadCanvassDesktop, mainCanvassDesktop,
            LoadingText, loadTextLabel, progressBar,
            cubeprefab, VolumePlayer, WelcomeMenu,
            inputFields, MenuBarBehaviour, quickMenuController);

        _renderingController = new RenderingPanelController(renderingPanelContent, this);
        _statsController     = new StatsPanelController(statsPanelContent, histogramHelper, this);
        _sourcesController   = new SourcesMappingController(sourcesPanelContent, SourceRowPrefab, this);

        _fileLoadController.VolumeLoaded += OnVolumeLoaded;

        _fileLoadController.Initialize();
        _renderingController.Initialize();

        var firstActiveRenderer = GetFirstActiveRenderer();
        if (firstActiveRenderer != null)
            _renderingController.SubscribeToRenderer(firstActiveRenderer);
    }

    private void OnDestroy()
    {
        var firstActiveRenderer = GetFirstActiveRenderer();
        if (firstActiveRenderer != null)
            _renderingController.UnsubscribeFromRenderer(firstActiveRenderer);
    }

    void Update()
    {
        _fileLoadController?.Update();
        _renderingController?.Update();
    }

    void OnGUI()
    {
        _fileLoadController?.DrawPopup();
    }

    // -----------------------------------------------------------------------
    // Post-load orchestration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called when FileLoadPanelController finishes loading a cube.
    /// Coordinates the post-load UI wiring across all sub-controllers.
    /// </summary>
    private void OnVolumeLoaded(VolumeDataSetRenderer renderer)
    {
        CheckCubesDataSet();

        VolumePlayer.SetActive(false);
        VolumePlayer.SetActive(true);

        // Unsubscribe any stale listeners then wire new renderer
        _renderingController.UnsubscribeFromRenderer(renderer);
        _renderingController.OnVolumeLoaded(renderer);
        _statsController.OnVolumeLoaded(renderer);

        LoadingText.gameObject.SetActive(false);
        progressBar.gameObject.SetActive(false);
        WelcomeMenu.gameObject.SetActive(false);

        EnableTabButtons();

        // Navigate to the Stats tab by default after load
        mainCanvassDesktop.gameObject.transform
            .Find("RightPanel/Tabs_ container/Stats_Button")
            .GetComponent<Button>().onClick.Invoke();

        if (MenuBarBehaviour.AboutSection.activeSelf)
            MenuBarBehaviour.ToggleAboutSection();
        if (!MenuBarBehaviour.VRViewDisplay.activeSelf)
            MenuBarBehaviour.ToggleVRViewDisplay();
    }

    private void EnableTabButtons()
    {
        var tabs = mainCanvassDesktop.gameObject.transform.Find("RightPanel/Tabs_ container");
        tabs.Find("Rendering_Button").GetComponent<Button>().interactable = true;
        tabs.Find("Stats_Button").GetComponent<Button>().interactable     = true;
        tabs.Find("Sources_Button").GetComponent<Button>().interactable   = true;
        tabs.Find("Paint_Button").GetComponent<Button>().interactable     = true;
    }

    // -----------------------------------------------------------------------
    // IDesktopShell implementation
    // -----------------------------------------------------------------------

    private sealed class PanelRegistration
    {
        public string         Title;
        public PanelPlacement Placement;
        public Action<object> OnMount;
        public Action         OnUnmount;
        public bool           Visible;
    }

    public void RegisterPanel(string panelId, string title, PanelPlacement placement,
        Action<object> onMount, Action onUnmount)
    {
        _panelRegistry[panelId] = new PanelRegistration
        {
            Title     = title,
            Placement = placement,
            OnMount   = onMount,
            OnUnmount = onUnmount,
            Visible   = false
        };
    }

    public void UnregisterPanel(string panelId)
    {
        if (_panelRegistry.TryGetValue(panelId, out var reg))
        {
            if (reg.Visible) reg.OnUnmount?.Invoke();
            _panelRegistry.Remove(panelId);
        }
    }

    public void ShowPanel(string panelId)
    {
        if (!_panelRegistry.TryGetValue(panelId, out var reg)) return;
        if (reg.Visible) return;
        reg.Visible = true;
        reg.OnMount?.Invoke(transform);
        PanelShown?.Invoke(panelId);
    }

    public void HidePanel(string panelId)
    {
        if (!_panelRegistry.TryGetValue(panelId, out var reg)) return;
        if (!reg.Visible) return;
        reg.Visible = false;
        reg.OnUnmount?.Invoke();
        PanelHidden?.Invoke(panelId);
    }

    public bool IsPanelVisible(string panelId) =>
        _panelRegistry.TryGetValue(panelId, out var r) && r.Visible;

    // -----------------------------------------------------------------------
    // IDesktopStateCapture implementation
    // -----------------------------------------------------------------------

    public DesktopStateDto Capture() => new DesktopStateDto
    {
        ActiveTabName       = _tabsManager != null ? GetActiveTabName() : string.Empty,
        IsFileLoadPanelOpen = _isFileLoadPanelOpen
    };

    public void Restore(DesktopStateDto dto)
    {
        _isFileLoadPanelOpen = dto.IsFileLoadPanelOpen;
        if (_isFileLoadPanelOpen)
        {
            fileLoadCanvassDesktop.SetActive(true);
            mainCanvassDesktop.SetActive(false);
        }
        else
        {
            fileLoadCanvassDesktop.SetActive(false);
            mainCanvassDesktop.SetActive(true);
        }
    }

    private string GetActiveTabName()
    {
        // Derive from TabsManager if needed — field schema open (IR-01)
        return string.Empty;
    }

    // -----------------------------------------------------------------------
    // Volume renderer registry
    // -----------------------------------------------------------------------

    public void CheckCubesDataSet()
    {
        _volumeDataSetManager = GameObject.Find("VolumeDataSetManager");
        _volumeDataSetRenderers = _volumeDataSetManager != null
            ? _volumeDataSetManager.GetComponentsInChildren<VolumeDataSetRenderer>(true)
            : new VolumeDataSetRenderer[0];
    }

    public VolumeDataSetRenderer GetFirstActiveRenderer()
    {
        if (_volumeDataSetRenderers == null) return null;
        foreach (var r in _volumeDataSetRenderers)
            if (r != null && r.isActiveAndEnabled) return r;
        return null;
    }

    // -----------------------------------------------------------------------
    // Public API preserved for external callers
    // -----------------------------------------------------------------------

    // Called by HistogramHelper.CreateHistogramImg
    public void UpdateUI(float min, float max, Sprite img) =>
        _statsController.UpdateUI(min, max, img);

    // Called by SourceRow.MapCoordInParent via GetComponentInParent<CanvassDesktop>()
    public void ChangeSourceMapping(int sourceIndex, SourceMappingOptions option) =>
        _sourcesController.ChangeSourceMapping(sourceIndex, option);

    // Called by FileLoadPanelController's inputFields tab-cycling (Inspector button event)
    public void SetInputIndex(int newIndex) =>
        _fileLoadController.SetInputIndex(newIndex);

    // -----------------------------------------------------------------------
    // Delegated public methods — wired to buttons via Unity Inspector
    // -----------------------------------------------------------------------

    // File load panel
    public void BrowseImageFile()                      => _fileLoadController.BrowseImageFile();
    public void BrowseMaskFile()                       => _fileLoadController.BrowseMaskFile();
    public void CheckImgMaskAxisSize()                 => _fileLoadController.CheckImgMaskAxisSize();
    public void LoadFileFromFileSystem()               => _fileLoadController.LoadFileFromFileSystem();
    public void onSubsetToggleSelected(bool val)       => _fileLoadController.onSubsetToggleSelected(val);
    public void setSubsetBounds()                      => _fileLoadController.setSubsetBounds();
    public void updateSubsetZMax(int v)                => _fileLoadController.updateSubsetZMax(v);
    public void checkSubsetBounds(string v)            => _fileLoadController.checkSubsetBounds(v);
    public void ChangeHduSelection(TMP_Dropdown dd)    => _fileLoadController.ChangeHduSelection(dd);

    // Rendering panel
    public void OnRatioDropdownValueChanged(int i)              => _renderingController.OnRatioDropdownValueChanged(i);
    public void OnRestFrequencyDropdownValueChanged(int i)      => _renderingController.OnRestFrequencyDropdownValueChanged(i);
    public void OnRestFrequencyValueChanged(string v)           => _renderingController.OnRestFrequencyValueChanged(v);
    public void ChangeColorMap()                                => _renderingController.ChangeColorMap();
    public void UpdateThresholdMin(float v)                     => _renderingController.UpdateThresholdMin(v);
    public void UpdateThresholdMax(float v)                     => _renderingController.UpdateThresholdMax(v);
    public void ResetThresholds()                               => _renderingController.ResetThresholds();

    // Stats panel
    public void UpdateSigma(Int32 i)                   => _statsController.UpdateSigma(i);
    public void RestoreDefaults()                      => _statsController.RestoreDefaults();
    public void UpdateScale(float min, float max)      => _statsController.UpdateScale(min, max);
    public void SetMaxMinPercentile(float p)           => _statsController.SetMaxMinPercentile(p);
    public void UpdateScaleMin(string s)               => _statsController.UpdateScaleMin(s);
    public void UpdateScaleMax(string s)               => _statsController.UpdateScaleMax(s);
    public VolumeDataSet getActiveDataSet()            => _statsController.GetActiveDataSet();
    public VolumeDataSet getActiveMaskSet()            => _statsController.GetActiveMaskSet();

    // Sources / mapping panel
    public void BrowseSourcesFile()  => _sourcesController.BrowseSourcesFile();
    public void BrowseMappingFile()  => _sourcesController.BrowseMappingFile();
    public void SaveMappingFile()    => _sourcesController.SaveMappingFile();
    public void LoadSourcesFile()    => _sourcesController.LoadSourcesFile();

    // -----------------------------------------------------------------------
    // Paint coordination — called by TabsManager
    // -----------------------------------------------------------------------

    public void paintTabSelected()
    {
        quickMenuController.OpenPaintMenu();
        vrMapDisplay.SetActive(false);
        RegionCubeDisplay.SetActive(true);
        PaintSelectionContainer.SetActive(true);
        PaintWaitingContainer.SetActive(false);
        RegionCubeDisplay.GetComponent<DesktopPaintController>().StartPaintSelection();
    }

    public void paintTabLeft() => paintMenuController.ExitPaintMode();

    // -----------------------------------------------------------------------
    // Shell navigation
    // -----------------------------------------------------------------------

    public void DismissFileLoad()
    {
        _isFileLoadPanelOpen = false;
        fileLoadCanvassDesktop.SetActive(false);
        mainCanvassDesktop.SetActive(true);
    }

    public void Exit()
    {
        StopAllCoroutines();
        var initOpenVR = !SteamVR.active && !SteamVR.usingNativeSupport;
        if (initOpenVR) OpenVR.Shutdown();
        Application.Quit();
    }
}
