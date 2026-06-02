// SPDX-License-Identifier: LGPL-3.0-or-later
// Minimal stubs for cross-team contract types that are owned by ST1 and ST3
// but are not yet materialised as .cs files in this branch.
// These definitions exist only for the test project to compile; the
// authoritative versions live in shared_interfaces.md and will ship with
// ST1's kernel assembly.

using System;

namespace iDaVIE.Kernel.Contracts.Types
{
    /// <summary>Voxel-space position. Replaces UnityEngine.Vector3 at every team boundary.</summary>
    public readonly record struct CartesianCoord(int X, int Y, int Z);

    /// <summary>Boundary-safe colour value. Replaces UnityEngine.Color at team boundaries.</summary>
    public readonly record struct FeatureColour(float R, float G, float B, float A);
}

namespace iDaVIE.Rendering.Contracts
{
    using iDaVIE.Kernel.Contracts.Types;

    public enum ScalingType  { Linear, Log, Sqrt, Square, Power, Gamma }
    public enum ProjectionMode { MaximumIntensityProjection, AverageIntensityProjection }
    public enum MaskMode     { Disabled, Enabled, Inverted, Isolated }

    /// <summary>Read-only view of the volume render settings. Fires SettingsChanged
    /// once per frame after any mutation; consumers MUST re-read.</summary>
    public interface IRenderSettings
    {
        float          ThresholdMin      { get; }
        float          ThresholdMax      { get; }
        ScalingType    ScalingType       { get; }
        float          ScalingBias       { get; }
        float          ScalingContrast   { get; }
        float          ScalingAlpha      { get; }
        float          ScalingGamma      { get; }
        ColorMapEnum   ColorMap          { get; }
        ProjectionMode ProjectionMode    { get; }
        float          ZAxisFactor       { get; }
        float          ZAxisMinFactor    { get; }
        float          ZAxisMaxFactor    { get; }
        bool           IsFullResolution  { get; }
        int            MaxRayMarchSteps  { get; }
        float          VignetteIntensity { get; }
        float          VignetteFadeStart { get; }
        float          VignetteFadeEnd   { get; }
        FeatureColour  VignetteColor     { get; }
        bool           FoveatedRendering { get; }
        float          FoveationStart    { get; }
        float          FoveationEnd      { get; }
        float          FoveationJitter   { get; }
        int            FoveatedStepsLow  { get; }
        int            FoveatedStepsHigh { get; }
        MaskMode       MaskMode          { get; }
        bool           DisplayMask       { get; }
        float          MaskVoxelSize     { get; }

        /// <summary>Fired once per frame after any mutation; consumers re-read all properties.</summary>
        event Action SettingsChanged;
    }

    /// <summary>Mutation surface for render settings. Intentionally separate from IRenderSettings
    /// (read–write split) so the GUI can hold a mutator without exposing mutation to read-only consumers.</summary>
    public interface IRenderSettingsMutator
    {
        void SetThreshold(float min, float max);
        void ResetThreshold();
        void SetScaling(ScalingType type,
            float bias = 0f, float contrast = 1f, float alpha = 1000f, float gamma = 1f);
        void SetColorMap(ColorMapEnum colorMap);
        void ShiftColorMap(int delta);
        void SetFoveationJitter(float jitter);
        void SetProjection(ProjectionMode mode);
        void SetZAxisFactor(float factor);
        void ResetZAxis();
        void SetMaxRayMarchSteps(int steps);
        void ResetTransform();
    }
}
