// SPDX-License-Identifier: LGPL-3.0-or-later
// Kernel-owned types (ST1) — reproduced here as *reference declarations* so the
// ST3 / ST5 refactor skeletons compile-as-illustrated. The authoritative versions
// live in shared_interfaces.md §1.4 and ship with ST1's kernel assembly.
//
// This file holds the iDaVIE.Kernel.Contracts.Plugins slice (raw voxel sampling
// sub-port + its buffer descriptor), split out of External/IVolumeDataSet.cs so
// each file compiles into exactly one assembly (iDaVIE.Kernel.Contracts.Plugins).

using System;
using iDaVIE.Kernel.Contracts.Types;     // CartesianCoord

namespace iDaVIE.Kernel.Contracts.Plugins
{
    /// <summary>Describes the layout of ST2's unmanaged voxel buffer
    /// (shared_interfaces.md §1.4). Generation MUST be compared against
    /// IRawVoxelAccess.CurrentGeneration before dereferencing DataPtr.</summary>
    public sealed class VoxelBufferDescriptor
    {
        public IntPtr         DataPtr      { get; init; }
        public long           Length       { get; init; }
        public int            SizeX        { get; init; }
        public int            SizeY        { get; init; }
        public int            SizeZ        { get; init; }
        public CartesianCoord RegionOffset { get; init; }
        public long           Generation   { get; init; }
    }

    /// <summary>Raw voxel sampling — sub-port held by IVolumeDataSet (M-27).
    /// ST1's design per resolution line 7 / shared_interfaces.md §1.4.
    /// Pointer is valid only while the volume is loaded; must not be cached
    /// across SetSubcubeAsync.</summary>
    public interface IRawVoxelAccess
    {
        VoxelBufferDescriptor Descriptor { get; }

        /// <summary>Monotonically incremented on every load / subcube / unload.
        /// A descriptor whose Generation differs from this value MUST NOT be
        /// dereferenced; refetch via Descriptor.</summary>
        long CurrentGeneration { get; }

        /// <summary>Copies a single XY slice at spectral channel zIndex to managed memory.</summary>
        float[] GetSlice(int zIndex);

        /// <summary>Copies a rectangular XY region at channel zIndex into destination.</summary>
        void GetRegion(int zIndex, int xMin, int xMax, int yMin, int yMax, Span<float> destination);
    }
}
