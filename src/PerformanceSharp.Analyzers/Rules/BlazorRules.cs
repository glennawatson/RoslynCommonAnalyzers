// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the Blazor component performance rules (PSH16xx). Each targets an
/// allocation or diff-churn pattern in a component's render output, and is gated on the relevant
/// Blazor type existing in the referenced framework so a non-Blazor project pays nothing.
/// </summary>
internal static partial class BlazorRules
{
    /// <summary>Creates an enabled-by-default Warning descriptor in the Blazor category.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Blazor", description);
}
