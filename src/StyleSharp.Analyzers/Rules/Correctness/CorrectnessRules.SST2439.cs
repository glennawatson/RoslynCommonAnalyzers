// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2439 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2439 — an exception is passed as a message value rather than the exception argument.</summary>
    public static readonly DiagnosticDescriptor ExceptionAsTemplateArgument = Create(
        "SST2439",
        "An exception should be passed as the exception argument",
        "'{0}' is an exception handed to the message template; pass it as the exception argument instead",
        ExceptionAsTemplateArgumentDescription);

    /// <summary>The ExceptionAsTemplateArgument rule description.</summary>
    private const string ExceptionAsTemplateArgumentDescription =
        "An exception passed as one of the template values is formatted into text and filed under a property name, while the dedicated "
        + "exception argument stays empty. A structured sink then has no stack trace, no exception type, and no inner-exception chain to "
        + "index, because those come only from the exception argument. Every logging method offers an overload whose first argument is the "
        + "exception; using it keeps the full detail. Reported when an argument whose type derives from the exception base type is passed "
        + "as a template value and that exception-argument overload exists.";
}
