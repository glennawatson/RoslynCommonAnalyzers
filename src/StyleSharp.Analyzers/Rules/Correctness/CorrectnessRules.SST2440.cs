// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2440 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2440 — two log values are ordered against the placeholders they name.</summary>
    public static readonly DiagnosticDescriptor TransposedTemplateArguments = Create(
        "SST2440",
        "Log values should follow the order of the template placeholders",
        "'{0}' is logged under placeholder '{1}'; the two values look transposed",
        TransposedTemplateArgumentsDescription);

    /// <summary>The TransposedTemplateArguments rule description.</summary>
    private const string TransposedTemplateArgumentsDescription =
        "A message template binds its values to placeholders by position: the first value fills the first placeholder, and so on. When two "
        + "values are named after the placeholders but sit in each other's slots, the call compiles and files each value under the wrong "
        + "property name, so every later query keyed on that name reads the wrong data. Only a genuine two-way swap is reported, and only "
        + "when each value is a bare name or a simple member access, the placeholder names are all distinct, and the name in each slot "
        + "matches the other slot's placeholder. A three-way rotation has no unambiguous repair and is left alone.";
}
