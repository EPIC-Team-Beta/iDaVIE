// SPDX-License-Identifier: LGPL-3.0-or-later
// IWorkspaceSaveCommand / IWorkspaceLoadCommand — cross-cutting workspace-command
// ports. Triggered by ST4 (voice / quick-menu) and ST6 (desktop save/load
// buttons); realised by ST7's WorkspaceService.
//
// Relocated to ST1 (the kernel floor) from iDaVIE.Persistence to dissolve the
// ST6 -> ST7 and ST4 -> ST7 back-edges that the raw skeleton carried (ST6/ST4
// must not depend on the persistence assembly). This is the same pattern the
// design already applied to ILogSink (M-20) and IDesktopShell (M-26): a port
// that lower layers must invoke lives on the kernel floor so everyone can
// reference it without an upward dependency. The richer ST7 surface
// (IStateIndexQuery, IPersistenceEvents, SavedStateInfo) stays in
// iDaVIE.Persistence — it has no consumer that would create a back-edge.

namespace iDaVIE.Kernel.Contracts
{
    // Consumed by: ST4 (voice/quick-menu trigger), ST6 (desktop save button).
    public interface IWorkspaceSaveCommand
    {
        /// <summary>Fire-and-forget. Outcome reported via IPersistenceEvents.</summary>
        void Save();
    }

    // Consumed by: ST4 (voice restore), ST6 (desktop load button).
    public interface IWorkspaceLoadCommand
    {
        /// <summary>Triggers load by opaque stateId obtained from IStateIndexQuery.</summary>
        void Load(string stateId);
    }
}
