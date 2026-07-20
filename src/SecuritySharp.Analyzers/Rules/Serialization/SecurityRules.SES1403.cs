// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1403 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1403 — a JSON deserialization depth limit is raised beyond a safe ceiling.</summary>
    public static readonly DiagnosticDescriptor JsonMaxDepth = new(
        "SES1403",
        "JSON deserialization depth limit must stay within a safe ceiling",
        "This sets a System.Text.Json MaxDepth of {0}; a limit above {1} lets deeply nested JSON exhaust the thread stack and crash the process, so keep MaxDepth at or below {1}",
        Serialization,
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: JsonMaxDepthDescription,
        helpLinkUri: "https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/SES1403.md");

    /// <summary>The SES1403 rule description.</summary>
    private const string JsonMaxDepthDescription =
        "System.Text.Json bounds how far it will recurse into nested objects and arrays with a MaxDepth limit (default 64), so "
        + "a maliciously deep payload cannot drive the reader into unbounded recursion and exhaust the thread stack -- a "
        + "StackOverflowException tears down the whole process and no catch block can recover it. Raising MaxDepth to a large "
        + "constant re-opens that denial-of-service surface: an attacker who controls the input nests to that depth and takes "
        + "the service down. This rule reports a 'MaxDepth' assignment on 'System.Text.Json.JsonSerializerOptions', "
        + "'System.Text.Json.JsonReaderOptions', or 'System.Text.Json.JsonDocumentOptions' whose value is a compile-time "
        + "constant strictly above the configured ceiling (default 64). A value of 0 selects the framework default and is left "
        + "alone, as is any non-constant value whose size cannot be judged from the source. Keep the limit at or below a depth "
        + "your inputs genuinely need, and configure the ceiling per project with 'securitysharp.SES1403.maxdepth'.";
}
