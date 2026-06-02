// SPDX-License-Identifier: LGPL-3.0-or-later
using System.Collections.Generic;
using iDaVIE.Features;
using iDaVIE.Kernel.Contracts.Types;

namespace iDaVIE.UI.Tests.Fakes
{
    /// <summary>Minimal IFeature implementation for constructing test scenarios.</summary>
    internal sealed class FakeFeature : IFeature
    {
        public int                   OriginId      { get; set; }
        public string                Name          { get; set; } = string.Empty;
        public string                Flag          { get; set; } = string.Empty;
        public CartesianCoord        Center        { get; set; }
        public CartesianCoord        Size          { get; set; }
        public bool                  IsSelected    { get; set; }
        public IReadOnlyList<string> RawDataValues { get; set; } = System.Array.Empty<string>();
        public IFeatureStatistics?   Statistics    { get; set; }
    }
}
