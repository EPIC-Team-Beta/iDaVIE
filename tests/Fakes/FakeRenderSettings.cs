// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using iDaVIE.Kernel.Contracts.Types;
using iDaVIE.Rendering.Contracts;

namespace iDaVIE.UI.Tests.Fakes
{
    /// <summary>Controllable IRenderSettings for unit tests.
    /// Call FireSettingsChanged() to simulate a frame-end coalesced event.</summary>
    internal sealed class FakeRenderSettings : IRenderSettings
    {
        public float          ThresholdMin      { get; set; }
        public float          ThresholdMax      { get; set; }
        public ScalingType    ScalingType       { get; set; }
        public float          ScalingBias       { get; set; }
        public float          ScalingContrast   { get; set; }
        public float          ScalingAlpha      { get; set; }
        public float          ScalingGamma      { get; set; }
        public ColorMapEnum   ColorMap          { get; set; }
        public ProjectionMode ProjectionMode    { get; set; }
        public float          ZAxisFactor       { get; set; }
        public float          ZAxisMinFactor    { get; set; }
        public float          ZAxisMaxFactor    { get; set; }
        public bool           IsFullResolution  { get; set; }
        public int            MaxRayMarchSteps  { get; set; }
        public float          VignetteIntensity { get; set; }
        public float          VignetteFadeStart { get; set; }
        public float          VignetteFadeEnd   { get; set; }
        public FeatureColour  VignetteColor     { get; set; }
        public bool           FoveatedRendering { get; set; }
        public float          FoveationStart    { get; set; }
        public float          FoveationEnd      { get; set; }
        public float          FoveationJitter   { get; set; }
        public int            FoveatedStepsLow  { get; set; }
        public int            FoveatedStepsHigh { get; set; }
        public MaskMode       MaskMode          { get; set; }
        public bool           DisplayMask       { get; set; }
        public float          MaskVoxelSize     { get; set; }

        public event Action? SettingsChanged;

        public void FireSettingsChanged() => SettingsChanged?.Invoke();
    }
}
