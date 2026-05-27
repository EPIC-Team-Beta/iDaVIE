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
using System.IO;
using System.Linq;
using DataFeatures;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plain C# class. Owns all sources/mapping panel logic extracted from CanvassDesktop.
/// </summary>
public class SourcesMappingController
{
    // --- Injected dependencies ---
    private readonly GameObject _sourcesPanelContent;
    private readonly GameObject _sourceRowPrefab;
    private readonly CanvassDesktop _shell;

    // --- State ---
    private string _sourcesPath = "";
    private GameObject[] _sourceRowObjects;
    private FeatureMapping _featureMapping;

    public SourcesMappingController(GameObject sourcesPanelContent, GameObject sourceRowPrefab, CanvassDesktop shell)
    {
        _sourcesPanelContent = sourcesPanelContent;
        _sourceRowPrefab     = sourceRowPrefab;
        _shell               = shell;
    }

    // -----------------------------------------------------------------------
    // Sources file browsing
    // -----------------------------------------------------------------------

    public void BrowseSourcesFile()
    {
        string lastPath = PlayerPrefs.GetString("LastPath");
        if (!Directory.Exists(lastPath))
            lastPath = "";
        var extensions = new[]
        {
            new ExtensionFilter("Source Tables", "xml", "fits", "fit"),
            new ExtensionFilter("All Files", "*"),
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", lastPath, extensions, false, (string[] paths) =>
        {
            if (paths.Length == 1)
            {
                PlayerPrefs.SetString("LastPath", Path.GetDirectoryName(paths[0]));
                PlayerPrefs.Save();
                _browseSourcesFile(paths[0]);
            }
        });
    }

    private void _browseSourcesFile(string path)
    {
        var firstActiveRenderer = _shell.GetFirstActiveRenderer();
        var featureDataSet = firstActiveRenderer.GetComponentInChildren<FeatureSetManager>();
        _sourcesPath = path;
        featureDataSet.FeatureFileToLoad = path;

        _sourcesPanelContent.gameObject.transform
            .Find("Lower_container").gameObject.transform
            .Find("MappingSave_container").gameObject.transform
            .Find("Button").GetComponent<Button>().interactable = true;
        _sourcesPanelContent.gameObject.transform
            .Find("Lower_container").gameObject.transform
            .Find("SourcesLoad_container").gameObject.transform
            .Find("Button").GetComponent<Button>().interactable = true;

        _sourcesPanelContent.gameObject.transform
            .Find("SourcesFile_container").gameObject.transform
            .Find("SourcesFilePath_text").GetComponent<TextMeshProUGUI>().text =
            Path.GetFileName(path);

        var featureTable = FeatureTable.GetFeatureTableFromFile(path);

        Transform sourceBody = _sourcesPanelContent.gameObject.transform
            .Find("SourcesInfo_container").gameObject.transform
            .Find("Scroll View").gameObject.transform
            .Find("Viewport").gameObject.transform
            .Find("Content").gameObject.transform;

        if (_sourceRowObjects != null)
        {
            foreach (var row in _sourceRowObjects)
                UnityEngine.Object.Destroy(row);
            _sourceRowObjects = null;
        }

        _sourceRowObjects = new GameObject[featureTable.Columns.Count];
        for (var i = 0; i < featureTable.Columns.Count; i++)
        {
            var row = UnityEngine.Object.Instantiate(_sourceRowPrefab, sourceBody);
            row.transform.Find("Source_number").GetComponent<TextMeshProUGUI>().text = i.ToString();
            string colName = featureTable.Columns.ElementAt(i).Key;
            // Hard coded 17 matching the length available in the UI
            if (colName.Length > 17)
                colName = colName.Substring(0, 14) + "...";
            row.transform.Find("Source_name").GetComponent<TextMeshProUGUI>().text = colName;
            var rowScript = row.GetComponentInParent<SourceRow>();
            rowScript.SourceName  = featureTable.Columns.ElementAt(i).Key;
            rowScript.SourceIndex = i;
            _sourceRowObjects[i]  = row;
        }

        _sourcesPanelContent.gameObject.transform
            .Find("MappingFile_container").gameObject.transform
            .Find("Button").GetComponent<Button>().interactable = true;
    }

    // -----------------------------------------------------------------------
    // Mapping file browsing
    // -----------------------------------------------------------------------

    public void BrowseMappingFile()
    {
        string lastPath = PlayerPrefs.GetString("LastPath");
        if (!Directory.Exists(lastPath))
            lastPath = "";
        var extensions = new[]
        {
            new ExtensionFilter("JSON", "json"),
            new ExtensionFilter("All Files", "*"),
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Open File", lastPath, extensions, false, (string[] paths) =>
        {
            if (paths.Length == 1)
            {
                PlayerPrefs.SetString("LastPath", Path.GetDirectoryName(paths[0]));
                PlayerPrefs.Save();
                _browseMappingFile(paths[0]);
            }
        });
    }

    private void _browseMappingFile(string path)
    {
        _sourcesPanelContent.gameObject.transform
            .Find("MappingFile_container").gameObject.transform
            .Find("MappingFilePath_text").GetComponent<TextMeshProUGUI>().text =
            Path.GetFileName(path);

        _featureMapping = FeatureMapping.GetMappingFromFile(path);

        // Reset all rows first
        foreach (var sourceRowObject in _sourceRowObjects)
        {
            var dropdown = sourceRowObject.transform.Find("Coord_dropdown").gameObject.GetComponent<TMP_Dropdown>();
            dropdown.value = 0;
            sourceRowObject.transform.Find("Import_toggle").gameObject.GetComponent<Toggle>().isOn = false;
        }

        // Apply the mapping from the file
        foreach (var sourceRowObject in _sourceRowObjects)
        {
            try
            {
                var sourceRow = sourceRowObject.GetComponent<SourceRow>();
                var dropdown  = sourceRowObject.transform.Find("Coord_dropdown").gameObject.GetComponent<TMP_Dropdown>();

                if (_featureMapping.Mapping.ImportedColumns.Contains(sourceRow.SourceName))
                    sourceRowObject.transform.Find("Import_toggle").gameObject.GetComponent<Toggle>().isOn = true;

                if (sourceRow.SourceName == _featureMapping.Mapping.ID.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.ID;      dropdown.value = (int)SourceMappingOptions.ID; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.X.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.X;       dropdown.value = (int)SourceMappingOptions.X; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.Y.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Y;       dropdown.value = (int)SourceMappingOptions.Y; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.Z.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Z;       dropdown.value = (int)SourceMappingOptions.Z; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.XMin.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Xmin;    dropdown.value = (int)SourceMappingOptions.Xmin; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.XMax.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Xmax;    dropdown.value = (int)SourceMappingOptions.Xmax; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.YMin.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Ymin;    dropdown.value = (int)SourceMappingOptions.Ymin; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.YMax.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Ymax;    dropdown.value = (int)SourceMappingOptions.Ymax; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.ZMin.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Zmin;    dropdown.value = (int)SourceMappingOptions.Zmin; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.ZMax.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Zmax;    dropdown.value = (int)SourceMappingOptions.Zmax; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.RA.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Ra;      dropdown.value = (int)SourceMappingOptions.Ra; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.Dec.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Dec;     dropdown.value = (int)SourceMappingOptions.Dec; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.Vel.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Velo;    dropdown.value = (int)SourceMappingOptions.Velo; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.Freq.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Freq;    dropdown.value = (int)SourceMappingOptions.Freq; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.Redshift.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Redshift; dropdown.value = (int)SourceMappingOptions.Redshift; }
                else if (sourceRow.SourceName == _featureMapping.Mapping.Flag.Source)
                { sourceRow.CurrentMapping = SourceMappingOptions.Flag;    dropdown.value = (int)SourceMappingOptions.Flag; }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error while loading mapping file. Check that all mappings are included: " + ex.Message);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Mapping file saving
    // -----------------------------------------------------------------------

    public void SaveMappingFile()
    {
        string lastPath = PlayerPrefs.GetString("LastPath");
        if (!Directory.Exists(lastPath))
            lastPath = null;

        var extensionList = new[]
        {
            new ExtensionFilter("JSON", "json"),
        };

        StandaloneFileBrowser.SaveFilePanelAsync("Save File", lastPath, "", extensionList, (string path) =>
        {
            if (path != "")
            {
                PlayerPrefs.SetString("LastPath", Path.GetDirectoryName(path));
                PlayerPrefs.Save();
                _saveMappingFile(path);
            }
        });
    }

    private void _saveMappingFile(string path)
    {
        var mapping         = new Dictionary<SourceMappingOptions, MapEntry>();
        var importedColumns = new List<string>();

        for (int i = 0; i < _sourceRowObjects.Length; i++)
        {
            var row = _sourceRowObjects[i].GetComponent<SourceRow>();
            if (_sourceRowObjects[i].transform.Find("Import_toggle").gameObject.GetComponent<Toggle>().isOn)
                importedColumns.Add(row.SourceName);
            if (row.CurrentMapping != SourceMappingOptions.none)
                mapping.Add(row.CurrentMapping, new MapEntry { Source = row.SourceName });
        }

        var mappingObject = new Mapping
        {
            ID       = mapping.ContainsKey(SourceMappingOptions.ID)       ? mapping[SourceMappingOptions.ID]       : new MapEntry { Source = "" },
            X        = mapping.ContainsKey(SourceMappingOptions.X)        ? mapping[SourceMappingOptions.X]        : new MapEntry { Source = "" },
            Y        = mapping.ContainsKey(SourceMappingOptions.Y)        ? mapping[SourceMappingOptions.Y]        : new MapEntry { Source = "" },
            Z        = mapping.ContainsKey(SourceMappingOptions.Z)        ? mapping[SourceMappingOptions.Z]        : new MapEntry { Source = "" },
            XMin     = mapping.ContainsKey(SourceMappingOptions.Xmin)     ? mapping[SourceMappingOptions.Xmin]     : new MapEntry { Source = "" },
            XMax     = mapping.ContainsKey(SourceMappingOptions.Xmax)     ? mapping[SourceMappingOptions.Xmax]     : new MapEntry { Source = "" },
            YMin     = mapping.ContainsKey(SourceMappingOptions.Ymin)     ? mapping[SourceMappingOptions.Ymin]     : new MapEntry { Source = "" },
            YMax     = mapping.ContainsKey(SourceMappingOptions.Ymax)     ? mapping[SourceMappingOptions.Ymax]     : new MapEntry { Source = "" },
            ZMin     = mapping.ContainsKey(SourceMappingOptions.Zmin)     ? mapping[SourceMappingOptions.Zmin]     : new MapEntry { Source = "" },
            ZMax     = mapping.ContainsKey(SourceMappingOptions.Zmax)     ? mapping[SourceMappingOptions.Zmax]     : new MapEntry { Source = "" },
            RA       = mapping.ContainsKey(SourceMappingOptions.Ra)       ? mapping[SourceMappingOptions.Ra]       : new MapEntry { Source = "" },
            Dec      = mapping.ContainsKey(SourceMappingOptions.Dec)      ? mapping[SourceMappingOptions.Dec]      : new MapEntry { Source = "" },
            Vel      = mapping.ContainsKey(SourceMappingOptions.Velo)     ? mapping[SourceMappingOptions.Velo]     : new MapEntry { Source = "" },
            Freq     = mapping.ContainsKey(SourceMappingOptions.Freq)     ? mapping[SourceMappingOptions.Freq]     : new MapEntry { Source = "" },
            Redshift = mapping.ContainsKey(SourceMappingOptions.Redshift) ? mapping[SourceMappingOptions.Redshift] : new MapEntry { Source = "" },
            Flag     = mapping.ContainsKey(SourceMappingOptions.Flag)     ? mapping[SourceMappingOptions.Flag]     : new MapEntry { Source = "" },
            ImportedColumns = importedColumns.ToArray()
        };

        var featureMappingObject = new FeatureMapping { Mapping = mappingObject };
        featureMappingObject.SaveMappingToFile(path);
    }

    // -----------------------------------------------------------------------
    // Source loading
    // -----------------------------------------------------------------------

    public void LoadSourcesFile()
    {
        var loadingText = _sourcesPanelContent.gameObject.transform
            .Find("Lower_container").gameObject.transform
            .Find("SourcesLoad_container").gameObject.transform
            .Find("Text").gameObject;
        var excludeExternalSources = _sourcesPanelContent.gameObject.transform
            .Find("Lower_container").gameObject.transform
            .Find("SourcesLoad_container").gameObject.transform
            .Find("ExternalSourcesToggle").gameObject.GetComponent<Toggle>().isOn;

        loadingText.GetComponent<TextMeshProUGUI>().color = new Color(0, 0.6f, 0.1f);
        loadingText.SetActive(true);

        bool[] columnsMask = new bool[_sourceRowObjects.Length];

        if (!AreMinimalMappingsSet())
        {
            Debug.Log("Minimal source mappings not set!");
            loadingText.GetComponent<TextMeshProUGUI>().color = Color.red;
            loadingText.GetComponent<TextMeshProUGUI>().text  = "Spatial coordinates not set!";
            return;
        }

        var featureSetManager = _shell.GetFirstActiveRenderer().GetComponentInChildren<FeatureSetManager>();
        var finalMapping = new Dictionary<SourceMappingOptions, string>();

        for (int i = 0; i < _sourceRowObjects.Length; i++)
        {
            var row = _sourceRowObjects[i].GetComponent<SourceRow>();
            if (row.CurrentMapping != SourceMappingOptions.none)
                finalMapping.Add(row.CurrentMapping, row.SourceName);
            columnsMask[i] = _sourceRowObjects[i].transform.Find("Import_toggle").gameObject.GetComponent<Toggle>().isOn;
        }

        if (featureSetManager.FeatureFileToLoad != "")
        {
            featureSetManager.ImportFeatureSetFromTable(
                finalMapping,
                FeatureTable.GetFeatureTableFromFile(_sourcesPath),
                Path.GetFileName(_sourcesPath),
                columnsMask,
                excludeExternalSources);
        }

        loadingText.GetComponent<TextMeshProUGUI>().text =
            $"Successfully loaded sources from:{Environment.NewLine}{Path.GetFileName(_sourcesPath)}";

        _sourcesPanelContent.gameObject.transform
            .Find("Lower_container").gameObject.transform
            .Find("SourcesLoad_container").gameObject.transform
            .Find("Button").GetComponent<Button>().interactable = false;
    }

    // -----------------------------------------------------------------------
    // Source-mapping coordination (called by CanvassDesktop, originally by SourceRow)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When a source row changes its mapping, ensures no other row holds an incompatible mapping.
    /// Called by CanvassDesktop.ChangeSourceMapping which is in turn called by SourceRow.MapCoordInParent.
    /// </summary>
    public void ChangeSourceMapping(int sourceIndex, SourceMappingOptions option)
    {
        for (var i = 0; i < _sourceRowObjects.Length; i++)
        {
            if (i == sourceIndex) continue;
            var sourceRow = _sourceRowObjects[i].GetComponent<SourceRow>();
            if (AreMappingsIncompatible(option, sourceRow.CurrentMapping))
            {
                sourceRow.CurrentMapping = SourceMappingOptions.none;
                _sourceRowObjects[i].transform.Find("Coord_dropdown").gameObject
                    .GetComponent<TMP_Dropdown>().value = 0;
            }
        }
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private bool AreMappingsIncompatible(SourceMappingOptions option1, SourceMappingOptions option2)
    {
        return option1 == option2 ||
               (option1 == SourceMappingOptions.X || option1 == SourceMappingOptions.Y || option1 == SourceMappingOptions.Z) &&
               (option2 == SourceMappingOptions.Ra || option2 == SourceMappingOptions.Dec || option2 == SourceMappingOptions.Velo ||
                option2 == SourceMappingOptions.Freq || option2 == SourceMappingOptions.Redshift) ||
               (option2 == SourceMappingOptions.X || option2 == SourceMappingOptions.Y || option2 == SourceMappingOptions.Z) &&
               (option1 == SourceMappingOptions.Ra || option1 == SourceMappingOptions.Dec || option1 == SourceMappingOptions.Velo ||
                option1 == SourceMappingOptions.Freq || option1 == SourceMappingOptions.Redshift) ||
               option1 == SourceMappingOptions.Velo     && (option2 == SourceMappingOptions.Freq     || option2 == SourceMappingOptions.Redshift) ||
               option1 == SourceMappingOptions.Freq     && (option2 == SourceMappingOptions.Redshift  || option2 == SourceMappingOptions.Velo) ||
               option1 == SourceMappingOptions.Redshift && (option2 == SourceMappingOptions.Freq     || option2 == SourceMappingOptions.Velo);
    }

    private bool AreMinimalMappingsSet()
    {
        var setOptions = new List<SourceMappingOptions>();
        foreach (var row in _sourceRowObjects)
        {
            var currentMapping = row.GetComponent<SourceRow>().CurrentMapping;
            if (currentMapping != SourceMappingOptions.none)
                setOptions.Add(currentMapping);
        }

        bool spatialIsSet =
            setOptions.Contains(SourceMappingOptions.X) && setOptions.Contains(SourceMappingOptions.Y) && setOptions.Contains(SourceMappingOptions.Z) ||
            setOptions.Contains(SourceMappingOptions.Ra) && setOptions.Contains(SourceMappingOptions.Dec) &&
            (setOptions.Contains(SourceMappingOptions.Freq) || setOptions.Contains(SourceMappingOptions.Velo) || setOptions.Contains(SourceMappingOptions.Redshift)) ||
            setOptions.Contains(SourceMappingOptions.Xmin);

        bool boxCornersWork =
            !setOptions.Contains(SourceMappingOptions.Xmin) && !setOptions.Contains(SourceMappingOptions.Xmax) &&
            !setOptions.Contains(SourceMappingOptions.Ymin) && !setOptions.Contains(SourceMappingOptions.Ymax) &&
            !setOptions.Contains(SourceMappingOptions.Zmin) && !setOptions.Contains(SourceMappingOptions.Zmax) ||
            setOptions.Contains(SourceMappingOptions.Xmin) && setOptions.Contains(SourceMappingOptions.Xmax) &&
            setOptions.Contains(SourceMappingOptions.Ymin) && setOptions.Contains(SourceMappingOptions.Ymax) &&
            setOptions.Contains(SourceMappingOptions.Zmin) && setOptions.Contains(SourceMappingOptions.Zmax);

        return spatialIsSet && boxCornersWork;
    }
}
