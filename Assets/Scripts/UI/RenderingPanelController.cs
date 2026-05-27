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
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VolumeData;

/// <summary>
/// Plain C# class. Owns all rendering-panel UI logic extracted from CanvassDesktop.
/// </summary>
public class RenderingPanelController
{
    // --- Injected dependencies ---
    private readonly GameObject _renderingPanelContent;
    private readonly CanvassDesktop _shell;

    // --- UI references (resolved in Initialize) ---
    private Slider _minThreshold;
    private TextMeshProUGUI _minThresholdLabel;
    private Slider _maxThreshold;
    private TextMeshProUGUI _maxThresholdLabel;

    // --- State ---
    private ColorMapEnum _activeColorMap = ColorMapEnum.None;

    public RenderingPanelController(GameObject renderingPanelContent, CanvassDesktop shell)
    {
        _renderingPanelContent = renderingPanelContent;
        _shell                 = shell;
    }

    /// <summary>
    /// Must be called from CanvassDesktop.Start() after all GameObjects are wired.
    /// </summary>
    public void Initialize()
    {
        _minThreshold = _renderingPanelContent.gameObject.transform
            .Find("Rendering_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Settings").gameObject.transform
            .Find("Threshold_container").gameObject.transform
            .Find("Threshold_min").gameObject.transform
            .Find("Slider").GetComponent<Slider>();

        _minThresholdLabel = _renderingPanelContent.gameObject.transform
            .Find("Rendering_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Settings").gameObject.transform
            .Find("Threshold_container").gameObject.transform
            .Find("Threshold_min").gameObject.transform
            .Find("Min_label").GetComponent<TextMeshProUGUI>();

        _maxThreshold = _renderingPanelContent.gameObject.transform
            .Find("Rendering_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Settings").gameObject.transform
            .Find("Threshold_container").gameObject.transform
            .Find("Threshold_max").gameObject.transform
            .Find("Slider").GetComponent<Slider>();

        _maxThresholdLabel = _renderingPanelContent.gameObject.transform
            .Find("Rendering_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Settings").gameObject.transform
            .Find("Threshold_container").gameObject.transform
            .Find("Threshold_max").gameObject.transform
            .Find("Max_label").GetComponent<TextMeshProUGUI>();
    }

    // -----------------------------------------------------------------------
    // Update — called each frame from CanvassDesktop.Update()
    // -----------------------------------------------------------------------

    /// <summary>
    /// Syncs threshold sliders and colormap dropdown with the active renderer each frame.
    /// Call from CanvassDesktop.Update().
    /// </summary>
    public void Update()
    {
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        if (firstActiveRenderer == null) return;

        if (_minThreshold.value > _maxThreshold.value)
            _minThreshold.value = _maxThreshold.value;

        var effectiveMin = firstActiveRenderer.ScaleMin +
            firstActiveRenderer.ThresholdMin * (firstActiveRenderer.ScaleMax - firstActiveRenderer.ScaleMin);
        var effectiveMax = firstActiveRenderer.ScaleMin +
            firstActiveRenderer.ThresholdMax * (firstActiveRenderer.ScaleMax - firstActiveRenderer.ScaleMin);
        _minThresholdLabel.text = effectiveMin.ToString();
        _maxThresholdLabel.text = effectiveMax.ToString();

        if (firstActiveRenderer.ThresholdMin != _minThreshold.value)
            _minThreshold.value = firstActiveRenderer.ThresholdMin;

        if (firstActiveRenderer.ThresholdMax != _maxThreshold.value)
            _maxThreshold.value = firstActiveRenderer.ThresholdMax;

        if (firstActiveRenderer.ColorMap != _activeColorMap)
        {
            _renderingPanelContent.gameObject.transform
                .Find("Rendering_container").gameObject.transform
                .Find("Viewport").gameObject.transform
                .Find("Content").gameObject.transform
                .Find("Settings").gameObject.transform
                .Find("Colormap_container").gameObject.transform
                .Find("Dropdown_colormap").GetComponent<TMP_Dropdown>().value =
                (int)firstActiveRenderer.ColorMap;
        }
    }

    // -----------------------------------------------------------------------
    // Rest frequency
    // -----------------------------------------------------------------------

    /// <summary>
    /// Populates the rest-frequency dropdown from the active renderer's frequency list.
    /// </summary>
    public void PopulateRestfreqencyDropdown()
    {
        var renderingFreqsDropdown = _renderingPanelContent.transform
            .Find("Rendering_container/Viewport/Content/Settings/RestFreq_container/RestFreq_dropdown")
            .GetComponent<TMP_Dropdown>();
        renderingFreqsDropdown.ClearOptions();
        foreach (var freq in _shell.GetFirstActiveRenderer().RestFrequencyGHzList.Keys)
            renderingFreqsDropdown.options.Add(new TMP_Dropdown.OptionData(freq));
    }

    /// <summary>
    /// Event handler: when the renderer's rest-frequency list index changes, sync the dropdown.
    /// </summary>
    private void OnRestFrequencyIndexOfDatasetChanged()
    {
        SetRestFrequencyDropdown(_shell.GetFirstActiveRenderer().RestFrequencyGHzListIndex);
    }

    /// <summary>
    /// Event handler: when the renderer's rest frequency value changes, sync the input field.
    /// </summary>
    private void OnRestFrequencyOfDatasetChanged()
    {
        SetRestFrequencyInputField(_shell.GetFirstActiveRenderer().RestFrequencyGHz);
    }

    /// <summary>
    /// Called by the dropdown UI: updates the renderer's rest-frequency index.
    /// </summary>
    public void OnRestFrequencyDropdownValueChanged(int optionIndex)
    {
        _shell.GetFirstActiveRenderer().RestFrequencyGHzListIndex = optionIndex;
    }

    /// <summary>
    /// Called by the input-field UI: updates the renderer's custom rest frequency.
    /// </summary>
    public void OnRestFrequencyValueChanged(string val)
    {
        var newRestFrequencyGHz = double.Parse(val);
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        firstActiveRenderer.RestFrequencyGHzList["Custom"] = newRestFrequencyGHz;
        if (firstActiveRenderer.OverrideRestFrequency)
            firstActiveRenderer.RestFrequencyGHz = newRestFrequencyGHz;
    }

    private void SetRestFrequencyInputInteractable(bool isInteractable)
    {
        _renderingPanelContent.transform
            .Find("Rendering_container/Viewport/Content/Settings/RestFreq_container/RestFreq_input")
            .GetComponent<TMP_InputField>().interactable = isInteractable;
    }

    private void SetRestFrequencyInputField(double restFrequency)
    {
        _renderingPanelContent.transform
            .Find("Rendering_container/Viewport/Content/Settings/RestFreq_container/RestFreq_input")
            .GetComponent<TMP_InputField>().text = restFrequency.ToString();
    }

    private void SetRestFrequencyDropdown(int index)
    {
        _renderingPanelContent.transform
            .Find("Rendering_container/Viewport/Content/Settings/RestFreq_container/RestFreq_dropdown")
            .GetComponent<TMP_Dropdown>().value = index;

        // Default (index 0): disable custom input, use cube's default
        if (index == 0)
        {
            SetRestFrequencyInputInteractable(false);
        }
        // Custom (last index): enable custom input
        else if (index == _shell.GetFirstActiveRenderer().RestFrequencyGHzList.Count - 1)
        {
            SetRestFrequencyInputInteractable(true);
        }
        // Config-file entry: disable custom input
        else
        {
            SetRestFrequencyInputInteractable(false);
        }
    }

    // -----------------------------------------------------------------------
    // Ratio dropdown
    // -----------------------------------------------------------------------

    /// <summary>
    /// Updates the Z scale of the active renderer when the ratio dropdown changes.
    /// </summary>
    public void OnRatioDropdownValueChanged(int optionIndex)
    {
        // Stored in FileLoadPanelController as _ratioDropdownIndex; also applied to the live renderer here.
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        if (firstActiveRenderer != null)
        {
            if (optionIndex == 0)
            {
                // X=Y=Z
                firstActiveRenderer.ZScale = firstActiveRenderer.XScale;
            }
            else
            {
                // X=Y
                firstActiveRenderer.ZScale =
                    firstActiveRenderer.XScale *
                    firstActiveRenderer.GetCubeDimensions().z /
                    firstActiveRenderer.GetCubeDimensions().x;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Color map
    // -----------------------------------------------------------------------

    /// <summary>
    /// Populates the colour-map dropdown with all ColorMapEnum values.
    /// </summary>
    public void PopulateColorMapDropdown()
    {
        _renderingPanelContent.gameObject.transform
            .Find("Rendering_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Settings").gameObject.transform
            .Find("Colormap_container").gameObject.transform
            .Find("Dropdown_colormap").GetComponent<TMP_Dropdown>().options.Clear();

        foreach (var colorMap in Enum.GetValues(typeof(ColorMapEnum)))
        {
            _renderingPanelContent.gameObject.transform
                .Find("Rendering_container").gameObject.transform
                .Find("Viewport").gameObject.transform
                .Find("Content").gameObject.transform
                .Find("Settings").gameObject.transform
                .Find("Colormap_container").gameObject.transform
                .Find("Dropdown_colormap").GetComponent<TMP_Dropdown>().options
                .Add(new TMP_Dropdown.OptionData() { text = colorMap.ToString() });
        }

        _renderingPanelContent.gameObject.transform
            .Find("Rendering_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Settings").gameObject.transform
            .Find("Colormap_container").gameObject.transform
            .Find("Dropdown_colormap").GetComponent<TMP_Dropdown>().value =
            VolumeData.Config.Instance.defaultColorMap.GetHashCode();
    }

    /// <summary>
    /// Applies the currently selected colour map from the dropdown to the active renderer.
    /// </summary>
    public void ChangeColorMap()
    {
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        if (firstActiveRenderer != null)
        {
            _activeColorMap = ColorMapUtils.FromHashCode(
                _renderingPanelContent.gameObject.transform
                    .Find("Rendering_container").gameObject.transform
                    .Find("Viewport").gameObject.transform
                    .Find("Content").gameObject.transform
                    .Find("Settings").gameObject.transform
                    .Find("Colormap_container").gameObject.transform
                    .Find("Dropdown_colormap").GetComponent<TMP_Dropdown>().value);
            firstActiveRenderer.ColorMap = _activeColorMap;
        }
    }

    // -----------------------------------------------------------------------
    // Thresholds
    // -----------------------------------------------------------------------

    public void UpdateThresholdMin(float value)
    {
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        if (firstActiveRenderer != null)
            firstActiveRenderer.ThresholdMin = Mathf.Clamp(value, 0, firstActiveRenderer.ThresholdMax);
    }

    public void UpdateThresholdMax(float value)
    {
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        if (firstActiveRenderer != null)
            firstActiveRenderer.ThresholdMax = Mathf.Clamp(value, firstActiveRenderer.ThresholdMin, 1);
    }

    public void ResetThresholds()
    {
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        if (firstActiveRenderer != null)
        {
            firstActiveRenderer.ThresholdMin = firstActiveRenderer.InitialThresholdMin;
            _minThreshold.value = firstActiveRenderer.ThresholdMin;

            firstActiveRenderer.ThresholdMax = firstActiveRenderer.InitialThresholdMax;
            _maxThreshold.value = firstActiveRenderer.ThresholdMax;
        }
    }

    // -----------------------------------------------------------------------
    // Post-load wiring
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called by CanvassDesktop.OnVolumeLoaded after a cube finishes loading.
    /// Populates dropdowns, subscribes to renderer events, and resets rest-frequency UI.
    /// </summary>
    public void OnVolumeLoaded(VolumeDataSetRenderer renderer)
    {
        // Update mask dropdown interactability
        _renderingPanelContent.gameObject.transform
            .Find("Rendering_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Settings").gameObject.transform
            .Find("Mask_container").gameObject.transform
            .Find("Dropdown_mask").GetComponent<TMP_Dropdown>().interactable =
            renderer.MaskFileName != "";

        PopulateColorMapDropdown();
        PopulateRestfreqencyDropdown();
        SetRestFrequencyInputInteractable(false);
        SetRestFrequencyInputField((float)renderer.RestFrequencyGHz);

        SubscribeToRenderer(renderer);
    }

    /// <summary>
    /// Subscribes to the given renderer's rest-frequency change events.
    /// </summary>
    public void SubscribeToRenderer(VolumeDataSetRenderer renderer)
    {
        if (renderer == null) return;
        renderer.RestFrequencyGHzListIndexChanged += OnRestFrequencyIndexOfDatasetChanged;
        renderer.RestFrequencyGHzChanged          += OnRestFrequencyOfDatasetChanged;
    }

    /// <summary>
    /// Unsubscribes from the given renderer's rest-frequency change events.
    /// </summary>
    public void UnsubscribeFromRenderer(VolumeDataSetRenderer renderer)
    {
        if (renderer == null) return;
        renderer.RestFrequencyGHzListIndexChanged -= OnRestFrequencyIndexOfDatasetChanged;
        renderer.RestFrequencyGHzChanged          -= OnRestFrequencyOfDatasetChanged;
    }
}
