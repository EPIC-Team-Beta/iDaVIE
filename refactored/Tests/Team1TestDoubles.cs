// SPDX-License-Identifier: LGPL-3.0-or-later
// Hand-written Team 1 test doubles for public API boundaries.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using iDaVIE.Data;
using iDaVIE.Kernel;
using iDaVIE.Kernel.Contracts;
using iDaVIE.Kernel.Contracts.Persistence;
using iDaVIE.Kernel.Contracts.Plugins;
using iDaVIE.Kernel.Contracts.Types;
using iDaVIE.Rendering.Contracts;

namespace iDaVIE.Tests
{
    public sealed class FakeVolumeDataSet : IVolumeDataSet
    {
        public FakeVolumeDataSet(
            string filePath = "fake.fits",
            int hduIndex = 0,
            VolumeExtents? extents = null,
            IRawVoxelAccess? rawVoxelAccess = null,
            IMaskEditState? maskEditState = null)
        {
            FilePath = filePath;
            HduIndex = hduIndex;
            Extents = extents ?? new VolumeExtents(64, 64, 32);
            SubcubeBounds = SubcubeBounds.FullVolume(Extents);
            RawVoxelAccess = rawVoxelAccess ?? new FakeRawVoxelAccess(Extents.NAxis1, Extents.NAxis2, Extents.NAxis3);
            MaskEditState = maskEditState ?? new FakeMaskEditState(Extents.NAxis1, Extents.NAxis2, Extents.NAxis3);
        }

        public LoadStatus Status { get; set; } = LoadStatus.Loaded;
        public string FilePath { get; set; }
        public int HduIndex { get; set; }
        public VolumeExtents Extents { get; set; }
        public SubcubeBounds SubcubeBounds { get; set; }
        public IReadOnlyDictionary<string, string> HeaderDictionary { get; set; }
            = new Dictionary<string, string>
            {
                ["CUNIT1"] = "deg",
                ["CUNIT2"] = "deg",
                ["CUNIT3"] = "m/s"
            };

        public DataStats Stats { get; set; } = new()
        {
            Min = 0f,
            Max = 1f,
            Mean = 0.5f,
            Rms = 0.1f,
            ZScaleLow = 0f,
            ZScaleHigh = 1f
        };

        public HistogramData Histogram { get; set; } = new()
        {
            RangeMin = 0f,
            RangeMax = 1f,
            Bins = new long[] { 1, 1 }
        };

        public AxisUnits AxisUnits { get; set; } = new("deg", "deg", "m/s");
        public IRawVoxelAccess RawVoxelAccess { get; set; }
        public IMaskEditState MaskEditState { get; set; }

        public DataStats GetStats() => Stats;
        public HistogramData GetHistogram() => Histogram;
        public AxisUnits GetAxisUnits() => AxisUnits;
        public string FormatCoord(CartesianCoord coord) => $"{coord.X},{coord.Y},{coord.Z}";
    }

    public sealed class FakeRawVoxelAccess : IRawVoxelAccess
    {
        private float[] _data;

        public FakeRawVoxelAccess(int sizeX = 64, int sizeY = 64, int sizeZ = 32, IEnumerable<float>? data = null)
        {
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            _data = data?.ToArray() ?? Enumerable.Repeat(0f, sizeX * sizeY * sizeZ).ToArray();
            RefreshDescriptor();
        }

        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }
        public VoxelBufferDescriptor Descriptor { get; private set; } = new();
        public long CurrentGeneration { get; private set; } = 1;

        public void SetData(IEnumerable<float> data)
        {
            _data = data.ToArray();
            CurrentGeneration++;
            RefreshDescriptor();
        }

        public float[] GetSlice(int zIndex)
        {
            if (zIndex < 0 || zIndex >= SizeZ)
                return Array.Empty<float>();

            var slice = new float[SizeX * SizeY];
            Array.Copy(_data, zIndex * slice.Length, slice, 0, Math.Min(slice.Length, _data.Length - zIndex * slice.Length));
            return slice;
        }

        public void GetRegion(int zIndex, int xMin, int xMax, int yMin, int yMax, Span<float> destination)
        {
            var slice = GetSlice(zIndex);
            var cursor = 0;
            for (var y = yMin; y <= yMax && cursor < destination.Length; y++)
            {
                for (var x = xMin; x <= xMax && cursor < destination.Length; x++)
                {
                    if (x >= 0 && x < SizeX && y >= 0 && y < SizeY)
                        destination[cursor++] = slice[y * SizeX + x];
                }
            }
        }

        private void RefreshDescriptor()
        {
            Descriptor = new VoxelBufferDescriptor
            {
                DataPtr = IntPtr.Zero,
                Length = _data.Length,
                SizeX = SizeX,
                SizeY = SizeY,
                SizeZ = SizeZ,
                RegionOffset = default,
                Generation = CurrentGeneration
            };
        }
    }

    public sealed class FakeMaskEditState : IMaskEditState
    {
        private readonly short[] _mask;
        private readonly int _sizeX;
        private readonly int _sizeY;
        private readonly int _sizeZ;

        public FakeMaskEditState(int sizeX = 64, int sizeY = 64, int sizeZ = 32)
        {
            _sizeX = sizeX;
            _sizeY = sizeY;
            _sizeZ = sizeZ;
            _mask = new short[sizeX * sizeY * sizeZ];
        }

        public void SetMaskValue(int x, int y, int z, short value)
        {
            if (InRange(x, y, z))
                _mask[Index(x, y, z)] = value;
        }

        public short GetMaskValue(int x, int y, int z) =>
            InRange(x, y, z) ? _mask[Index(x, y, z)] : (short)0;

        public short[] GetMaskSlice(int axis, int sliceIndex)
        {
            if (axis == 0)
                return Slice(_sizeY, _sizeZ, (u, v) => GetMaskValue(sliceIndex, u, v));
            if (axis == 1)
                return Slice(_sizeX, _sizeZ, (u, v) => GetMaskValue(u, sliceIndex, v));
            return Slice(_sizeX, _sizeY, (u, v) => GetMaskValue(u, v, sliceIndex));
        }

        private static short[] Slice(int width, int height, Func<int, int, short> read)
        {
            var values = new short[width * height];
            var cursor = 0;
            for (var v = 0; v < height; v++)
                for (var u = 0; u < width; u++)
                    values[cursor++] = read(u, v);
            return values;
        }

        private bool InRange(int x, int y, int z) =>
            x >= 0 && x < _sizeX && y >= 0 && y < _sizeY && z >= 0 && z < _sizeZ;

        private int Index(int x, int y, int z) => z * _sizeX * _sizeY + y * _sizeX + x;
    }

    public sealed class FakeVolumeRegistry : IVolumeRegistry
    {
        private readonly List<IVolumeDataSet> _volumes = new();

        public IReadOnlyList<IVolumeDataSet> LoadedVolumes => _volumes.AsReadOnly();
        public IVolumeDataSet? ActiveVolume { get; private set; }
        public IVolumeDataSet Active => ActiveVolume ?? throw new InvalidOperationException("No active volume is registered.");
        public bool HasActive => ActiveVolume != null;

        public event Action ActiveVolumeChanged;
        public event Action Changed;

        public void Add(IVolumeDataSet volume)
        {
            if (!_volumes.Contains(volume))
                _volumes.Add(volume);
            if (ActiveVolume == null)
                ActiveVolume = volume;
            Raise(activeChanged: ReferenceEquals(ActiveVolume, volume));
        }

        public bool Remove(IVolumeDataSet volume)
        {
            var removed = _volumes.Remove(volume);
            if (!removed)
                return false;

            var activeChanged = ReferenceEquals(ActiveVolume, volume);
            if (activeChanged)
                ActiveVolume = _volumes.FirstOrDefault();
            Raise(activeChanged);
            return true;
        }

        public void SetActive(IVolumeDataSet volume)
        {
            if (!_volumes.Contains(volume))
                _volumes.Add(volume);
            if (ReferenceEquals(ActiveVolume, volume))
                return;
            ActiveVolume = volume;
            Raise(activeChanged: true);
        }

        public void ClearActive()
        {
            if (ActiveVolume == null)
                return;
            ActiveVolume = null;
            Raise(activeChanged: true);
        }

        private void Raise(bool activeChanged)
        {
            Changed?.Invoke();
            if (activeChanged)
                ActiveVolumeChanged?.Invoke();
        }
    }

    public sealed class FakeVolumeLoader : IVolumeLoader
    {
        private readonly List<IVolumeDataSet> _loaded = new();

        public IReadOnlyList<IVolumeDataSet> Loaded => _loaded.AsReadOnly();
        public IVolumeDataSet? NextVolume { get; set; }
        public IVolumeDataSet? LastLoadedVolume { get; private set; }
        public SubcubeBounds? LastSubcube { get; private set; }

        public event DatasetLoadedHandler DatasetLoaded;
        public event DatasetUnloadedHandler DatasetUnloaded;
        public event SubcubeChangedHandler SubcubeChanged;

        public Task<IVolumeDataSet> LoadAsync(
            string path,
            int hduIndex = 0,
            SubcubeBounds? initialSubcube = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var volume = NextVolume ?? new FakeVolumeDataSet(path, hduIndex);
            if (initialSubcube.HasValue && volume is FakeVolumeDataSet fake)
                fake.SubcubeBounds = initialSubcube.Value;
            _loaded.Add(volume);
            LastLoadedVolume = volume;
            DatasetLoaded?.Invoke();
            return Task.FromResult(volume);
        }

        public void Unload(IVolumeDataSet volume)
        {
            _loaded.Remove(volume);
            DatasetUnloaded?.Invoke();
        }

        public Task UnloadAsync(IVolumeDataSet volume, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Unload(volume);
            return Task.CompletedTask;
        }

        public Task SetSubcubeAsync(IVolumeDataSet volume, SubcubeBounds newSubcube, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastSubcube = newSubcube;
            if (volume is FakeVolumeDataSet fake)
                fake.SubcubeBounds = newSubcube;
            SubcubeChanged?.Invoke(newSubcube);
            return Task.CompletedTask;
        }

        public async Task LoadAsync(string filePath, int hduIndex) =>
            await LoadAsync(filePath, hduIndex, null).ConfigureAwait(false);

        public Task UnloadAsync()
        {
            if (LastLoadedVolume != null)
                Unload(LastLoadedVolume);
            return Task.CompletedTask;
        }

        public Task SetSubcubeAsync(SubcubeBounds bounds)
        {
            if (LastLoadedVolume == null)
            {
                LastSubcube = bounds;
                SubcubeChanged?.Invoke(bounds);
                return Task.CompletedTask;
            }

            return SetSubcubeAsync(LastLoadedVolume, bounds);
        }
    }

    public sealed class FakeVolumeStateCapture : IVolumeStateCapture
    {
        public VolumeStateDto State { get; set; } = new();
        public VolumeStateDto Capture() => State;
        public void Restore(VolumeStateDto dto) => State = dto;
    }

    public sealed class InMemoryLogSink : ILogSink
    {
        private readonly int _capacity;
        private readonly List<LogEntry> _entries = new();

        public InMemoryLogSink(int capacity = 200)
        {
            _capacity = capacity > 0 ? capacity : 200;
        }

        public IReadOnlyList<LogEntry> RecentEntries => _entries.AsReadOnly();
        public LogLevel MinimumStoredLevel { get; set; } = LogLevel.Trace;

        public event Action<LogEntry> EntryLogged;
        public event Action<LogEntry> EntryAppended;

        public void Log(LogLevel level, string source, string message)
        {
            var entry = new LogEntry(level, source, message);
            if (level >= MinimumStoredLevel)
            {
                _entries.Add(entry);
                if (_entries.Count > _capacity)
                    _entries.RemoveRange(0, _entries.Count - _capacity);
            }

            EntryLogged?.Invoke(entry);
            EntryAppended?.Invoke(entry);
        }

        public void LogInfo(string source, string message) => Log(LogLevel.Info, source, message);
        public void LogWarning(string source, string message) => Log(LogLevel.Warning, source, message);
        public void LogError(string source, string message) => Log(LogLevel.Error, source, message);
        public void Write(LogLevel level, string source, string message) => Log(level, source, message);
    }

    public sealed class FakeConfig : IConfig
    {
        public float DefaultThresholdMin { get; set; } = 0.05f;
        public float DefaultThresholdMax { get; set; } = 0.95f;
        public float DefaultZAxisFactor { get; set; } = 1f;
        public int MaxLoadedVolumes { get; set; } = 4;
        public int DefaultSubcubeSize { get; set; }
        public int LogRingCapacity { get; set; } = 500;
        public string PersistenceRootPath { get; set; } = "Workspaces";
        public int MaxSavedWorkspaces { get; set; } = 20;
        public float DefaultBrushRadius { get; set; } = 3f;
        public int ExpectedPluginAbiMajor { get; set; } = 1;
        public IReadOnlyDictionary<string, string> Extras { get; set; } = new Dictionary<string, string>();
        public int GpuMemoryLimitMb { get; set; } = 384;
        public int MaxRaymarchingSteps { get; set; } = 384;
        public int MaxModeDownsampling { get; set; } = 1;
        public bool FoveatedRendering { get; set; } = true;
        public bool BilinearFiltering { get; set; }
        public string DefaultColorMap { get; set; } = "Plasma";
        public string DefaultScalingType { get; set; } = "Linear";
        public string AngleCoordFormat { get; set; } = "Sexagesimal";
        public string VelocityUnit { get; set; } = "Km";
        public float VoiceCommandConfidenceLevel { get; set; } = 0.3f;
        public bool ImportedFeaturesStartVisible { get; set; } = true;
        public int MomentMapThresholdSteps { get; set; } = 40;
        public float MomentMapStepsPerSecond { get; set; } = 2f;
        public IReadOnlyList<string> Flags { get; set; } = new[] { "-1", "0", "1" };
    }

    public sealed class FakePluginRegistry : IPluginRegistry
    {
        private readonly Dictionary<Type, object> _plugins = new();

        public T GetPlugin<T>() where T : class =>
            TryGetPlugin<T>(out var plugin) ? plugin : throw new PluginNotFoundException(typeof(T));

        public void RegisterPlugin<T>(T plugin) where T : class
        {
            var type = typeof(T);
            if (_plugins.ContainsKey(type))
                throw new InvalidOperationException($"Plugin already registered for {type.FullName}.");
            _plugins[type] = plugin;
        }

        public bool IsRegistered<T>() where T : class => _plugins.ContainsKey(typeof(T));

        public bool TryGetPlugin<T>(out T plugin) where T : class
        {
            if (_plugins.TryGetValue(typeof(T), out var value))
            {
                plugin = (T)value;
                return true;
            }

            plugin = null!;
            return false;
        }
    }

    public sealed class FakeDesktopShell : IDesktopShell
    {
        private readonly Dictionary<PanelHandle, (string Title, object ViewModel, bool Visible)> _panels = new();
        private int _nextId = 1;

        public IReadOnlyDictionary<PanelHandle, (string Title, object ViewModel, bool Visible)> Panels => _panels;

        public PanelHandle RegisterPanel(string title, object viewModel)
        {
            var handle = new PanelHandle(_nextId++);
            _panels[handle] = (title, viewModel, false);
            return handle;
        }

        public void ShowPanel(PanelHandle handle)
        {
            if (_panels.TryGetValue(handle, out var panel))
                _panels[handle] = (panel.Title, panel.ViewModel, true);
        }

        public void HidePanel(PanelHandle handle)
        {
            if (_panels.TryGetValue(handle, out var panel))
                _panels[handle] = (panel.Title, panel.ViewModel, false);
        }

        public void UnregisterPanel(PanelHandle handle) => _panels.Remove(handle);
    }

    public sealed class FakeFitsFileHandle : IFitsFileHandle
    {
        public string FilePath { get; set; } = "fake.fits";
        public int HduIndex { get; set; }
        public int HduCount { get; set; } = 1;
        public bool IsReadWrite { get; set; }
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    public sealed class FakeFitsPlugin : IFitsPlugin
    {
        public string AbiVersion { get; set; } = "1.0.0";
        public IReadOnlyDictionary<string, string> Header { get; set; } = new Dictionary<string, string>();
        public FitsVoxelBuffer FullCube { get; set; } = new() { SizeX = 1, SizeY = 1, SizeZ = 1, Data = new[] { 0f } };
        public short[] LastMaskVoxels { get; private set; } = Array.Empty<short>();
        public CartesianCoord LastMaskOrigin { get; private set; }

        public Task<IFitsFileHandle> OpenAsync(
            string absolutePath,
            int hduIndex = 0,
            FitsOpenMode mode = FitsOpenMode.ReadOnly,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IFitsFileHandle>(new FakeFitsFileHandle
            {
                FilePath = absolutePath,
                HduIndex = hduIndex,
                IsReadWrite = mode == FitsOpenMode.ReadWrite
            });
        }

        public void Close(IFitsFileHandle handle) => handle.Dispose();
        public IReadOnlyDictionary<string, string> ReadHeader(IFitsFileHandle handle) => Header;
        public string ReadRawHeader(IFitsFileHandle handle) => string.Join("\n", Header.Select(x => $"{x.Key}={x.Value}"));
        public void SelectHdu(IFitsFileHandle handle, int hduIndex)
        {
            if (handle is FakeFitsFileHandle fake)
                fake.HduIndex = hduIndex;
        }

        public Task<FitsVoxelBuffer> ReadFullCubeAsync(IFitsFileHandle handle, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FullCube);
        }

        public Task<FitsVoxelBuffer> ReadSubcubeAsync(IFitsFileHandle handle, SubcubeBounds region, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new FitsVoxelBuffer
            {
                Data = FullCube.Data,
                SizeX = region.SizeX,
                SizeY = region.SizeY,
                SizeZ = region.SizeZ,
                RegionOffset = region.Min
            });
        }

        public Task<float[]> ReadSliceAsync(IFitsFileHandle handle, int zSlice, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(FullCube.Data.Take(FullCube.SizeX * FullCube.SizeY).ToArray());
        }

        public void WriteMaskVoxels(IFitsFileHandle handle, ReadOnlySpan<short> values, CartesianCoord origin, int sizeX, int sizeY, int sizeZ)
        {
            LastMaskVoxels = values.ToArray();
            LastMaskOrigin = origin;
        }
    }

    public sealed class FakeWcsPlugin : IWcsPlugin
    {
        public string AbiVersion { get; set; } = "1.0.0";
        public string LastHeader { get; private set; } = string.Empty;
        public IReadOnlyList<string> AltFrames { get; set; } = new[] { "native" };

        public void InitialiseFromHeader(string rawFitsHeader) => LastHeader = rawFitsHeader;
        public (double Longitude, double Latitude, double Spectral) PixelToWorld(CartesianCoord pixel) => (pixel.X, pixel.Y, pixel.Z);
        public CartesianCoord? WorldToPixel(double longitude, double latitude, double spectral) => new((int)longitude, (int)latitude, (int)spectral);

        public void PixelToWorldBulk(ReadOnlySpan<CartesianCoord> pixels, Span<double> longitudes, Span<double> latitudes, Span<double> spectrals)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                longitudes[i] = pixels[i].X;
                latitudes[i] = pixels[i].Y;
                spectrals[i] = pixels[i].Z;
            }
        }

        public IReadOnlyList<string> GetAvailableAltFrames() => AltFrames;
        public double ConvertSpectralValue(double nativeValue, string targetFrame) => nativeValue;
        public double AngularSeparationArcsec(double aLon, double aLat, double bLon, double bLat) => Math.Abs(aLon - bLon) + Math.Abs(aLat - bLat);
        public string FormatAxisValue(int axis, double value) => value.ToString("0.###");
    }

    public sealed class FakeWcsMapping : IWcsMapping
    {
        public IReadOnlyList<string> AvailableAltFrames { get; set; } = new[] { "native" };
        public (double Longitude, double Latitude, double Spectral) PixelToWorld(CartesianCoord pixel) => (pixel.X, pixel.Y, pixel.Z);
        public string FormatAxisValue(int axis, double value) => value.ToString("0.###");
    }

    public sealed class FakeCoordinateTransformer : ICoordinateTransformer
    {
        public WorldCoord Transform(CartesianCoord pixelCoord) =>
            new(pixelCoord.X, pixelCoord.Y, pixelCoord.Z, "voxel");

        public CartesianCoord PixelOf(WorldCoord worldCoord) =>
            new((int)worldCoord.RightAscension, (int)worldCoord.Declination, (int)worldCoord.SpectralValue);
    }

    public sealed class FakeMaskMutationService : IMaskMutationService
    {
        public List<BrushStroke> AppliedBrushes { get; } = new();
        public int FinishStrokeCount { get; private set; }
        public int UndoCount { get; private set; }
        public int RedoCount { get; private set; }
        public bool MaskInitialised { get; private set; }
        public IReadOnlyList<Vector2> LastPolygon { get; private set; } = Array.Empty<Vector2>();
        public MaskMode MaskMode { get; set; }
        public bool DisplayMask { get; set; }
        public short NewSourceId { get; set; } = 1;
        public short CursorSource { get; set; }
        public IReadOnlyList<SourceEntry> MaskedSources { get; set; } = Array.Empty<SourceEntry>();

        public void ApplyBrush(BrushStroke stroke) => AppliedBrushes.Add(stroke);
        public void FinishStroke() => FinishStrokeCount++;
        public void PaintPolygon(int axis, int sliceIndex, IReadOnlyList<Vector2> polygon, PaintConfig config) => LastPolygon = polygon;
        public void Undo() => UndoCount++;
        public void Redo() => RedoCount++;
        public void InitialiseMask() => MaskInitialised = true;
        public int SaveMask(bool overwrite) => overwrite ? 1 : 0;
        public IReadOnlyList<SourceEntry> GetMaskedSources() => MaskedSources;
    }

    public sealed class FakeFitsMaskWriter : IFitsMaskWriter
    {
        public string LastOperation { get; private set; } = string.Empty;
        public string LastPath { get; private set; } = string.Empty;

        public void WriteNew(string cubeFilePath, int hduIndex, string outputPath)
        {
            LastOperation = nameof(WriteNew);
            LastPath = outputPath;
        }

        public void WriteCopy(string sourceMaskPath, string targetPath)
        {
            LastOperation = nameof(WriteCopy);
            LastPath = targetPath;
        }

        public void Overwrite(string maskPath)
        {
            LastOperation = nameof(Overwrite);
            LastPath = maskPath;
        }
    }

    public sealed class FakeFitsCubeReader : IFitsCubeReader
    {
        public SubcubeBounds LastBounds { get; private set; }
        public string LastOutputPath { get; private set; } = string.Empty;

        public void SaveSubCube(string cubeFilePath, SubcubeBounds bounds, string? maskFilePath, string outputPath)
        {
            LastBounds = bounds;
            LastOutputPath = outputPath;
        }
    }

    public sealed class FakeWorkspaceSaveCommand : IWorkspaceSaveCommand
    {
        public int SaveCount { get; private set; }
        public void Save() => SaveCount++;
    }

    public sealed class FakeWorkspaceLoadCommand : IWorkspaceLoadCommand
    {
        public string LastStateId { get; private set; } = string.Empty;
        public void Load(string stateId) => LastStateId = stateId;
    }

    public sealed class FakeBenchmarkHarness : IBenchmarkHarness
    {
        private readonly List<BenchmarkSample> _samples = new();

        public IReadOnlyList<BenchmarkSample> RecentSamples => _samples.AsReadOnly();
        public event Action<BenchmarkSample> SampleCompleted;

        public IDisposable Measure(string name, IReadOnlyDictionary<string, string>? tags = null) =>
            new Scope(() => AddSample(name, tags));

        public BenchmarkSession Start(string name, IReadOnlyDictionary<string, string>? tags = null) => null!;
        public BenchmarkSample Complete(BenchmarkSession session) => AddSample(session?.Name ?? "fake", session?.Tags);

        private BenchmarkSample AddSample(string name, IReadOnlyDictionary<string, string>? tags)
        {
            var now = DateTime.UtcNow;
            var sample = new BenchmarkSample
            {
                Name = name,
                StartedUtc = now,
                CompletedUtc = now,
                Elapsed = TimeSpan.Zero,
                Tags = tags ?? new Dictionary<string, string>()
            };
            _samples.Add(sample);
            SampleCompleted?.Invoke(sample);
            return sample;
        }

        private sealed class Scope : IDisposable
        {
            private readonly Action _onDispose;
            private bool _disposed;

            public Scope(Action onDispose) => _onDispose = onDispose;

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _onDispose();
            }
        }
    }
}
