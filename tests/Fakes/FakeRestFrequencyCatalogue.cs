// SPDX-License-Identifier: LGPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using iDaVIE.Rendering;

namespace iDaVIE.UI.Tests.Fakes
{
    /// <summary>Controllable IRestFrequencyCatalogue for unit tests.
    /// Call SetupEntries to populate the dropdown list and FireSelectionChanged
    /// to simulate the user picking an entry.</summary>
    internal sealed class FakeRestFrequencyCatalogue : IRestFrequencyCatalogue
    {
        private List<RestFrequencyEntry> _entries = new();

        public IReadOnlyList<RestFrequencyEntry> Entries => _entries;
        public int    SelectedIndex        { get; set; }
        public double CurrentFrequencyGHz  { get; set; }

        public event Action? SelectionChanged;

        public void SetupEntries(IEnumerable<string> labels)
        {
            _entries = labels.Select(l => new RestFrequencyEntry(l, 0.0)).ToList();
        }

        public void FireSelectionChanged() => SelectionChanged?.Invoke();
    }
}
