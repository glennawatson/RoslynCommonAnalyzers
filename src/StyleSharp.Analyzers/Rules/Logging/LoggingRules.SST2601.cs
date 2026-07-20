// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2601 descriptor.</summary>
internal static partial class LoggingRules
{
    /// <summary>SST2601 — a logger field or property named against the logger naming convention.</summary>
    public static readonly DiagnosticDescriptor LoggerMemberNaming = Create(
        "SST2601",
        "Logger field or property should follow the logger naming convention",
        "Logger member '{0}' does not follow the logger naming convention; name it '{1}'",
        LoggerMemberNamingDescription);

    /// <summary>The LoggerMemberNaming rule description.</summary>
    private const string LoggerMemberNamingDescription =
        "An 'ILogger' or 'ILogger<T>' field or property is named against the conventional logger name, so loggers "
        + "read inconsistently from one type to the next and a reader cannot tell at a glance which member is the "
        + "logger. The convention is a private instance logger named '_logger' (or '_log') and any non-private or "
        + "static logger named 'Logger'; a member whose name does not match the pattern for its accessibility and "
        + "static-ness is reported on its identifier. The accepted private-instance names are configurable with "
        + "'stylesharp.SST2601.fieldname' (a comma-separated list, defaulting to '_logger, _log'). The rule reports "
        + "only when 'Microsoft.Extensions.Logging.ILogger' resolves in the compilation, and binds each candidate to "
        + "confirm its type, so a same-named member of another type is left alone. There is no code fix: a rename "
        + "ripples across every reference and is a judgement the author should make.";
}
