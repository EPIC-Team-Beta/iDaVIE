// SPDX-License-Identifier: LGPL-3.0-or-later
using iDaVIE.Rendering.Contracts;

namespace iDaVIE.UI.Tests.Fakes
{
    /// <summary>Spy IRenderSettingsMutator that records which methods were called
    /// so tests can assert ViewModel command delegation.</summary>
    internal sealed class FakeRenderSettingsMutator : IRenderSettingsMutator
    {
        public int  ShiftColorMapCalls { get; private set; }
        public int  LastShiftDelta     { get; private set; }
        public bool ResetThresholdCalled { get; private set; }
        public bool ResetTransformCalled { get; private set; }

        public void ShiftColorMap(int delta)
        {
            ShiftColorMapCalls++;
            LastShiftDelta = delta;
        }

        public void ResetThreshold() => ResetThresholdCalled = true;
        public void ResetTransform() => ResetTransformCalled = true;

        public void SetThreshold(float min, float max) { }
        public void SetScaling(ScalingType type,
            float bias = 0f, float contrast = 1f, float alpha = 1000f, float gamma = 1f) { }
        public void SetColorMap(ColorMapEnum colorMap) { }
        public void SetFoveationJitter(float jitter) { }
        public void SetProjection(ProjectionMode mode) { }
        public void SetZAxisFactor(float factor) { }
        public void ResetZAxis() { }
        public void SetMaxRayMarchSteps(int steps) { }
    }
}
