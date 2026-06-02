// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using System.Collections.Generic;
using iDaVIE.Features;
using iDaVIE.Kernel.Contracts.Types;

namespace iDaVIE.UI.Tests.Fakes
{
    /// <summary>Spy IFeatureSetQuery that records CopyFeatureToUserDefined calls
    /// and lets tests trigger FeatureSetChanged.</summary>
    internal sealed class FakeFeatureSetQuery : IFeatureSetQuery
    {
        public int      CopyFeatureCalls    { get; private set; }
        public IFeature? LastCopiedFeature  { get; private set; }

        public event Action? FeatureSetChanged;

        public IReadOnlyList<IFeatureSet> GetAllFeatureSets() =>
            Array.Empty<IFeatureSet>();

        public IReadOnlyList<IFeatureSet> GetFeatureSetsByType(FeatureSetType type) =>
            Array.Empty<IFeatureSet>();

        public void SetVisible(IFeatureSet featureSet, bool visible) { }
        public void SetDisplayColour(IFeatureSet featureSet, FeatureColour colour) { }
        public void SetFeatureBounds(IFeature feature, CartesianCoord boundsMin, CartesianCoord boundsMax) { }
        public void SetFeatureFlag(IFeature feature, string flag) { }
        public void SetSelectionBoxBounds(CartesianCoord boundsMin, CartesianCoord boundsMax) { }

        public void CopyFeatureToUserDefined(IFeature source)
        {
            CopyFeatureCalls++;
            LastCopiedFeature = source;
        }

        public void FireFeatureSetChanged() => FeatureSetChanged?.Invoke();
    }
}
