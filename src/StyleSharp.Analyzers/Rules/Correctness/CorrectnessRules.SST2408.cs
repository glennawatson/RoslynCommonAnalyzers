// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2408 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2408 — a StringBuilder is filled and never read.</summary>
    public static readonly DiagnosticDescriptor StringBuilderNeverRead = Create(
        "SST2408",
        "A StringBuilder that is filled should be read",
        "'{0}' is appended to but its contents are never used",
        StringBuilderNeverReadDescription);

    /// <summary>The StringBuilderNeverRead rule description.</summary>
    private const string StringBuilderNeverReadDescription =
        "Every Append in the method did real work and then threw it away — the 'ToString' that was supposed to collect it is missing. The "
        + "code looks like it builds a string and does not, and nothing at runtime says so.";
}
