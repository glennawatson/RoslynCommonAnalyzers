// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1448CallerInfoArgumentAnalyzer,
    StyleSharp.Analyzers.Sst1448CallerInfoArgumentCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst1448CallerInfoArgumentAnalyzer"/> (SST1448 explicit caller-info arguments).</summary>
public class Sst1448CallerInfoArgumentAnalyzerUnitTest
{
    /// <summary>An explicit caller-member-name argument.</summary>
    private const string ExplicitMemberNameSource = """
        using System.Runtime.CompilerServices;

        public class C
        {
            public void Log(string message, [CallerMemberName] string caller = "")
            {
            }

            public void M() => Log("text", {|SST1448:"M"|});
        }
        """;

    /// <summary>The explicit-argument source after the fix.</summary>
    private const string ExplicitMemberNameFixed = """
        using System.Runtime.CompilerServices;

        public class C
        {
            public void Log(string message, [CallerMemberName] string caller = "")
            {
            }

            public void M() => Log("text");
        }
        """;

    /// <summary>Verifies constructor, finalizer, accessor, and local-function names follow compiler spelling.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task SpecialMemberNamesAreReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                C() { Log({|SST1448:".ctor"|}); }
                static C() { Log({|SST1448:".cctor"|}); Log("C"); }
                ~C() { Log({|SST1448:"Finalize"|}); Log("C"); }
                string this[int index] => Log({|SST1448:"Item"|});
                event Action Changed
                {
                    add { Log({|SST1448:"Changed"|}); }
                    remove { Log({|SST1448:"Changed"|}); }
                }
                void Outer()
                {
                    void Local() { Log({|SST1448:"Outer"|}); Log("Local"); }
                    Local();
                }
                static string Log([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies names in initializers and nonconstant or different arguments remain unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NonRedundantMemberArgumentsAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System.Runtime.CompilerServices;
            class C
            {
                string field = Log("field");
                string Property { get; } = Log("Property");
                void M(string name)
                {
                    const string Other = "Other";
                    Log(name);
                    Log(Other);
                    Log(null);
                }
                static string Log([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies file and line arguments report while forwarding preserves the original caller.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task FileAndLineArgumentsRespectForwardingAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System;
            using System.Runtime.CompilerServices;
            class C
            {
                static void Log([CallerFilePath] string file = "", [CallerLineNumber] int line = 0) { }
                void M(string file, int line)
                {
                    Log({|SST1448:file|}, {|SST1448:line|});
                    Log({|SST1448:"explicit.cs"|}, {|SST1448:42|});
                }
                void Forward([CallerFilePath] string file = "", [CallerLineNumber] int line = 0) => Log(file, line);
                static void Ordinary([Marker] int value = 0) { }
                void Other() { Ordinary(1); Log(); }
            }
            class MarkerAttribute : Attribute { }
            """);

    /// <summary>Verifies explicit and target-typed creations bind caller-info parameters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ConstructorArgumentsAreReportedAsync() =>
        Verify.VerifyAnalyzerAsync("""
            using System.Runtime.CompilerServices;
            class C
            {
                public C([CallerLineNumber] int line = 0) { }
                static void M()
                {
                    C first = new C({|SST1448:42|});
                    C second = new({|SST1448:42|});
                    C third = new C { };
                }
            }
            """);

    /// <summary>Verifies unresolved invocations and nonoptional arguments do not report.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task UnboundAndRequiredArgumentsAreIgnoredAsync() =>
        new Verify.Test { TestCode = "class C { void M() { Missing(1); Required(1); } void Required(int value) { } }", CompilerDiagnostics = CompilerDiagnostics.None }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies an attributed optional parameter is ignored when caller-info attributes are unavailable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingCallerInfoAttributesAreCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            namespace System
            {
                public class Object { }
                public class Attribute { }
                public struct Void { }
                public struct Int32 { }
            }
            class MarkerAttribute : System.Attribute { }
            class C
            {
                static void Log([Marker] int value = 0) { }
                void M() { Log(1); Log(2); }
            }
            """);
        var compilation = CSharpCompilation.Create("MissingCallerInfo", [tree], options: new(OutputKind.DynamicallyLinkedLibrary));
        var diagnostics = await compilation.WithAnalyzers([new Sst1448CallerInfoArgumentAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies an explicit caller-member-name argument is flagged.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitCallerMemberNameArgumentIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(ExplicitMemberNameSource);

    /// <summary>Verifies letting the compiler fill the parameter is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CompilerSuppliedCallerInfoIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Log(string message, [CallerMemberName] string caller = "")
                {
                }

                public void M() => Log("text");
            }
            """);

    /// <summary>Verifies forwarding the enclosing member's caller-info parameter is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ForwardingCallerInfoParameterIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Log(string message, [CallerMemberName] string caller = "")
                {
                }

                public void Outer(string message, [CallerMemberName] string caller = "") => Log(message, caller);
            }
            """);

    /// <summary>Verifies an explicit caller-line-number argument is flagged, including named form.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitCallerLineNumberArgumentIsFlaggedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Log(string message, [CallerLineNumber] int line = 0)
                {
                }

                public void M() => Log("text", {|SST1448:line: 42|});
            }
            """);

    /// <summary>Verifies ordinary optional arguments are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OrdinaryOptionalArgumentIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Log(string message, int retries = 3)
                {
                }

                public void M() => Log("text", 5);
            }
            """);

    /// <summary>Verifies a name stated inside a constructor is clean, since the compiler supplies <c>.ctor</c>.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MemberNameStatedInAConstructorIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public C()
                {
                    Width = Register(nameof(Width));
                    Height = Register(nameof(Height));
                }

                public string Width { get; }

                public string Height { get; }

                private static string Register([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies a name stated about another member is clean, since the compiler supplies this one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MemberNameStatedAboutAnotherMemberIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public string Width => Register(nameof(Height));

                public string Height => "";

                private static string Register([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies an accessor stating its own property is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MemberNameStatedInItsOwnAccessorIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                public string Width => Register({|SST1448:nameof(Width)|});

                private static string Register([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies a lambda takes the name from the member containing it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MemberNameStatedInsideALambdaFollowsTheEnclosingMemberAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Runtime.CompilerServices;

            public class C
            {
                public void Run()
                {
                    Action inside = () => Register({|SST1448:"Run"|});
                    Action other = () => Register("Elsewhere");
                    inside();
                    other();
                }

                private static string Register([CallerMemberName] string name = "") => name;
            }
            """);

    /// <summary>Verifies the fix removes the explicit argument.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixRemovesExplicitArgumentAsync() =>
        Verify.VerifyCodeFixAsync(ExplicitMemberNameSource, ExplicitMemberNameFixed);
}
