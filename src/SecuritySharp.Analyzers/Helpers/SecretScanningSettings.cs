// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The secret-scanning settings resolved for one syntax tree.</summary>
/// <param name="AllowDocumentationExamples">Whether any key carrying a published-sample marker is accepted.</param>
/// <param name="AllowedExamples">The exact literal values accepted as published samples.</param>
/// <remarks>
/// The default value of this type is the shipped behaviour: every recognised credential shape is reported.
/// </remarks>
internal readonly record struct SecretScanningSettings(bool AllowDocumentationExamples, string[]? AllowedExamples)
{
    /// <summary>Returns whether a literal is one the project has named as a published sample.</summary>
    /// <param name="value">The decoded literal content.</param>
    /// <returns><see langword="true"/> when the value matches a configured entry exactly.</returns>
    internal bool IsAllowedExample(string value)
    {
        var allowed = AllowedExamples;
        if (allowed is null)
        {
            return false;
        }

        for (var i = 0; i < allowed.Length; i++)
        {
            if (string.Equals(allowed[i], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
