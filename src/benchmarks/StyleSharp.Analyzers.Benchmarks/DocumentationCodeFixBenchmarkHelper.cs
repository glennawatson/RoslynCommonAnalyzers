// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Shared lookup helpers for documentation-oriented code-fix benchmarks.</summary>
internal static class DocumentationCodeFixBenchmarkHelper
{
    /// <summary>Gets the documented member's summary element.</summary>
    /// <param name="member">The documented member declaration.</param>
    /// <returns>The summary element.</returns>
    public static XmlElementSyntax GetSummary(MemberDeclarationSyntax member)
    {
        if (XmlDocumentationHelper.GetDocumentationComment(member) is not { } documentation
            || XmlDocumentationHelper.FindElement(documentation, "summary") is not XmlElementSyntax summary)
        {
            throw new InvalidOperationException("Summary element not found.");
        }

        return summary;
    }
}
