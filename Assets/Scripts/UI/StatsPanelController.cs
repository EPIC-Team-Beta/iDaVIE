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
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VolumeData;

/// <summary>
/// Plain C# class. Owns all stats/histogram panel logic extracted from CanvassDesktop.
/// </summary>
public class StatsPanelController
{
    // --- Injected dependencies ---
    private readonly GameObject _statsPanelContent;
    private readonly HistogramHelper _histogramHelper;
    private readonly CanvassDesktop _shell;

    public StatsPanelController(GameObject statsPanelContent, HistogramHelper histogramHelper, CanvassDesktop shell)
    {
        _statsPanelContent = statsPanelContent;
        _histogramHelper   = histogramHelper;
        _shell             = shell;
    }

    // -----------------------------------------------------------------------
    // Post-load wiring
    // -----------------------------------------------------------------------

    /// <summary>
    /// Called by CanvassDesktop.OnVolumeLoaded after a cube finishes loading.
    /// </summary>
    public void OnVolumeLoaded(VolumeDataSetRenderer renderer)
    {
        PopulateStatsValue();
    }

    // -----------------------------------------------------------------------
    // Stats panel
    // -----------------------------------------------------------------------

    /// <summary>
    /// Populates the statistics fields (min, max, std, mean) and generates the initial histogram.
    /// </summary>
    public void PopulateStatsValue()
    {
        var volumeDataSet = _shell.GetFirstActiveRenderer().Data;

        Transform stats = _statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats");

        stats.gameObject.transform.Find("Line_min").gameObject.transform
            .Find("InputField_min").GetComponent<TMP_InputField>().text =
            volumeDataSet.MinValue.ToString();
        stats.gameObject.transform.Find("Line_max").gameObject.transform
            .Find("InputField_max").GetComponent<TMP_InputField>().text =
            volumeDataSet.MaxValue.ToString();
        stats.gameObject.transform.Find("Line_std").gameObject.transform
            .Find("Text_std").GetComponent<TextMeshProUGUI>().text =
            volumeDataSet.StanDev.ToString();
        stats.gameObject.transform.Find("Line_mean").gameObject.transform
            .Find("Text_mean").GetComponent<TextMeshProUGUI>().text =
            volumeDataSet.MeanValue.ToString();

        _histogramHelper.CreateHistogramImg(
            volumeDataSet.Histogram,
            volumeDataSet.HistogramBinWidth,
            volumeDataSet.MinValue,
            volumeDataSet.MaxValue,
            volumeDataSet.MeanValue,
            volumeDataSet.StanDev);
    }

    /// <summary>
    /// Updates the histogram with a new sigma range. optionIndex is the dropdown value (0-based).
    /// </summary>
    public void UpdateSigma(Int32 optionIndex)
    {
        float sigma = optionIndex + 1f;
        float histMin = float.Parse(_statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats").gameObject.transform
            .Find("Line_min").gameObject.transform
            .Find("InputField_min").GetComponent<TMP_InputField>().text);
        float histMax = float.Parse(_statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats").gameObject.transform
            .Find("Line_max").gameObject.transform
            .Find("InputField_max").GetComponent<TMP_InputField>().text);

        var volumeDataSet = _shell.GetFirstActiveRenderer().Data;
        _histogramHelper.CreateHistogramImg(
            volumeDataSet.Histogram,
            volumeDataSet.HistogramBinWidth,
            histMin,
            histMax,
            volumeDataSet.MeanValue,
            volumeDataSet.StanDev,
            sigma);
    }

    /// <summary>
    /// Resets the histogram sigma dropdown, thresholds, and stats display to defaults.
    /// </summary>
    public void RestoreDefaults()
    {
        _statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats").gameObject.transform
            .Find("Line_sigma").gameObject.transform
            .Find("Dropdown").GetComponent<TMP_Dropdown>().value = 0;

        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        VolumeDataSet.UpdateHistogram(firstActiveRenderer.Data, firstActiveRenderer.Data.MinValue, firstActiveRenderer.Data.MaxValue);
        firstActiveRenderer.ResetThresholds();
        PopulateStatsValue();
    }

    /// <summary>
    /// Updates the histogram scale min/max and resets thresholds to match.
    /// </summary>
    public void UpdateScale(float min, float max)
    {
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        VolumeDataSet volumeDataSet = firstActiveRenderer.Data;
        float sigma = _statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats").gameObject.transform
            .Find("Line_sigma").gameObject.transform
            .Find("Dropdown").GetComponent<TMP_Dropdown>().value + 1f;

        firstActiveRenderer.ScaleMin = min;
        firstActiveRenderer.ScaleMax = max;
        VolumeDataSet.UpdateHistogram(volumeDataSet, min, max);
        _histogramHelper.CreateHistogramImg(
            volumeDataSet.Histogram,
            volumeDataSet.HistogramBinWidth,
            min,
            max,
            volumeDataSet.MeanValue,
            volumeDataSet.StanDev,
            sigma);
        firstActiveRenderer.ResetThresholds();
    }

    /// <summary>
    /// Sets the histogram scale to the given min/max percentile pair.
    /// </summary>
    public void SetMaxMinPercentile(float maxPercentile)
    {
        var config = VolumeData.Config.Instance;
        float minPercentileValue, maxPercentileValue;
        var minPercentile = 100 - maxPercentile;
        var dataSet = _shell.GetFirstActiveRenderer().Data;

        if (maxPercentile == 100)
        {
            minPercentileValue = dataSet.MinValue;
            maxPercentileValue = dataSet.MaxValue;
        }
        else if (config.useQuickModeForPercentiles)
        {
            IntPtr histogramPtr = IntPtr.Zero;
            if (dataSet.FullHistogram != null)
            {
                histogramPtr = Marshal.AllocHGlobal(dataSet.FullHistogram.Length * sizeof(int));
                Marshal.Copy(dataSet.FullHistogram, 0, histogramPtr, dataSet.FullHistogram.Length);
            }

            if (DataAnalysis.GetPercentileValuesFromHistogram(histogramPtr, dataSet.FullHistogram.Length,
                    dataSet.MinValue, dataSet.MaxValue, minPercentile,
                    maxPercentile, out minPercentileValue, out maxPercentileValue) != 0)
            {
                Debug.LogError("Error calculating percentiles from histogram.");
            }
            Marshal.FreeHGlobal(histogramPtr);
        }
        else
        {
            if (DataAnalysis.GetPercentileValuesFromData(dataSet.FitsData, dataSet.NumPoints,
                    minPercentile, maxPercentile, out minPercentileValue, out maxPercentileValue) != 0)
            {
                Debug.LogError("Error calculating percentiles from data.");
            }
        }

        Debug.Log("Setting histogram scale min to percentiles: " + minPercentile + "% and " + maxPercentile +
                  "% with values: " + minPercentileValue + " and " + maxPercentileValue + ".");
        UpdateScale(minPercentileValue, maxPercentileValue);
    }

    /// <summary>
    /// Updates the histogram scale minimum using the text from the min input field for the max value.
    /// </summary>
    public void UpdateScaleMin(string minString)
    {
        float max = float.Parse(_statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats").gameObject.transform
            .Find("Line_max").gameObject.transform
            .Find("InputField_max").GetComponent<TMP_InputField>().text);
        UpdateScale(float.Parse(minString), max);
    }

    /// <summary>
    /// Updates the histogram scale maximum using the text from the max input field for the min value.
    /// </summary>
    public void UpdateScaleMax(string maxString)
    {
        float min = float.Parse(_statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats").gameObject.transform
            .Find("Line_min").gameObject.transform
            .Find("InputField_min").GetComponent<TMP_InputField>().text);
        UpdateScale(min, float.Parse(maxString));
    }

    /// <summary>
    /// Updates the histogram image and min/max labels in the stats panel.
    /// Called by CanvassDesktop.UpdateUI() which is in turn called by HistogramHelper.
    /// </summary>
    public void UpdateUI(float min, float max, Sprite img)
    {
        _statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats").gameObject.transform
            .Find("Line_min").gameObject.transform
            .Find("InputField_min").GetComponent<TMP_InputField>().text = min.ToString();

        _statsPanelContent.gameObject.transform
            .Find("Stats_container").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform
            .Find("Stats").gameObject.transform
            .Find("Line_max").gameObject.transform
            .Find("InputField_max").GetComponent<TMP_InputField>().text = max.ToString();

        _statsPanelContent.gameObject.transform
            .Find("Histogram_container").gameObject.transform
            .Find("Histogram").GetComponent<Image>().sprite = img;
    }

    // -----------------------------------------------------------------------
    // Dataset accessors
    // -----------------------------------------------------------------------

    /// <summary>Returns the active VolumeDataSet (image cube data).</summary>
    public VolumeDataSet GetActiveDataSet()
    {
        return _shell.GetFirstActiveRenderer().Data;
    }

    /// <summary>Returns the active mask VolumeDataSet.</summary>
    public VolumeDataSet GetActiveMaskSet()
    {
        return _shell.GetFirstActiveRenderer().Mask;
    }
}
