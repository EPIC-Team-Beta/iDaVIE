// SPDX-License-Identifier: LGPL-3.0-or-later
// StoredState — ST7 Domain envelope. Carries state identity + metadata +
// per-team serialised payloads. Immutable once constructed; serialised to disk
// by WorkspaceRepository.

using System;
using iDaVIE.Data.Contracts;                    // MaskStateDto (ST2)
using iDaVIE.Features;                          // FeatureStateDto (ST5)
using iDaVIE.Interaction;                       // InteractionStateDto (ST4)
using iDaVIE.Kernel.Contracts.Persistence;      // VolumeStateDto (ST1)
using iDaVIE.Rendering.Contracts;               // RenderStateDto (ST3)
using iDaVIE.UI.Contracts;                      // DesktopStateDto (ST6)

namespace iDaVIE.Persistence.Domain
{
    public sealed class StoredState
    {
        public int      SchemaVersion { get; init; } = 1;
        public string   StateId       { get; init; } = string.Empty;
        public string   DisplayName   { get; init; } = string.Empty;
        public DateTime SavedAtUtc    { get; init; }

        // Per-team capture DTOs. Null if the sub-system had no state at save time;
        // Restore skips null fields rather than throwing.
        public VolumeStateDto?      VolumeState      { get; init; }  // ST1
        public MaskStateDto?        MaskState        { get; init; }  // ST2
        public RenderStateDto?      RenderState      { get; init; }  // ST3
        public InteractionStateDto? InteractionState { get; init; }  // ST4
        public FeatureStateDto?     FeatureState     { get; init; }  // ST5
        public DesktopStateDto?     DesktopState     { get; init; }  // ST6
    }
}
