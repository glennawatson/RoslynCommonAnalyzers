// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2310 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2310 — deprecated code is still here, and is a standing reminder to remove it.</summary>
    public static readonly DiagnosticDescriptor ObsoleteCodeShouldBeRemoved = Create(
        "SST2310",
        "Deprecated code should be removed",
        "'{0}' is deprecated; remove it once its last caller is gone",
        ObsoleteCodeShouldBeRemovedDescription);

    /// <summary>The ObsoleteCodeShouldBeRemoved rule description.</summary>
    private const string ObsoleteCodeShouldBeRemovedDescription =
        "Deprecating a member is the first half of removing it. The second half is the one that pays: until the member is gone it is still "
        + "compiled, still tested, still maintained, and still found by everyone reading the type for the first time. This rule is a standing "
        + "reminder — it reports every '[Obsolete]', including one with a message, and it keeps reporting until the code is deleted. That "
        + "makes it a rule for a codebase actively retiring API, and the wrong rule for a library that must keep its obsolete members for "
        + "compatibility: there, set it to 'none' and let SST2308 and SST2314 police the attribute's contents instead.";
}
