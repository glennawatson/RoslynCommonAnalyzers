// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2310 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2310 — an opt-in reminder to retire deprecated code.</summary>
    public static readonly DiagnosticDescriptor ObsoleteCodeShouldBeRemoved = CreateDisabled(
        "SST2310",
        "Deprecated code should be removed",
        "'{0}' is deprecated; remove it once its last caller is gone",
        ObsoleteCodeShouldBeRemovedDescription);

    /// <summary>The ObsoleteCodeShouldBeRemoved rule description.</summary>
    private const string ObsoleteCodeShouldBeRemovedDescription =
        "Enable this reminder when actively retiring deprecated APIs. It reports every '[Obsolete]', including attributes with migration "
        + "messages and diagnostic IDs. Libraries retaining deprecated members for compatibility can leave it disabled and use SST2308 "
        + "and SST2314 to validate migration guidance.";
}
