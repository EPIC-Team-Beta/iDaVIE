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
using System.Collections.Generic;
using UnityEngine;
using VolumeData;

internal static class MaskOverlayBuilder
{
    internal static Texture2D CreateOverlayTexture(Texture2D baseTexture, float[,] overlaySource,
        int width, int height, VolumeDataSet maskSet, int axis, int sliceIndex, short sourceID)
    {
        Texture2D overlayTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                overlayTexture.SetPixel(x, y, baseTexture.GetPixel(x, y));

        bool[,] visited = new bool[width, height];
        List<List<Vector2Int>> regions = new List<List<Vector2Int>>();

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (overlaySource[x, y] > 0 && !visited[x, y])
                {
                    List<Vector2Int> region = new List<Vector2Int>();
                    FindRegion(overlaySource, x, y, visited, region);
                    regions.Add(region);
                }

        Color maskColor = new Color(0.8018868f, 0.5030705f, 0.5030705f);
        foreach (var region in regions)
            DrawOutlineAndGrid(overlayTexture, region, overlaySource, maskColor, maskSet, axis, sliceIndex, sourceID);

        overlayTexture.Apply();
        return overlayTexture;
    }

    internal static void FindRegion(float[,] texture, int startX, int startY, bool[,] visited, List<Vector2Int> region)
    {
        int width = texture.GetLength(0);
        int height = texture.GetLength(1);
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int x = current.x;
            int y = current.y;

            if (x < 0 || x >= width || y < 0 || y >= height || visited[x, y] || texture[x, y] <= 0)
                continue;

            visited[x, y] = true;
            region.Add(current);

            queue.Enqueue(new Vector2Int(x + 1, y));
            queue.Enqueue(new Vector2Int(x - 1, y));
            queue.Enqueue(new Vector2Int(x, y + 1));
            queue.Enqueue(new Vector2Int(x, y - 1));
        }
    }

    internal static void DrawOutlineAndGrid(Texture2D texture, List<Vector2Int> region,
        float[,] overlaySource, Color color, VolumeDataSet maskSet, int axis, int sliceIndex, short sourceID)
    {
        int width = texture.width;
        int height = texture.height;
        HashSet<Vector2Int> borderPixels = new HashSet<Vector2Int>();
        Color currentSourceColor = Color.yellow;

        foreach (var pixel in region)
        {
            int x = pixel.x;
            int y = pixel.y;

            bool isBorder = false;
            if (x > 0 && overlaySource[x - 1, y] == 0) isBorder = true;
            if (x < width - 1 && overlaySource[x + 1, y] == 0) isBorder = true;
            if (y > 0 && overlaySource[x, y - 1] == 0) isBorder = true;
            if (y < height - 1 && overlaySource[x, y + 1] == 0) isBorder = true;

            if (isBorder)
                borderPixels.Add(pixel);
        }

        foreach (var pixel in borderPixels)
        {
            if (axis == 0 && maskSet.GetMaskValue2(sliceIndex, pixel.x, pixel.y) == sourceID)
                color = currentSourceColor;
            if (axis == 1 && maskSet.GetMaskValue2(pixel.x, sliceIndex, pixel.y) == sourceID)
                color = currentSourceColor;
            if (axis == 2 && maskSet.GetMaskValue2(pixel.x, pixel.y, sliceIndex) == sourceID)
                color = currentSourceColor;

            texture.SetPixel(pixel.x, pixel.y, color);
        }
    }
}
