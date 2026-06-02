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
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VolumeData;

internal static class PaintSliceTextureHelper
{
    internal static Texture2D GetSlice(Texture3D texture3D, int axis, int sliceIndex,
        float minVal, float maxVal, ColorMapEnum colorMapEnum,
        Texture2D colormap, int colormapWidth, int colorMapRowHeight)
    {
        if (texture3D == null)
        {
            Debug.LogWarning("GetSlice called with null Texture3D");
            return null;
        }

        int width = texture3D.width;
        int height = texture3D.height;
        int depth = texture3D.depth;

        if (sliceIndex < 0 || (axis == 0 && sliceIndex >= width) || (axis == 1 && sliceIndex >= height) || (axis == 2 && sliceIndex >= depth))
        {
            Debug.LogError("Slice index out of range");
            return null;
        }

        NativeArray<float> volumeData = texture3D.GetPixelData<float>(0);

        Texture2D slice;
        Color[] sliceData;
        int size;
        int indexMap = 80 - colorMapEnum.GetHashCode();

        switch (axis)
        {
            case 0:
                slice = new Texture2D(height, depth, TextureFormat.RGBA32, false);
                size = height * depth;
                sliceData = new Color[size];
                for (int y = 0; y < height; y++)
                    for (int z = 0; z < depth; z++)
                    {
                        float n = (volumeData[sliceIndex + y * width + z * width * height] - minVal) / (maxVal - minVal);
                        sliceData[y + z * height] = GetColorFromColormap(colormap, indexMap, n, colormapWidth, colorMapRowHeight);
                    }
                break;

            case 1:
                slice = new Texture2D(width, depth, TextureFormat.RGBA32, false);
                size = width * depth;
                sliceData = new Color[size];
                for (int x = 0; x < width; x++)
                    for (int z = 0; z < depth; z++)
                    {
                        float n = (volumeData[x + sliceIndex * width + z * width * height] - minVal) / (maxVal - minVal);
                        sliceData[x + z * width] = GetColorFromColormap(colormap, indexMap, n, colormapWidth, colorMapRowHeight);
                    }
                break;

            case 2:
                slice = new Texture2D(width, height, TextureFormat.RGBA32, false);
                size = width * height;
                sliceData = new Color[size];
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                    {
                        float n = (volumeData[x + y * width + sliceIndex * width * height] - minVal) / (maxVal - minVal);
                        sliceData[x + y * width] = GetColorFromColormap(colormap, indexMap, n, colormapWidth, colorMapRowHeight);
                    }
                break;

            default:
                Debug.LogError("Invalid axis specified. Use 0 for x-axis, 1 for y-axis, and 2 for z-axis.");
                volumeData.Dispose();
                return null;
        }

        volumeData.Dispose();
        slice.SetPixels(sliceData, 0);
        slice.Apply();
        return slice;
    }

    internal static float[,] GetFloatSlice(Texture3D texture3D, int axis, int sliceIndex)
    {
        if (texture3D == null)
        {
            Debug.LogWarning("GetFloatSlice called with null Texture3D");
            return null;
        }

        int width = texture3D.width;
        int height = texture3D.height;
        int depth = texture3D.depth;

        if (sliceIndex < 0 || (axis == 0 && sliceIndex >= width) || (axis == 1 && sliceIndex >= height) || (axis == 2 && sliceIndex >= depth))
        {
            Debug.LogError("Slice index out of range");
            return null;
        }

        NativeArray<half> volumeData = texture3D.GetPixelData<half>(0);
        float[,] sliceData;

        switch (axis)
        {
            case 0:
                sliceData = new float[height, depth];
                for (int y = 0; y < height; y++)
                    for (int z = 0; z < depth; z++)
                        sliceData[y, z] = (float)volumeData[sliceIndex + y * width + z * width * height];
                break;

            case 1:
                sliceData = new float[width, depth];
                for (int x = 0; x < width; x++)
                    for (int z = 0; z < depth; z++)
                        sliceData[x, z] = (float)volumeData[x + sliceIndex * width + z * width * height];
                break;

            case 2:
                sliceData = new float[width, height];
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        sliceData[x, y] = (float)volumeData[x + y * width + sliceIndex * width * height];
                break;

            default:
                Debug.LogError("Invalid axis specified. Use 0 for x-axis, 1 for y-axis, and 2 for z-axis.");
                volumeData.Dispose();
                return null;
        }

        volumeData.Dispose();
        return sliceData;
    }

    internal static Color GetColorFromColormap(Texture2D colormap, int rowIndex, float value, int colormapWidth, int colorMapRowHeight)
    {
        if (colormap == null)
        {
            Debug.LogError("Colormap texture is not assigned.");
            return Color.black;
        }

        int y = rowIndex * colorMapRowHeight - 1;
        int x = Mathf.FloorToInt(value * (colormapWidth - 1));
        return colormap.GetPixel(x, y);
    }
}
