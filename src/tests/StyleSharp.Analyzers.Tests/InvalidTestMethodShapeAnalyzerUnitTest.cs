// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using VerifyTest = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2509InvalidTestMethodShapeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2509 (a test method whose shape the runner cannot execute).</summary>
public class InvalidTestMethodShapeAnalyzerUnitTest
{
    /// <summary>Minimal xUnit attribute stubs, including the theory attribute that derives from the fact attribute.</summary>
    private const string XunitStubs = """
        namespace Xunit
        {
            using System;
            public class FactAttribute : Attribute { }
            public class TheoryAttribute : FactAttribute { }
        }
        """;

    /// <summary>Minimal NUnit attribute stubs.</summary>
    private const string NUnitStubs = """
        namespace NUnit.Framework
        {
            using System;
            public class TestAttribute : Attribute { }
        }
        """;

    /// <summary>Minimal MSTest attribute stubs, including the data-test-method attribute that derives from the test-method attribute.</summary>
    private const string MsTestStubs = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            using System;
            public class TestMethodAttribute : Attribute { }
            public class DataTestMethodAttribute : TestMethodAttribute { }
        }
        """;

    /// <summary>Minimal TUnit attribute stubs.</summary>
    private const string TUnitStubs = """
        namespace TUnit.Core
        {
            using System;
            public sealed class TestAttribute : Attribute { }
        }
        """;

    /// <summary>Verifies a non-public xUnit fact is reported because the runner does not discover it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XunitPrivateFactIsReportedAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                private void {|SST2509:Case|}() { }
            }
            """);

    /// <summary>Verifies an internal xUnit theory is reported, exercising the derived-attribute marker walk.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XunitInternalTheoryIsReportedAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Theory]
                internal void {|SST2509:Case|}() { }
            }
            """);

    /// <summary>Verifies a non-public NUnit test is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NUnitPrivateTestIsReportedAsync() =>
        VerifyAsync(
            NUnitStubs + """

            public class Tests
            {
                [NUnit.Framework.Test]
                private void {|SST2509:Case|}() { }
            }
            """);

    /// <summary>Verifies a non-public MSTest test method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MsTestPrivateMethodIsReportedAsync() =>
        VerifyAsync(
            MsTestStubs + """

            public class Tests
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                private void {|SST2509:Case|}() { }
            }
            """);

    /// <summary>Verifies a parameterless generic xUnit fact is reported, because its type argument cannot be inferred.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XunitGenericParameterlessFactIsReportedAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public void {|SST2509:Case|}<T>() { }
            }
            """);

    /// <summary>Verifies a parameterless generic TUnit test is reported even though TUnit is exempt from the public check.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TUnitGenericParameterlessTestIsReportedAsync() =>
        VerifyAsync(
            TUnitStubs + """

            public class Tests
            {
                [TUnit.Core.Test]
                public void {|SST2509:Case|}<T>() { }
            }
            """);

    /// <summary>Verifies an xUnit fact that returns a non-awaitable value type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XunitFactReturningIntIsReportedAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public int {|SST2509:Case|}() => 0;
            }
            """);

    /// <summary>Verifies an NUnit test that returns a non-awaitable reference type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NUnitTestReturningStringIsReportedAsync() =>
        VerifyAsync(
            NUnitStubs + """

            public class Tests
            {
                [NUnit.Framework.Test]
                public string {|SST2509:Case|}() => "";
            }
            """);

    /// <summary>Verifies a public, non-generic, void-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PublicVoidFactIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public void Case() { }
            }
            """);

    /// <summary>Verifies a static public, void-returning xUnit fact is never reported, because static is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticPublicVoidFactIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public static void Case() { }
            }
            """);

    /// <summary>Verifies a Task-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XunitFactReturningTaskIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public System.Threading.Tasks.Task Case() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    /// <summary>Verifies a ValueTask-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XunitFactReturningValueTaskIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public System.Threading.Tasks.ValueTask Case() => default;
            }
            """);

    /// <summary>Verifies a Task&lt;T&gt;-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XunitFactReturningTaskOfIntIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public System.Threading.Tasks.Task<int> Case() => System.Threading.Tasks.Task.FromResult(0);
            }
            """);

    /// <summary>Verifies a ValueTask&lt;T&gt;-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task XunitFactReturningValueTaskOfIntIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public System.Threading.Tasks.ValueTask<int> Case() => default;
            }
            """);

    /// <summary>Verifies a non-public TUnit test is never reported, because TUnit does not universally require public methods.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TUnitPrivateTestIsCleanAsync() =>
        VerifyAsync(
            TUnitStubs + """

            public class Tests
            {
                [TUnit.Core.Test]
                private void Case() { }
            }
            """);

    /// <summary>Verifies a generic test method that declares parameters is not reported for shape, leaving data-source concerns to another rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GenericFactWithParametersIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public void Case<T>(T value) { }
            }
            """);

    /// <summary>Verifies an ordinary method that carries no test attribute is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonTestMethodIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [System.Obsolete]
                private int Helper() => 0;
            }
            """);

    /// <summary>Verifies a same-named attribute from an unrelated namespace on a suspect-shaped method is never treated as a test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LookalikeTestAttributeIsCleanAsync() =>
        VerifyAsync(
            XunitStubs + """

            public sealed class TheoryAttribute : System.Attribute { }

            public class Tests
            {
                [Theory]
                private int Case() => 0;
            }
            """);

    /// <summary>Verifies nothing is reported when no test framework is referenced at all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NoFrameworkReferencedIsCleanAsync() =>
        VerifyAsync(
            """
            using System;

            public sealed class FactAttribute : Attribute { }

            public class Tests
            {
                [Fact]
                private int Case() => 0;
            }
            """);

    /// <summary>Verifies a test method whose return type does not resolve is not reported, so transient editing errors stay quiet.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnresolvedReturnTypeIsCleanAsync() =>
        VerifyIgnoringCompilerDiagnosticsAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public Undefined Case() => default;
            }
            """);

    /// <summary>Verifies implicit public accessibility reaches the bound void-returning fast path.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceVoidTestIsCleanAsync() =>
        VerifyAsync(XunitStubs + """

            public interface Tests
            {
                [Xunit.Fact] void Case();
            }
            """);

    /// <summary>Verifies all four awaited definitions remain runnable when reused in one compilation.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AwaitedReturnDefinitionsAreReusedAsync() =>
        VerifyAsync(XunitStubs + """

            public class Tests
            {
                [Xunit.Fact] public System.Threading.Tasks.Task First() => null;
                [Xunit.Fact] public System.Threading.Tasks.Task Second() => null;
                [Xunit.Theory] public System.Threading.Tasks.Task<T> Generic<T>(T value) => null;
                [Xunit.Fact] public System.Threading.Tasks.ValueTask Third() => default;
                [Xunit.Theory] public System.Threading.Tasks.ValueTask<T> Fourth<T>(T value) => default;
            }
            """);

    /// <summary>Verifies custom awaitables and unrelated types named Task are outside the accepted return definitions.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UncachedAwaitableAndTaskLookalikeAreReportedAsync() =>
        VerifyAsync(XunitStubs + """

            public class Task
            {
                public System.Runtime.CompilerServices.TaskAwaiter GetAwaiter() => default;
            }
            public class Tests
            {
                [Xunit.Fact] public Task {|SST2509:Lookalike|}() => null;
                [Xunit.Fact] public System.Runtime.CompilerServices.YieldAwaitable {|SST2509:Yielding|}() => default;
            }
            """);

    /// <summary>Verifies TUnit accepts bound awaited and unresolved returns after the accessibility fast path declines.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PrivateTUnitReturnShapesAreClassifiedAsync() =>
        VerifyIgnoringCompilerDiagnosticsAsync(TUnitStubs + """

            public class Tests
            {
                [TUnit.Core.Test] private System.Threading.Tasks.Task First() => null;
                [TUnit.Core.Test] private System.Threading.Tasks.Task<int> Second() => null;
                [TUnit.Core.Test] private System.Threading.Tasks.ValueTask Third() => default;
                [TUnit.Core.Test] private System.Threading.Tasks.ValueTask<int> Fourth() => default;
                [TUnit.Core.Test] private Undefined Unresolved() => default;
                [TUnit.Core.Test] private void Generic<T>(T value) { }
                [TUnit.Core.Test] private int {|SST2509:Invalid|}() => 0;
            }
            """);

    /// <summary>Verifies ordinary attributes before and after real markers do not hide the framework's public requirement.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MixedAttributeListsKeepPublicRequirementAsync() =>
        VerifyAsync(XunitStubs + TUnitStubs + MsTestStubs + """

            public class Tests
            {
                [System.Obsolete][TUnit.Core.Test, Xunit.Fact, System.CLSCompliant(false)]
                private void {|SST2509:Mixed|}() { }
                [Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethodAttribute]
                private void {|SST2509:Data|}() { }
            }
            """);

    /// <summary>Verifies unresolved and non-method attribute targets are classified without assuming method attribute data exists.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InvalidAttributeTargetsAndBindingsAreHandledAsync() =>
        VerifyIgnoringCompilerDiagnosticsAsync(XunitStubs + """

            public class Tests
            {
                [Fact] private int Unresolved() => 0;
                [Xunit.Fact(1)] private int MissingConstructor() => 0;
                [return: System.Obsolete][Xunit.Fact]
                private int {|SST2509:ReturnTarget|}() => 0;
                [return: Xunit.Fact]
                private int {|SST2509:ReturnMarker|}() => 0;
            }
            """);

    /// <summary>Verifies framework markers on partial declarations can be matched despite attributes from another tree.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PartialMethodAttributesAcrossTreesAreClassifiedAsync()
    {
        var test = new VerifyTest.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90 };
        test.TestState.Sources.Add(XunitStubs + """

            public partial class Tests
            {
                [System.Obsolete] private partial void Case();
            }
            """);
        test.TestState.Sources.Add("""
            public partial class Tests
            {
                [Xunit.Fact] private partial void {|SST2509:Case|}() { }
            }
            """);
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies aliased marker names bind to the real framework attribute.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AliasQualifiedTestMarkerIsReportedAsync() =>
        VerifyAsync("""
            using X = Xunit;
            public class Tests
            {
                [X::FactAttribute] private void {|SST2509:Case|}() { }
            }
            """ + XunitStubs);

    /// <summary>Runs a verification against the .NET 9 reference assemblies with the source's own framework stubs.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyTest.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that ignores compiler diagnostics, for sources that intentionally do not resolve.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyIgnoringCompilerDiagnosticsAsync(string source)
    {
        var test = new VerifyTest.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None, };

        await test.RunAsync(CancellationToken.None);
    }
}
