// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2432 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2432 — <c>GetType()</c> is called on a value that is already a <see cref="System.Type"/>.</summary>
    public static readonly DiagnosticDescriptor RedundantGetType = Create(
        "SST2432",
        "Do not call GetType() on a value that is already a Type",
        "'{0}' is already a Type, so GetType() returns the runtime type of the reflection object, not '{0}'",
        RedundantGetTypeDescription);

    /// <summary>The RedundantGetType rule description.</summary>
    private const string RedundantGetTypeDescription =
        "Calling GetType() on an expression whose type is System.Type (or a type derived from it) does not return the type the expression "
        + "describes. It returns the runtime type of the reflection object itself, which is the internal RuntimeType, so a follow-on such as "
        + ".Name is always the string \"RuntimeType\" regardless of the input. The call is silent and always wrong, and it turns up most often "
        + "in reflection and source-generator support code where the mistake is easy to miss. Drop the GetType() call and use the value directly.";
}
