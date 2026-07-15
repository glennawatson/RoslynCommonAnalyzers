// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2431 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2431 — an overridden <c>ToString</c> can hand back <see langword="null"/>.</summary>
    public static readonly DiagnosticDescriptor ToStringReturnsNull = Create(
        "SST2431",
        "ToString() should not return null",
        "This ToString() can return null, which breaks interpolation, concatenation, and debugger display; return string.Empty instead",
        ToStringReturnsNullDescription);

    /// <summary>The ToStringReturnsNull rule description.</summary>
    private const string ToStringReturnsNullDescription =
        "An overridden ToString() is called from string interpolation, concatenation, Console output, logging, and every "
        + "debugger display, and none of those callers expect a null back. When one comes anyway the failure surfaces far "
        + "from the ToString() that produced it, usually as a NullReferenceException inside framework code the caller cannot "
        + "see. Returning string.Empty for the empty case keeps every one of those call sites working. Only a return that is "
        + "syntactically null is reported — null, null!, a parenthesised or cast null, or a null branch of a conditional; a "
        + "null returned by a nested lambda or local function does not return from ToString() and is left alone.";
}
