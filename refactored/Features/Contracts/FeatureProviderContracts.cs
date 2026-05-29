// SPDX-License-Identifier: LGPL-3.0-or-later
// iDaVIE (immersive Data Visualisation Interactive Explorer)
// Copyright (C) 2024 IDIA, INAF-OACT — refactor skeleton, design-only.
//
// iDaVIE.Features.Contracts — ST5's driven-port surface: the interfaces that
// ST2 (Data) adapters realise plus the DTOs in their signatures. Extracted into
// a dedicated contracts assembly so the ST2 plug-in adapters can implement them
// (Data -> Features.Contracts) WITHOUT the full iDaVIE.Features domain assembly
// depending back on iDaVIE.Data — i.e. this breaks the Data <-> Features
// dependency cycle the raw skeleton carried, at both the assembly and the
// namespace level. Ownership stays with ST5 (resolution lines 8, 16, 17);
// only the physical home changed.
//
// Moved here verbatim from:
//   • Features/SourceStats.cs          (SourceStats, ISourceStatsProvider, IDataAnalysisPlugin)
//   • Features/FitsTableReader.cs       (IFitsBinaryTableSource — now public)
//   • Features/IFeatureImportExport.cs  (FeatureColumnInfo, FeatureTable)
//
// References only iDaVIE.Kernel.Contracts.Types (CartesianCoord). No UnityEngine.

using System;
using System.Collections.Generic;
using iDaVIE.Kernel.Contracts.Types;     // CartesianCoord

namespace iDaVIE.Features.Contracts
{
    /// <summary>Stats payload POCO. The "// ← x" comments map this field to
    /// the legacy DataAnalysis.SourceStats field that populates it (guidance
    /// for ST2's adapter implementation).</summary>
    public sealed class SourceStats
    {
        public long           VoxelCount           { get; init; }   // ← numVoxels
        public CartesianCoord BoundsMin            { get; init; }   // ← minX/Y/Z
        public CartesianCoord BoundsMax            { get; init; }   // ← maxX/Y/Z
        public double         TotalFlux            { get; init; }   // ← sum
        public double         PeakFlux             { get; init; }   // ← peak
        public CartesianCoord FluxWeightedCentroid { get; init; }   // ← cX/Y/Z
        public double         ChannelW20           { get; init; }
        public double         VeloW20              { get; init; }
        public double         ChannelVsys          { get; init; }
        public double         VeloVsys             { get; init; }
        public IReadOnlyList<double> SpectralProfile { get; init; } = Array.Empty<double>();
        public int            ZStartChannel        { get; init; }
    }

    public interface ISourceStatsProvider
    {
        SourceStats? GetStatsForSource(int originId);
        IReadOnlyDictionary<int, SourceStats> GetAllStats();

        /// <summary>Fired with the source id whose stats were updated (or -1 for bulk reload).</summary>
        event Action<int> SourceStatsUpdated;
    }

    /// <summary>Narrow ST5 design (resolution line 8). ST1's VolumeDataSet uses
    /// GetStats() / GetHistogram() for cube-wide aggregates; this port covers
    /// region-bounded stats used by ST5's spectral profile service and feature
    /// annotation pipeline.</summary>
    public interface IDataAnalysisPlugin
    {
        SourceStats ComputeRegionStats(CartesianCoord boundsMin, CartesianCoord boundsMax);
    }

    public readonly record struct FeatureColumnInfo(string Name, string Unit, string DataType, string Ucd);

    public sealed class FeatureTable
    {
        public IReadOnlyList<FeatureColumnInfo>            Columns { get; init; }
        public IReadOnlyList<IReadOnlyList<string>>        Rows    { get; init; }
    }

    /// <summary>Narrow port over the CFITSIO binary-table surface so ST5's
    /// FitsTableReader can be unit-tested with a fake source. Realised in the ST2
    /// plug-in adapter (wraps the legacy static FitsReader) — see
    /// ST5_domain_design.md §8.1. Made public (was internal) so the ST2 adapter,
    /// in a separate assembly, can implement it.</summary>
    public interface IFitsBinaryTableSource
    {
        IReadOnlyList<FeatureColumnInfo> ReadColumns(string filePath);
        IReadOnlyList<IReadOnlyList<string>> ReadRows(string filePath, int columnCount);
    }
}
