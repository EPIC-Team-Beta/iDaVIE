// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using iDaVIE.Features;
using iDaVIE.Kernel.Contracts.Types;

namespace iDaVIE.UI.Tests.Fakes
{
    /// <summary>Controllable IFeatureSelectionService. Set SelectedFeature before
    /// constructing the ViewModel to pre-load selection state. Use FireSelectionChanged
    /// to simulate the service raising its event after the ViewModel is wired up.</summary>
    internal sealed class FakeFeatureSelectionService : IFeatureSelectionService
    {
        public IFeature?    SelectedFeature    { get; set; }
        public IFeatureSet? SelectedFeatureSet { get; set; }
        public bool         SelectAtCursorResult { get; set; }
        public bool         DeselectCalled       { get; private set; }

        public event Action<IFeature?>? SelectionChanged;

        public bool SelectAtCursor(CartesianCoord cursorVoxelSpace) => SelectAtCursorResult;

        public void SelectFeature(IFeature feature, IFeatureSet owningSet) { }

        public void DeselectFeature() => DeselectCalled = true;

        public void FireSelectionChanged(IFeature? feature) => SelectionChanged?.Invoke(feature);
    }
}
