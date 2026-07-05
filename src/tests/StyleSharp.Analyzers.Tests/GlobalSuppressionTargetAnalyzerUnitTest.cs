// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyGlobalSuppressionTarget = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.GlobalSuppressionTargetAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="GlobalSuppressionTargetAnalyzer"/>.</summary>
public class GlobalSuppressionTargetAnalyzerUnitTest
{
    /// <summary>Verifies a global suppression target that cannot resolve is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingDeclarationTargetIsReportedAsync()
        => await VerifyGlobalSuppressionTarget.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            [assembly: {|SST1457:SuppressMessage("Style", "SST1000", Justification = "Test.", Scope = "member", Target = "M:C.Missing")|}]

            public sealed class C
            {
            }
            """);

    /// <summary>Verifies a legacy tilde-prefixed target is reported before target resolution.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LegacyTargetPrefixIsReportedAsync()
        => await VerifyGlobalSuppressionTarget.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            [assembly: {|SST1458:SuppressMessage("Style", "SST1000", Justification = "Test.", Scope = "member", Target = "~M:C.M")|}]

            public sealed class C
            {
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies a target that resolves to a declaration is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolvedDeclarationTargetIsCleanAsync()
        => await VerifyGlobalSuppressionTarget.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.CodeAnalysis;

            [assembly: SuppressMessage("Style", "SST1000", Justification = "Test.", Scope = "member", Target = "M:C.M")]

            public sealed class C
            {
                public void M()
                {
                }
            }
            """);
}
