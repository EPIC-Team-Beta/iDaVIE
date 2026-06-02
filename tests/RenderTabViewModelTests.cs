// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using NUnit.Framework;
using iDaVIE.UI;
using iDaVIE.UI.Tests.Fakes;

namespace iDaVIE.UI.Tests
{
    /// <summary>
    /// Unit tests for RenderTabViewModel.
    ///
    /// Test strategy (brief §6.6):
    ///   - Pure C# — no Unity, no scene, no MonoBehaviour.
    ///   - Every dependency is replaced by a Fake (hand-rolled test double).
    ///   - White-box: exercises every public method and event to push towards
    ///     the ≥70% branch coverage target on domain code (brief §7 Testability).
    ///   - Covers: constructor guards, command delegation, event propagation,
    ///     derived property logic, and Dispose cleanup.
    /// </summary>
    [TestFixture]
    public class RenderTabViewModelTests
    {
        private FakeRenderSettings         _settings   = null!;
        private FakeRenderSettingsMutator  _mutator    = null!;
        private FakeRestFrequencyCatalogue _catalogue  = null!;

        [SetUp]
        public void SetUp()
        {
            _settings  = new FakeRenderSettings();
            _mutator   = new FakeRenderSettingsMutator();
            _catalogue = new FakeRestFrequencyCatalogue();
            _catalogue.SetupEntries(new[] { "Default", "HI 21cm", "Custom" });
        }

        // ── Constructor null guards ──────────────────────────────────────────

        [Test]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RenderTabViewModel(null!, _mutator, _catalogue));
        }

        [Test]
        public void Constructor_NullMutator_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RenderTabViewModel(_settings, null!, _catalogue));
        }

        [Test]
        public void Constructor_NullRestFreqs_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new RenderTabViewModel(_settings, _mutator, null!));
        }

        // ── ShiftColorMap ────────────────────────────────────────────────────

        [Test]
        public void ShiftColorMap_DelegatesToMutatorWithCorrectDelta()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            vm.ShiftColorMap(2);
            Assert.That(_mutator.ShiftColorMapCalls, Is.EqualTo(1));
            Assert.That(_mutator.LastShiftDelta, Is.EqualTo(2));
        }

        [Test]
        public void ShiftColorMap_FiresColorMapChangedEvent()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            int fired = 0;
            vm.ColorMapChanged += () => fired++;
            vm.ShiftColorMap(1);
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void ShiftColorMap_NegativeDelta_StillDelegatesAndFires()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            int fired = 0;
            vm.ColorMapChanged += () => fired++;
            vm.ShiftColorMap(-3);
            Assert.That(_mutator.LastShiftDelta, Is.EqualTo(-3));
            Assert.That(fired, Is.EqualTo(1));
        }

        // ── ResetThresholds ──────────────────────────────────────────────────

        [Test]
        public void ResetThresholds_CallsMutatorResetThreshold()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            vm.ResetThresholds();
            Assert.That(_mutator.ResetThresholdCalled, Is.True);
        }

        // ── ResetTransform ───────────────────────────────────────────────────

        [Test]
        public void ResetTransform_CallsMutatorResetTransform()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            vm.ResetTransform();
            Assert.That(_mutator.ResetTransformCalled, Is.True);
        }

        // ── ReportLoadingStep ────────────────────────────────────────────────

        [Test]
        public void ReportLoadingStep_FiresLoadingStepChangedWithCorrectArgs()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            string? capturedLabel = null;
            int capturedStep = -1;
            vm.LoadingStepChanged += (label, step) => { capturedLabel = label; capturedStep = step; };

            vm.ReportLoadingStep("Parsing FITS header", 2);

            Assert.That(capturedLabel, Is.EqualTo("Parsing FITS header"));
            Assert.That(capturedStep, Is.EqualTo(2));
        }

        [Test]
        public void ReportLoadingStep_WithNoSubscribers_DoesNotThrow()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            Assert.DoesNotThrow(() => vm.ReportLoadingStep("Loading textures", 0));
        }

        // ── RaiseStatus ──────────────────────────────────────────────────────

        [Test]
        public void RaiseStatus_FiresStatusRaisedWithMatchingTextAndLevel()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            StatusMessage? captured = null;
            vm.StatusRaised += msg => captured = msg;

            vm.RaiseStatus("File not found", StatusLevel.Error);

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.Value.Text,  Is.EqualTo("File not found"));
            Assert.That(captured.Value.Level, Is.EqualTo(StatusLevel.Error));
        }

        [Test]
        public void RaiseStatus_InfoLevel_FiresWithInfoLevel()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            StatusMessage? captured = null;
            vm.StatusRaised += msg => captured = msg;

            vm.RaiseStatus("Cube loaded", StatusLevel.Info);

            Assert.That(captured!.Value.Level, Is.EqualTo(StatusLevel.Info));
        }

        // ── ReportCropStatus ─────────────────────────────────────────────────

        [Test]
        public void ReportCropStatus_FiresCropStatusChangedWithCorrectDescriptions()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            CropStatus? captured = null;
            vm.CropStatusChanged += s => captured = s;

            vm.ReportCropStatus("100×200×50", "512×512×256");

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured!.Value.CropDescription,       Is.EqualTo("100×200×50"));
            Assert.That(captured.Value.ResolutionDescription, Is.EqualTo("512×512×256"));
        }

        // ── Settings.SettingsChanged propagation ─────────────────────────────

        [Test]
        public void WhenSettingsFiresChanged_ColorMapChangedEventIsRaised()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            int fired = 0;
            vm.ColorMapChanged += () => fired++;

            _settings.FireSettingsChanged();

            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void WhenSettingsFiresChangedTwice_ColorMapChangedFiresTwice()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            int fired = 0;
            vm.ColorMapChanged += () => fired++;

            _settings.FireSettingsChanged();
            _settings.FireSettingsChanged();

            Assert.That(fired, Is.EqualTo(2));
        }

        // ── IsCustomRestFrequencySelected ────────────────────────────────────

        [Test]
        public void IsCustomRestFrequencySelected_WhenSelectedIndexIsLastEntry_ReturnsTrue()
        {
            _catalogue.SelectedIndex = 2; // "Custom" is index 2 (last of 3)
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            Assert.That(vm.IsCustomRestFrequencySelected, Is.True);
        }

        [Test]
        public void IsCustomRestFrequencySelected_WhenSelectedIndexIsNotLastEntry_ReturnsFalse()
        {
            _catalogue.SelectedIndex = 1; // "HI 21cm"
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            Assert.That(vm.IsCustomRestFrequencySelected, Is.False);
        }

        [Test]
        public void IsCustomRestFrequencySelected_WhenFirstEntrySelected_ReturnsFalse()
        {
            _catalogue.SelectedIndex = 0; // "Default"
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            Assert.That(vm.IsCustomRestFrequencySelected, Is.False);
        }

        // ── Dispose ──────────────────────────────────────────────────────────

        [Test]
        public void Dispose_SettingsChangedNoLongerPropagatesColorMapChanged()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            int fired = 0;
            vm.ColorMapChanged += () => fired++;

            vm.Dispose();
            _settings.FireSettingsChanged();

            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_CanBeCalledTwiceWithoutException()
        {
            var vm = new RenderTabViewModel(_settings, _mutator, _catalogue);
            vm.Dispose();
            Assert.DoesNotThrow(() => vm.Dispose());
        }
    }
}
