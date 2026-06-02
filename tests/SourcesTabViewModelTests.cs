// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using NUnit.Framework;
using iDaVIE.Features;
using iDaVIE.Kernel.Contracts.Types;
using iDaVIE.UI;
using iDaVIE.UI.Tests.Fakes;

namespace iDaVIE.UI.Tests
{
    /// <summary>
    /// Unit tests for SourcesTabViewModel.
    ///
    /// Test strategy (brief §6.6):
    ///   - Pure C# — no Unity, no scene, no MonoBehaviour.
    ///   - Every dependency is replaced by a Fake (hand-rolled test double).
    ///   - White-box: exercises every public method and event to push towards
    ///     the ≥70% branch coverage target on domain code (brief §7 Testability).
    ///   - Covers: constructor guards, ActiveFeatureSetType guard clause,
    ///     cursor selection branching, AddSelectedFeatureToNewSet null guard,
    ///     event propagation from both upstream services, and Dispose cleanup.
    /// </summary>
    [TestFixture]
    public class SourcesTabViewModelTests
    {
        private FakeFeatureSetQuery         _query     = null!;
        private FakeFeatureSelectionService _selection = null!;

        [SetUp]
        public void SetUp()
        {
            _query     = new FakeFeatureSetQuery();
            _selection = new FakeFeatureSelectionService();
        }

        // ── Constructor null guards ──────────────────────────────────────────

        [Test]
        public void Constructor_NullQuery_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SourcesTabViewModel(null!, _selection));
        }

        [Test]
        public void Constructor_NullSelectionService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SourcesTabViewModel(_query, null!));
        }

        // ── ActiveFeatureSetType ─────────────────────────────────────────────

        [Test]
        public void ActiveFeatureSetType_WhenChangedToNewValue_FiresFeatureListChanged()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            int fired = 0;
            vm.FeatureListChanged += () => fired++;

            vm.ActiveFeatureSetType = FeatureSetType.Mask; // default is Imported

            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void ActiveFeatureSetType_WhenSetToSameValue_DoesNotFireFeatureListChanged()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            // Default is Imported; setting it again must not fire.
            int fired = 0;
            vm.FeatureListChanged += () => fired++;

            vm.ActiveFeatureSetType = FeatureSetType.Imported;

            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void ActiveFeatureSetType_ReflectsTheValueThatWasSet()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            vm.ActiveFeatureSetType = FeatureSetType.UserDefined;
            Assert.That(vm.ActiveFeatureSetType, Is.EqualTo(FeatureSetType.UserDefined));
        }

        // ── SelectFeatureAtCursor ────────────────────────────────────────────

        [Test]
        public void SelectFeatureAtCursor_WhenServiceSucceeds_ReturnsTrueAndFiresFeatureListChanged()
        {
            _selection.SelectAtCursorResult = true;
            var vm = new SourcesTabViewModel(_query, _selection);
            int fired = 0;
            vm.FeatureListChanged += () => fired++;

            bool result = vm.SelectFeatureAtCursor(new CartesianCoord(10, 20, 5));

            Assert.That(result, Is.True);
            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void SelectFeatureAtCursor_WhenServiceFails_ReturnsFalseAndDoesNotFireFeatureListChanged()
        {
            _selection.SelectAtCursorResult = false;
            var vm = new SourcesTabViewModel(_query, _selection);
            int fired = 0;
            vm.FeatureListChanged += () => fired++;

            bool result = vm.SelectFeatureAtCursor(new CartesianCoord(0, 0, 0));

            Assert.That(result, Is.False);
            Assert.That(fired, Is.EqualTo(0));
        }

        // ── AddSelectedFeatureToNewSet ───────────────────────────────────────

        [Test]
        public void AddSelectedFeatureToNewSet_WhenNoSelectedFeature_DoesNotCopyAndDoesNotFire()
        {
            _selection.SelectedFeature = null;
            var vm = new SourcesTabViewModel(_query, _selection);
            int fired = 0;
            vm.FeatureListChanged += () => fired++;

            vm.AddSelectedFeatureToNewSet();

            Assert.That(_query.CopyFeatureCalls, Is.EqualTo(0));
            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void AddSelectedFeatureToNewSet_WithSelectedFeature_CopiesFeatureAndFiresFeatureListChanged()
        {
            var feature = new FakeFeature { OriginId = 42, Name = "HI source" };
            _selection.SelectedFeature = feature;
            var vm = new SourcesTabViewModel(_query, _selection);
            int fired = 0;
            vm.FeatureListChanged += () => fired++;

            vm.AddSelectedFeatureToNewSet();

            Assert.That(_query.CopyFeatureCalls, Is.EqualTo(1));
            Assert.That(_query.LastCopiedFeature, Is.SameAs(feature));
            Assert.That(fired, Is.EqualTo(1));
        }

        // ── DeselectFeature ──────────────────────────────────────────────────

        [Test]
        public void DeselectFeature_DelegatesToSelectionService()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            vm.DeselectFeature();
            Assert.That(_selection.DeselectCalled, Is.True);
        }

        // ── Delegated properties ─────────────────────────────────────────────

        [Test]
        public void SelectedFeature_ReflectsCurrentSelectionServiceState()
        {
            var feature = new FakeFeature { OriginId = 7 };
            _selection.SelectedFeature = feature;
            var vm = new SourcesTabViewModel(_query, _selection);
            Assert.That(vm.SelectedFeature, Is.SameAs(feature));
        }

        [Test]
        public void SelectedFeature_WhenNullInService_ReturnsNull()
        {
            _selection.SelectedFeature = null;
            var vm = new SourcesTabViewModel(_query, _selection);
            Assert.That(vm.SelectedFeature, Is.Null);
        }

        // ── Upstream event propagation ───────────────────────────────────────

        [Test]
        public void WhenQueryFiresFeatureSetChanged_FeatureListChangedIsRaised()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            int fired = 0;
            vm.FeatureListChanged += () => fired++;

            _query.FireFeatureSetChanged();

            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void WhenSelectionServiceFiresSelectionChanged_SelectedFeatureChangedCarriesFeature()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            var feature = new FakeFeature { OriginId = 99 };
            IFeature? captured = null;
            vm.SelectedFeatureChanged += f => captured = f;

            _selection.FireSelectionChanged(feature);

            Assert.That(captured, Is.SameAs(feature));
        }

        [Test]
        public void WhenSelectionServiceFiresSelectionChangedWithNull_SelectedFeatureChangedReceivesNull()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            bool eventFired = false;
            IFeature? captured = new FakeFeature(); // sentinel — should be overwritten with null
            vm.SelectedFeatureChanged += f => { captured = f; eventFired = true; };

            _selection.FireSelectionChanged(null);

            Assert.That(eventFired, Is.True);
            Assert.That(captured, Is.Null);
        }

        // ── Dispose ──────────────────────────────────────────────────────────

        [Test]
        public void Dispose_QueryFeatureSetChangedNoLongerPropagatesFeatureListChanged()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            int fired = 0;
            vm.FeatureListChanged += () => fired++;

            vm.Dispose();
            _query.FireFeatureSetChanged();

            Assert.That(fired, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_SelectionChangedNoLongerPropagatesSelectedFeatureChanged()
        {
            var vm = new SourcesTabViewModel(_query, _selection);
            int fired = 0;
            vm.SelectedFeatureChanged += _ => fired++;

            vm.Dispose();
            _selection.FireSelectionChanged(null);

            Assert.That(fired, Is.EqualTo(0));
        }
    }
}
