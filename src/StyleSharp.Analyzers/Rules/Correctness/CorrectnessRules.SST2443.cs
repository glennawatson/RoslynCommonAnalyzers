// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2443 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2443 — a typed logger's category is a type other than the one that logs.</summary>
    public static readonly DiagnosticDescriptor WrongLoggerCategory = Create(
        "SST2443",
        "A typed logger's category should be the type that logs through it",
        "This logger is categorized as '{0}', not '{1}'; log-level filters and sink routes for '{1}' will not take effect",
        WrongLoggerCategoryDescription);

    /// <summary>The WrongLoggerCategory rule description.</summary>
    private const string WrongLoggerCategoryDescription =
        "A generic logger's type argument becomes the log category, which is the string that per-namespace level filters and sink routes "
        + "match against. When a type holds or creates a logger categorized as some other type — the usual result of copying a field or a "
        + "creation call — its own configured filters and routes silently do nothing, and the entries appear under a category that has "
        + "nothing to do with where they came from. This is one of the harder logging faults to spot, because the code runs and logs; it "
        + "is only the filtering that fails. A base type or an implemented interface logging on behalf of the type, a dedicated category "
        + "marker type, and a nesting relationship are all deliberate and are not reported.";
}
