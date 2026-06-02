// SPDX-License-Identifier: LGPL-3.0-or-later
// WorkspaceService — application-layer orchestrator for workspace save/restore.
// Realises IWorkspaceSaveCommand, IWorkspaceLoadCommand, IStateIndexQuery,
// and IPersistenceEvents (shared_interfaces.md §7).
//
// SRP: owns save/restore orchestration only. Serialisation lives in
// WorkspaceRepository; UI notification is via IPersistenceEvents; state
// capture/restore is delegated to the six injected capture ports.
//
// DIP: all six capture ports are constructor-injected interfaces.
//
// Restore order follows global_model.md §2 acyclic graph (ST1 → ST2 → … → ST6):
// volumes must exist before mask/render/interaction/feature/desktop can restore.
//
// ISP trade-off: WorkspaceService realises all four ST7 interfaces on one class
// because all four share the WorkspaceRepository instance and the event-raise
// logic. Splitting into four classes would recreate the same coupling one level
// up. Documented trade-off per brief §4.2 constraint 1.

using System;
using System.Collections.Generic;
using System.Linq;
using iDaVIE.Data.Contracts;                    // IMaskStateCapture (ST2)
using iDaVIE.Features;                          // IFeatureStateCapture (ST5)
using iDaVIE.Interaction;                       // IInteractionStateCapture (ST4)
using iDaVIE.Kernel.Contracts;                  // ILogSink
using iDaVIE.Kernel.Contracts.Persistence;      // IVolumeStateCapture (ST1)
using iDaVIE.Persistence.Domain;
using iDaVIE.Persistence.Internal;
using iDaVIE.Rendering.Contracts;               // IRenderStateCapture (ST3)
using iDaVIE.UI.Contracts;                      // IDesktopStateCapture (ST6)

namespace iDaVIE.Persistence
{
    internal sealed class WorkspaceService :
        IWorkspaceSaveCommand,
        IWorkspaceLoadCommand,
        IStateIndexQuery,
        IPersistenceEvents
    {
        // ── Injected capture ports (one per team ST1–ST6) ─────────────────────

        private readonly IVolumeStateCapture      _volumeCapture;      // ST1
        private readonly IMaskStateCapture        _maskCapture;        // ST2
        private readonly IRenderStateCapture      _renderCapture;      // ST3
        private readonly IInteractionStateCapture _interactionCapture; // ST4
        private readonly IFeatureStateCapture     _featureCapture;     // ST5
        private readonly IDesktopStateCapture     _desktopCapture;     // ST6

        private readonly WorkspaceRepository _repository;
        private readonly ILogSink            _log;

        // IPersistenceEvents — nullable backing fields; ?.Invoke() is safe with no subscribers.
        public event Action?         SaveStarted;
        public event Action<string>? SaveCompleted;
        public event Action<string>? SaveFailed;
        public event Action?         LoadStarted;
        public event Action?         LoadCompleted;
        public event Action<string>? LoadFailed;

        public WorkspaceService(
            IVolumeStateCapture      volumeCapture,
            IMaskStateCapture        maskCapture,
            IRenderStateCapture      renderCapture,
            IInteractionStateCapture interactionCapture,
            IFeatureStateCapture     featureCapture,
            IDesktopStateCapture     desktopCapture,
            WorkspaceRepository      repository,
            ILogSink                 log)
        {
            _volumeCapture      = volumeCapture      ?? throw new ArgumentNullException(nameof(volumeCapture));
            _maskCapture        = maskCapture        ?? throw new ArgumentNullException(nameof(maskCapture));
            _renderCapture      = renderCapture      ?? throw new ArgumentNullException(nameof(renderCapture));
            _interactionCapture = interactionCapture ?? throw new ArgumentNullException(nameof(interactionCapture));
            _featureCapture     = featureCapture     ?? throw new ArgumentNullException(nameof(featureCapture));
            _desktopCapture     = desktopCapture     ?? throw new ArgumentNullException(nameof(desktopCapture));
            _repository         = repository         ?? throw new ArgumentNullException(nameof(repository));
            _log                = log                ?? throw new ArgumentNullException(nameof(log));
        }

        // ── IWorkspaceSaveCommand ─────────────────────────────────────────────

        public void Save()
        {
            SaveStarted?.Invoke();
            _log.LogInfo(nameof(WorkspaceService), "Save pipeline started.");
            try
            {
                var stateId = Guid.NewGuid().ToString("N");
                var state = new StoredState
                {
                    StateId          = stateId,
                    DisplayName      = $"Workspace {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    SavedAtUtc       = DateTime.UtcNow,
                    VolumeState      = _volumeCapture.Capture(),
                    MaskState        = _maskCapture.Capture(),
                    RenderState      = _renderCapture.Capture(),
                    InteractionState = _interactionCapture.Capture(),
                    FeatureState     = _featureCapture.Capture(),
                    DesktopState     = _desktopCapture.Capture(),
                };

                _repository.Save(state);
                _log.LogInfo(nameof(WorkspaceService), $"Save completed: stateId={stateId}");
                SaveCompleted?.Invoke(stateId);
            }
            catch (Exception ex)
            {
                var msg = $"Save failed: {ex.Message}";
                _log.LogError(nameof(WorkspaceService), msg);
                SaveFailed?.Invoke(msg);
            }
        }

        // ── IWorkspaceLoadCommand ─────────────────────────────────────────────

        public void Load(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId))
            {
                LoadFailed?.Invoke("StateId must not be null or empty.");
                return;
            }

            LoadStarted?.Invoke();
            _log.LogInfo(nameof(WorkspaceService), $"Load pipeline started: stateId={stateId}");
            try
            {
                var state = _repository.Load(stateId);
                if (state == null)
                {
                    var msg = $"Workspace not found or integrity check failed: stateId={stateId}";
                    _log.LogError(nameof(WorkspaceService), msg);
                    LoadFailed?.Invoke(msg);
                    return;
                }

                if (state.VolumeState      != null) _volumeCapture.Restore(state.VolumeState);
                if (state.MaskState        != null) _maskCapture.Restore(state.MaskState);
                if (state.RenderState      != null) _renderCapture.Restore(state.RenderState);
                if (state.InteractionState != null) _interactionCapture.Restore(state.InteractionState);
                if (state.FeatureState     != null) _featureCapture.Restore(state.FeatureState);
                if (state.DesktopState     != null) _desktopCapture.Restore(state.DesktopState);

                _log.LogInfo(nameof(WorkspaceService), $"Load completed: stateId={stateId}");
                LoadCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                var msg = $"Load failed: {ex.Message}";
                _log.LogError(nameof(WorkspaceService), msg);
                LoadFailed?.Invoke(msg);
            }
        }

        // ── IStateIndexQuery ──────────────────────────────────────────────────

        public IReadOnlyList<SavedStateInfo> GetAll() => _repository.GetIndex();

        public IReadOnlyList<SavedStateInfo> Search(string searchTerm)
        {
            var all = _repository.GetIndex();
            if (string.IsNullOrWhiteSpace(searchTerm)) return all;
            return all.Where(s => s.DisplayName.Contains(
                searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Not on the cross-team IStateIndexQuery surface; called by
        // PersistenceMenuController which holds the concrete WorkspaceService type.
        public void Delete(string stateId)
        {
            _repository.Delete(stateId);
            _log.LogInfo(nameof(WorkspaceService), $"Deleted: stateId={stateId}");
        }
    }
}
