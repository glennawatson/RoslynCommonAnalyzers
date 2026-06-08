// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using VerifyLockTarget = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.LockTargetAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the lock-target rules: SST1901 (accessible member), SST1902 (weak identity), and SST1903 (new object).</summary>
public class LockTargetAnalyzerUnitTest
{
    /// <summary>Verifies locking on a public field is reported (SST1901).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicFieldTargetReportedAsync()
        => await VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                public readonly object Gate = new();

                public void M()
                {
                    lock ({|SST1901:Gate|})
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies an object cast does not hide an accessible field lock target (SST1901).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectCastOnPublicFieldTargetReportedAsync()
        => await VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                public readonly object Gate = new();

                public void M()
                {
                    lock ((object){|SST1901:Gate|})
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies locking on 'this' is reported (SST1902, opt-in).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThisTargetReportedAsync()
        => await VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    lock ({|SST1902:this|})
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies locking on a string and a typeof expression are reported (SST1902).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringAndTypeofTargetsReportedAsync()
        => await VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly string _name = "x";

                public void M()
                {
                    lock ({|SST1902:_name|})
                    {
                    }

                    lock ({|SST1902:typeof(C)|})
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies locking on a newly-created object is reported (SST1903).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewObjectTargetReportedAsync()
        => await VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    lock ({|SST1903:new object()|})
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies locking on a private object field is not reported by any lock-target rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateObjectFieldIsCleanAsync()
        => await VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly object _gate = new();

                public void M()
                {
                    lock (_gate)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies the syntax fast path recognizes a private object field lock target declared in the same type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxFastPathRecognizesPrivateObjectFieldAsync()
    {
        var lockStatement = ParseLockStatement(
            "public class C { private readonly object _gate = new(); void M() { lock (_gate) { } } }");
        var type = (TypeDeclarationSyntax)lockStatement.Parent!.Parent!.Parent!;

        await Assert.That(LockTargetAnalyzer.IsPrivateObjectFieldLockTarget(type, lockStatement.Expression)).IsTrue();
    }

    /// <summary>Verifies the syntax fast path rejects a shadowed identifier so semantic binding still runs.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxFastPathRejectsShadowedIdentifierAsync()
    {
        var lockStatement = ParseLockStatement(
            "public class C { private readonly object _gate = new(); void M(object _gate) { lock (_gate) { } } }");
        var type = (TypeDeclarationSyntax)lockStatement.Parent!.Parent!.Parent!;

        await Assert.That(LockTargetAnalyzer.IsPrivateObjectFieldLockTarget(type, lockStatement.Expression)).IsFalse();
    }

    /// <summary>Parses the first lock statement from the supplied source.</summary>
    /// <param name="source">The source containing the lock statement.</param>
    /// <returns>The parsed lock statement.</returns>
    private static LockStatementSyntax ParseLockStatement(string source)
        => (LockStatementSyntax)((MethodDeclarationSyntax)((ClassDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members[1]).Body!.Statements[0];
}
