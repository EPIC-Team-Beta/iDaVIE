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

/// <summary>
/// Plain C# class. Owns the six subset bound input fields, the committed Subset and
/// TrueBounds arrays, and all clamping/validation logic. Knows nothing about FITS files
/// or the broader load pipeline — it only manages what the user has typed.
/// </summary>
public class SubsetBoundsController
{
    private readonly TMP_InputField _xMinInput, _xMaxInput;
    private readonly TMP_InputField _yMinInput, _yMaxInput;
    private readonly TMP_InputField _zMinInput, _zMaxInput;

    private const int AbsoluteMin = 1;
    private int _maxX = 2, _maxY = 2, _maxZ = 2;

    /// <summary>Committed subset bounds [xMin, xMax, yMin, yMax, zMin, zMax] (1-based).</summary>
    public int[] Subset     { get; } = new int[6];

    /// <summary>Full-cube bounds mirroring Subset, set on each Reset call.</summary>
    public int[] TrueBounds { get; } = new int[6];

    public SubsetBoundsController(
        TMP_InputField xMin, TMP_InputField xMax,
        TMP_InputField yMin, TMP_InputField yMax,
        TMP_InputField zMin, TMP_InputField zMax)
    {
        _xMinInput = xMin; _xMaxInput = xMax;
        _yMinInput = yMin; _yMaxInput = yMax;
        _zMinInput = zMin; _zMaxInput = zMax;

        xMin.onEndEdit.AddListener(Validate);
        yMin.onEndEdit.AddListener(Validate);
        zMin.onEndEdit.AddListener(Validate);
        xMax.onEndEdit.AddListener(Validate);
        yMax.onEndEdit.AddListener(Validate);
        zMax.onEndEdit.AddListener(Validate);

        ResetToCurrentBounds();
    }

    /// <summary>
    /// Stores new axis maximums and resets all input fields to the full-cube range.
    /// Call after a new image file is selected.
    /// </summary>
    public void SetBoundsAndReset(int maxX, int maxY, int maxZ)
    {
        _maxX = maxX; _maxY = maxY; _maxZ = maxZ;
        ResetToCurrentBounds();
    }

    /// <summary>
    /// Resets input fields and arrays to the current stored maximums.
    /// </summary>
    public void ResetToCurrentBounds()
    {
        _xMinInput.text = AbsoluteMin.ToString(); _xMaxInput.text = _maxX.ToString();
        _yMinInput.text = AbsoluteMin.ToString(); _yMaxInput.text = _maxY.ToString();
        _zMinInput.text = AbsoluteMin.ToString(); _zMaxInput.text = _maxZ.ToString();

        Subset[0] = TrueBounds[0] = AbsoluteMin; Subset[1] = TrueBounds[1] = _maxX;
        Subset[2] = TrueBounds[2] = AbsoluteMin; Subset[3] = TrueBounds[3] = _maxY;
        Subset[4] = TrueBounds[4] = AbsoluteMin; Subset[5] = TrueBounds[5] = _maxZ;
    }

    /// <summary>
    /// Updates the Z axis maximum when the Z-axis dropdown changes.
    /// Called by FileLoadPanelController after it resolves the new Z size from the axis dict.
    /// </summary>
    public void UpdateZMax(int newMaxZ)
    {
        int oldMaxZ = _maxZ;
        _maxZ = newMaxZ;

        if (int.TryParse(_zMaxInput.text, out int val) &&
            (val < AbsoluteMin || val > _maxZ || val == oldMaxZ))
            _zMaxInput.text = _maxZ.ToString();

        Subset[0] = Subset[2] = Subset[4] = AbsoluteMin;
        Subset[1] = _maxX; Subset[3] = _maxY; Subset[5] = _maxZ;
    }

    /// <summary>
    /// Validates and clamps all six input fields. Registered as onEndEdit on each field;
    /// also callable from outside (e.g. after a programmatic text change).
    /// </summary>
    public void Validate(string _ = "")
    {
        // Max fields: must stay within [committedMin, axisMax].
        ClampMax(_xMaxInput, _maxX, Subset[0]);
        ClampMax(_yMaxInput, _maxY, Subset[2]);
        ClampMax(_zMaxInput, _maxZ, Subset[4]);
        // Min fields: must stay within [AbsoluteMin, committedMax].
        ClampMin(_xMinInput, _maxX, Subset[1]);
        ClampMin(_yMinInput, _maxY, Subset[3]);
        ClampMin(_zMinInput, _maxZ, Subset[5]);

        Subset[0] = int.Parse(_xMinInput.text); Subset[1] = int.Parse(_xMaxInput.text);
        Subset[2] = int.Parse(_yMinInput.text); Subset[3] = int.Parse(_yMaxInput.text);
        Subset[4] = int.Parse(_zMinInput.text); Subset[5] = int.Parse(_zMaxInput.text);
    }

    private static void ClampMax(TMP_InputField field, int max, int committedMin)
    {
        if (!int.TryParse(field.text, out int v)) { field.text = max.ToString(); return; }
        if (v < AbsoluteMin || v < committedMin)    field.text = committedMin.ToString();
        else if (v > max)                           field.text = max.ToString();
    }

    private static void ClampMin(TMP_InputField field, int max, int committedMax)
    {
        if (!int.TryParse(field.text, out int v)) { field.text = AbsoluteMin.ToString(); return; }
        if (v < AbsoluteMin)                        field.text = AbsoluteMin.ToString();
        else if (v > max || v > committedMax)       field.text = committedMax.ToString();
    }
}
