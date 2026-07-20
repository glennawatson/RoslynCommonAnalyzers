// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1301 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1301 — a process command line is composed from a non-constant formatted or concatenated string.</summary>
    public static readonly DiagnosticDescriptor ProcessArgumentsComposition = Create(
        "SES1301",
        "Do not build a process command line from non-constant string parts",
        "'{0}' is set from a formatted or concatenated string; add each value to 'ArgumentList' so the runtime escapes it, instead of composing one command-line string",
        Injection,
        ProcessArgumentsCompositionDescription);

    /// <summary>The SES1301 rule description.</summary>
    private const string ProcessArgumentsCompositionDescription =
        "A process command line built by string interpolation or concatenation lets any special character in the composed "
        + "value change how the launched program parses its arguments: a space splits one value into two, and a quote or a "
        + "shell metacharacter can inject an extra argument or, on Windows, a whole extra command. Set the arguments through "
        + "'ProcessStartInfo.ArgumentList' (or the collection overload of 'Process.Start') instead, which takes each argument "
        + "as a discrete string and applies the platform's quoting rules, so a value containing spaces or quotes is passed "
        + "through unchanged as a single argument. The rule reports only a formatted or concatenated (non-constant) string "
        + "assigned to 'ProcessStartInfo.Arguments' or passed as the 'arguments' string of 'Process.Start'; a fully constant "
        + "value is left alone.";
}
