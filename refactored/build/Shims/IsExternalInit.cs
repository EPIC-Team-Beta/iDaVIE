// SPDX-License-Identifier: LGPL-3.0-or-later
// Polyfill: netstandard2.1 (Unity's target framework) does not ship
// System.Runtime.CompilerServices.IsExternalInit, which C# requires for `init`
// accessors and positional records/record-structs. The skeleton uses these
// heavily ({ get; init; }, `readonly record struct`). One internal copy is
// compiled into every iDaVIE.* assembly via Directory.Build.props.
// Not part of the design — a compile-only shim for the standalone (Route B) build.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
