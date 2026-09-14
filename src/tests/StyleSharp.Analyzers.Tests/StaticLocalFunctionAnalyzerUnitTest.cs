// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyStaticLocalFunction = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2235StaticLocalFunctionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2235StaticLocalFunctionAnalyzer"/>.</summary>
public class StaticLocalFunctionAnalyzerUnitTest
{
    /// <summary>Verifies a capture-free local function is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CaptureFreeLocalFunctionIsReportedAsync() =>
        RunAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    int {|SST2235:Twice|}(int input) => input * 2;
                    return Twice(value);
                }
            }
            """);

    /// <summary>Verifies a local function that captures an outer parameter is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CapturingLocalFunctionIsCleanAsync() =>
        RunAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    int Add(int input) => input + value;
                    return Add(1);
                }
            }
            """);

    /// <summary>Verifies a recursive capture-free local function is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RecursiveCaptureFreeLocalFunctionIsReportedAsync() =>
        RunAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    int {|SST2235:Factorial|}(int input) => input <= 1 ? 1 : input * Factorial(input - 1);
                    return Factorial(value);
                }
            }
            """);

    /// <summary>Verifies member-access names do not hide a capturing receiver.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CapturingReceiverIsCleanAsync() =>
        RunAsync(
            """
            public sealed class C
            {
                public int M(int value)
                {
                    int Format(int input) => value.ToString().Length + input;
                    return Format(1);
                }
            }
            """);

    /// <summary>Verifies a local function whose nested lambda captures an outer local is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The capture does not have to be written in the body itself. A lambda created there that touches an
    /// enclosing local captures it just the same, and the local function that builds the lambda cannot then
    /// be static — the compiler says CS8421.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalFunctionCapturingThroughANestedLambdaIsNotReportedAsync() =>
        RunAsync(
            """
            using System;

            public class C
            {
                public Action M()
                {
                    var count = 0;

                    Action Build() => () => count++;

                    return Build();
                }
            }
            """);

    /// <summary>Verifies explicit instance access and implicit instance members prevent static conversion.</summary>
    /// <param name="expression">The capturing expression.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("this.Value")]
    [Arguments("base.GetHashCode()")]
    [Arguments("Value")]
    [Arguments("Field")]
    [Arguments("Changed")]
    [Arguments("GetHashCode()")]
    [Arguments("StaticField")]
    public Task InstanceReferencesPreventStaticConversionAsync(string expression) =>
        RunAsync($$"""
            public class C
            {
                private int Field = 1;
                private static int StaticField = 2;
                private int Value => 3;
                private event System.Action Changed;
                public object M()
                {
                    object Read() => {{expression}};
                    return Read();
                }
            }
            """);

    /// <summary>Verifies references owned by the function, including nested lambda locals, remain capture-free.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalsAndNestedLambdaParametersAreOwnedByFunctionAsync() =>
        RunAsync("""
            public class C
            {
                public int M()
                {
                    int {|SST2235:Read|}(string first, string second)
                    {
                        System.Func<int, int> transform = input => { System.Int32 local = input; return local; };
                        return transform(second?.Length ?? first.Length);
                    }
                    return Read("a", "b");
                }
            }
            """);

    /// <summary>Verifies generic type parameters and captures owned by an outer local function are safe.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GenericTypesAndNestedLocalFunctionsRespectOwnershipAsync() =>
        RunAsync("""
            class C
            {
                public T M<T>()
                {
                    T {|SST2235:Read|}() => default(T);
                    int {|SST2235:Outer|}(int value)
                    {
                        int Inner() => value;
                        return Inner();
                    }
                    _ = Outer(1);
                    return Read();
                }
            }
            """);

    /// <summary>Verifies an unqualified static method call does not capture the containing instance.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticMethodCallsAreCaptureFreeAsync() =>
        RunAsync("class C { static int Value() => 1; int M() { int {|SST2235:Read|}() => Value(); return Read(); } }");

    /// <summary>Verifies the implicitly declared setter value still counts as captured state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SetterValueIsCapturedAsync() =>
        RunAsync("class C { int P { set { int Read() => value; _ = Read(); } } }");

    /// <summary>Verifies existing static functions require no diagnostic.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticFunctionIsCleanAsync() =>
        RunAsync("class C { int M() { static int Read() => 1; return Read(); } }");

    /// <summary>Verifies malformed functions are handled without analyzer exceptions.</summary>
    /// <param name="source">The incomplete declaration and its current diagnostic behavior.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C { void M() { int Read(); } }")]
    [Arguments("class C { void M() { int {|SST2235:Read|}() => missing; } }")]
    public Task IncompleteFunctionsRetainCurrentDiagnosticsAsync(string source) =>
        new VerifyStaticLocalFunction.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies static local functions are not suggested before C# 8.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OlderLanguageVersionIsCleanAsync()
    {
        var test = new VerifyStaticLocalFunction.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = "class C { int M() { int Read() => 1; return Read(); } }" };
        test.SolutionTransforms.Add(static (solution, projectId) =>
            solution.WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp7_3)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task RunAsync(string source) =>
        new VerifyStaticLocalFunction.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source }.RunAsync(CancellationToken.None);
}
