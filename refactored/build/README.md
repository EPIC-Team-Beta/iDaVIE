# `build/` — standalone compile of the refactored skeletons for NDepend

Route B from `../info/README.md` ("Compiling for NDepend analysis"): one
hand-written `.csproj` per assembly-map row, files assigned by explicit
`<Compile Include>`, references wired bottom-up along the acyclic graph in
`info/global_model.md §2`. NDepend reads IL/metadata, so the code must *compile*
but need not *run*; every `NotImplementedException` stub is measured normally.

## How to build (on a machine with Unity + NuGet access)

```
dotnet build iDaVIE.sln -m:1 \
  -p:UnityManagedDir="<UnityEditor>/Editor/Data/Managed/UnityEngine" \
  -p:UnityPackagesDir="<iDaVIEproject>/Library/ScriptAssemblies" \
  -p:SteamVrDir="<iDaVIEproject>/Library/ScriptAssemblies"
```

`-m:1` avoids a spurious MSB4006 that the parallel multi-node solution build
raises on shared leaf projects (the graph is acyclic — see below). The three
`-p:` paths supply the managed DLLs the skeletons reference but do not ship
(`UnityEngine*`, `Unity.TextMeshPro`, `Valve.VR[.InteractionSystem]`).
`System.Text.Json` (one call site in `Features/FeatureImportService.cs`) comes
from NuGet. Target framework is `netstandard2.1` (Unity's); `Shims/IsExternalInit.cs`
polyfills the `init`/record support that ns2.1 lacks.

Then point NDepend at `iDaVIE.sln` (or the emitted `iDaVIE.*.dll` set), scope the
application assemblies to `iDaVIE.*`, and mark `UnityEngine*` / `TMPro` /
`Valve.*` / `System.*` as third-party — matching the `T2 Baseline Report.pdf`
methodology so before/after numbers compare.

## 16 assemblies (acyclic build order)

`Kernel.Contracts.Types` → `Kernel.Contracts.Plugins` → `Rendering.Contracts` /
`Features.Contracts` / `Data.Contracts` / `UI.Contracts` → `Kernel.Contracts` →
`Kernel` / `Data` → `Rendering` → `Features` → `Interaction` → `UI` →
`Persistence.Domain` → `Persistence.Application` → `Persistence`.

This is the README's 15-row map **plus `iDaVIE.Features.Contracts`** (see below).

## Local verification status (this repo, no Unity present)

- **Acyclic graph: verified three ways** — topological sort of all
  `ProjectReference` edges, direct build of the top project (`Persistence`, full
  graph via project refs), and `-m:1` solution build all report **0** circular
  dependencies.
- **9/16 assemblies compile clean (0 warnings, 0 errors):** the six `*.Contracts`,
  `Kernel`, and `Data` (Unity-free). This exercises the `init` shim, the
  cross-team reference wiring, and the cycle-breaking relocations below.
- The other 7 fail **only** on absent third-party DLLs (UnityEngine / TMPro /
  Valve / System.Text.Json), i.e. the `-p:` paths above. No code-level errors.

## Source changes made to reach a compiling, acyclic shape

The raw skeleton's `using`-graph was **not** acyclic and had compile blockers.
The following were applied (design decisions — `info/README.md` only flagged the
phantom namespace):

1. **Broke `Data ↔ Features` cycle.** ST2 plug-ins implement ST5-owned provider
   ports while ST5 consumes ST2's `ICoordinateTransformer`. Extracted those
   driven ports + their DTOs (`ISourceStatsProvider`, `IDataAnalysisPlugin`,
   `IFitsBinaryTableSource`, `SourceStats`, `FeatureColumnInfo`, `FeatureTable`)
   into a new ST5 assembly **`iDaVIE.Features.Contracts`** (namespace
   `iDaVIE.Features.Contracts`). ST2 now references the port assembly, not the
   ST5 domain. `IFitsBinaryTableSource` made `public` (was `internal`).
2. **Broke `UI ↔ Persistence` and `Interaction ↔ Persistence` cycles.** Moved the
   two command ports `IWorkspaceSaveCommand` / `IWorkspaceLoadCommand` to the
   kernel floor (`iDaVIE.Kernel.Contracts`, `Kernel/Contracts/IWorkspaceCommands.cs`)
   — the ILogSink (M-20) / IDesktopShell (M-26) relocation pattern — so ST4/ST6
   invoke save/load without depending on ST7.
3. **Phantom namespace.** `using iDaVIE.Kernel.Contracts.Persistence;` (5 files)
   removed/redirected to `iDaVIE.Kernel.Contracts`. Declared the previously-missing
   `SubcubeBoundsDto` as an ST5 DTO in `Features/FeatureStateCapture.cs`.
4. **Split `External/IVolumeDataSet.cs`** into `IVolumeDataSet.cs`
   (`iDaVIE.Kernel.Contracts`) + `IRawVoxelAccess.cs` (`…Plugins`) so each file
   compiles into one assembly.
5. **Build-only:** `Kernel/Delegates.cs` compiles into `iDaVIE.Kernel.Contracts`
   (not `iDaVIE.Kernel`) because contracts (`IVolumeLoader`) consume the
   delegates — a contract may not depend on its implementation assembly.
   `Shims/IsExternalInit.cs` polyfills `init` on netstandard2.1.

Items 1, 2, and the `SubcubeBoundsDto`/Delegates homing are cross-team
ownership decisions taken to make the graph acyclic; surface them with the
owning sub-teams (ST2/ST4/ST5/ST6/ST7) before merging upstream.
