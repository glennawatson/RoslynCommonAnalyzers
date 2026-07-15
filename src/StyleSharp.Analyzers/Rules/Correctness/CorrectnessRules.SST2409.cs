// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2409 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2409 — a general or runtime-reserved exception type is thrown.</summary>
    public static readonly DiagnosticDescriptor ThrowsGeneralException = Create(
        "SST2409",
        "Do not throw a general exception type",
        "Throwing '{0}' {1}",
        ThrowsGeneralExceptionDescription);

    /// <summary>The ThrowsGeneralException rule description.</summary>
    private const string ThrowsGeneralExceptionDescription =
        "'Exception', 'SystemException' and 'ApplicationException' say only that something went wrong. A caller who wants to handle one "
        + "failure has to catch all of them, including the ones it has no idea about — so the code that handles a missing file also "
        + "swallows the bug three frames down. 'NullReferenceException', 'IndexOutOfRangeException' and 'OutOfMemoryException' are worse: "
        + "the runtime raises them to report a bug in the process, so throwing one yourself makes deliberate code look like a runtime "
        + "failure. Throw the type that names the failure.";
}
