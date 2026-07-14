// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the correctness rules (SST24xx). These report code that compiles and
/// runs but does not do what it appears to: arguments handed over in the wrong order, a guard that
/// runs too late to guard anything, a reference to a member that is not there.
/// </summary>
internal static partial class CorrectnessRules
{
    /// <summary>Creates a Warning-severity Correctness descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Correctness", description);
}
