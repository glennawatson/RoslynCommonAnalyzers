// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2319 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2319 — an optional parameter whose default a shorter overload always shadows.</summary>
    public static readonly DiagnosticDescriptor UnreachableOptionalDefault = Create(
        "SST2319",
        "This optional parameter's default can never be used",
        "'{0}' can never bind with its default; an overload taking ({1}) already handles every call that omits it",
        UnreachableOptionalDefaultDescription);

    /// <summary>The UnreachableOptionalDefault rule description.</summary>
    private const string UnreachableOptionalDefaultDescription =
        "A method declares an optional parameter, but a sibling overload of the same name takes exactly the parameters that come before it. "
        + "Every call that omits the optional argument matches the shorter overload, so overload resolution binds that one and the default is "
        + "never reached. The default is dead surface: it shows up in completion lists and documentation promising a behaviour no caller can "
        + "ever get. Remove the default, or give the two methods signatures that do not shadow each other.";
}
