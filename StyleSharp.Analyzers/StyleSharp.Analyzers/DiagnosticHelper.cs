// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Centralizes the smallest <c>Diagnostic.Create</c> overloads we use on analyzer hot
/// paths. This keeps call sites honest about message-arg count today, and gives us one place to
/// update if a lower-allocation Roslyn API becomes available later.
/// </summary>
internal static class DiagnosticHelper
{
    /// <summary>Creates a diagnostic with no message arguments or custom properties.</summary>
    /// <param name="descriptor">The rule descriptor.</param>
    /// <param name="location">The diagnostic location.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Create(DiagnosticDescriptor descriptor, Location location)
        => Diagnostic.Create(descriptor, location);

    /// <summary>Creates a diagnostic with one message argument.</summary>
    /// <param name="descriptor">The rule descriptor.</param>
    /// <param name="location">The diagnostic location.</param>
    /// <param name="messageArg">The one message argument.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Create(DiagnosticDescriptor descriptor, Location location, string messageArg)
        => Diagnostic.Create(descriptor, location, messageArg);

    /// <summary>Creates a diagnostic with two message arguments.</summary>
    /// <param name="descriptor">The rule descriptor.</param>
    /// <param name="location">The diagnostic location.</param>
    /// <param name="firstMessageArg">The first message argument.</param>
    /// <param name="secondMessageArg">The second message argument.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Create(DiagnosticDescriptor descriptor, Location location, string firstMessageArg, string secondMessageArg)
        => Diagnostic.Create(descriptor, location, firstMessageArg, secondMessageArg);

    /// <summary>Creates a diagnostic with cached custom properties but no message arguments.</summary>
    /// <param name="descriptor">The rule descriptor.</param>
    /// <param name="location">The diagnostic location.</param>
    /// <param name="properties">The custom diagnostic properties.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Create(
        DiagnosticDescriptor descriptor,
        Location location,
        ImmutableDictionary<string, string?> properties)
        => Diagnostic.Create(descriptor, location, properties);

    /// <summary>Creates a diagnostic from a syntax-tree span with no message arguments.</summary>
    /// <param name="descriptor">The rule descriptor.</param>
    /// <param name="tree">The owning syntax tree.</param>
    /// <param name="span">The source span to report.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Create(DiagnosticDescriptor descriptor, SyntaxTree tree, TextSpan span)
        => Diagnostic.Create(descriptor, Location.Create(tree, span));

    /// <summary>Creates a diagnostic from a syntax-tree span with cached custom properties.</summary>
    /// <param name="descriptor">The rule descriptor.</param>
    /// <param name="tree">The owning syntax tree.</param>
    /// <param name="span">The source span to report.</param>
    /// <param name="properties">The custom diagnostic properties.</param>
    /// <returns>The created diagnostic.</returns>
    public static Diagnostic Create(
        DiagnosticDescriptor descriptor,
        SyntaxTree tree,
        TextSpan span,
        ImmutableDictionary<string, string?> properties)
        => Diagnostic.Create(descriptor, Location.Create(tree, span), properties);
}
