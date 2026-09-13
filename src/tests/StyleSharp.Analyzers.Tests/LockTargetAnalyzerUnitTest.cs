// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using VerifyLockTarget = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.LockTargetAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the lock-target rules: SST1901 (accessible member), SST1902 (weak identity), and SST1903 (new object).</summary>
public class LockTargetAnalyzerUnitTest
{
    /// <summary>Checks absent reflection metadata and unresolved lock targets do not crash analysis.</summary>
    /// <param name="expression">The lock expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("gate")]
    [Arguments("missing")]
    public async Task MissingLockMetadataIsCleanAsync(string expression)
    {
        var tree = CSharpSyntaxTree.ParseText($"class Gate {{}} class C {{ void M(Gate gate) {{ lock ({expression}) {{}} }} }}");
        var compilation = CSharpCompilation.Create("MissingLockMetadata", [tree]);
        var diagnostics = await compilation.WithAnalyzers([new LockTargetAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Checks wrapper casts and semantic member types preserve the lock diagnostics.</summary>
    /// <param name="expression">The lock target, including expected diagnostics.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("((System.Object){|SST1901:Gate|})")]
    [Arguments("((global::System.Object){|SST1901:Gate|})")]
    [Arguments("(C)this")]
    [Arguments("{|SST1902:Name|}")]
    [Arguments("{|SST1902:Reflection|}")]
    [Arguments("{|SST1902:DerivedReflection|}")]
    [Arguments("{|SST1901:PublicProperty|}")]
    [Arguments("{|SST1901:ProtectedProperty|}")]
    [Arguments("{|SST1901:ProtectedInternalProperty|}")]
    [Arguments("other.InternalGate")]
    public Task WrappedAndSemanticLockTargetsAreClassifiedAsync(string expression) =>
        VerifyLockTarget.VerifyAnalyzerAsync($$"""
            class C
            {
                public readonly object Gate = new();
                private string Name => "gate";
                private System.Type Reflection => typeof(C);
                private System.Reflection.TypeDelegator DerivedReflection => new(typeof(C));
                public object PublicProperty => Gate;
                protected object ProtectedProperty => Gate;
                protected internal object ProtectedInternalProperty => Gate;
                internal object InternalGate = new();
                void M(Other other) { lock ({{expression}}) {} }
            }
            class Other { internal object InternalGate = new(); }
            """);

    /// <summary>Checks fresh locals escape through captures and arguments but not ordinary member reads.</summary>
    /// <param name="body">The method body.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("object gate = new(); gate.ToString(); lock ({|SST1903:gate|}) {}")]
    [Arguments("object gate = new(); System.Action use = () => gate.ToString(); lock (gate) {}")]
    [Arguments("object gate = new(); void Use() { gate.ToString(); } lock (gate) {} Use();")]
    [Arguments("object gate = new(); Use(gate); lock (gate) {}")]
    [Arguments("object gate = new(); gate = shared; lock (gate) {}")]
    [Arguments("switch (flag) { case true: object gate = new(); lock ({|SST1903:gate|}) {} break; }")]
    [Arguments("for (object gate = new(); flag;) { lock ({|SST1903:gate|}) {} }")]
    [Arguments("object gate; gate = shared; lock (gate) {}")]
    [Arguments("foreach (object gate in new object[] { shared }) { lock (gate) {} }")]
    [Arguments("if (shared is object gate) { lock (gate) {} }")]
    public Task FreshLocalPublicationControlsDiagnosticAsync(string body) =>
        VerifyLockTarget.VerifyAnalyzerAsync($"class C {{ void M(bool flag, object shared) {{ {body} }} static void Use(object value) {{}} }}");

    /// <summary>Checks top-level local scopes are not treated as method-local fresh locks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TopLevelLocalIsOutsideFreshLocalScopeAsync()
    {
        var test = new VerifyLockTarget.Test { TestCode = "object gate = new(); lock (gate) {}" };
        test.TestState.OutputKind = OutputKind.ConsoleApplication;
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies locking on a public field is reported (SST1901).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PublicFieldTargetReportedAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ObjectCastOnPublicFieldTargetReportedAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThisTargetReportedAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StringAndTypeofTargetsReportedAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NewObjectTargetReportedAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateObjectFieldIsCleanAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
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

    /// <summary>Verifies locking on a non-readonly private object field is reported (SST1904).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonReadonlyPrivateObjectFieldReportedAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                private object _gate = new();

                public void M()
                {
                    lock ({|SST1904:_gate|})
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies locking on a non-readonly private non-object field is reported (SST1904).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonReadonlyPrivateFieldReportedAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly object _other = new();
                private Gate _gate = new();

                public void M()
                {
                    lock ({|SST1904:_gate|})
                    {
                    }
                }
            }

            public sealed class Gate
            {
            }
            """);

    /// <summary>Verifies a readonly private object field is clean under every lock-target rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReadonlyPrivateObjectFieldIsCleanForNonReadonlyRuleAsync() =>
        PrivateObjectFieldIsCleanAsync();

    /// <summary>Verifies locking on a fresh local object that never escapes is reported (SST1903).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FreshLocalObjectTargetReportedAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void M()
                {
                    object gate = new();
                    lock ({|SST1903:gate|})
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a local that aliases a shared field is not reported (SST1903 stays silent).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalAliasingSharedFieldIsCleanAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly object _gate = new();

                public void M()
                {
                    object local = _gate;
                    lock (local)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a fresh local that is published to a field is not reported (SST1903 stays silent).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FreshLocalPublishedToFieldIsCleanAsync() =>
        VerifyLockTarget.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly object _gate = new();

                public C()
                {
                    object gate = new();
                    _gate = gate;
                    lock (gate)
                    {
                    }
                }
            }
            """);

    /// <summary>Parses the first lock statement from the supplied source.</summary>
    /// <param name="source">The source containing the lock statement.</param>
    /// <returns>The parsed lock statement.</returns>
    private static LockStatementSyntax ParseLockStatement(string source) =>
        (LockStatementSyntax)((MethodDeclarationSyntax)((ClassDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[0]).Members[1]).Body!.Statements[0];
}
