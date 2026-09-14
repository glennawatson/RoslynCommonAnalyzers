// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2506ThreadSleepInTestAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2506 (a <c>Thread.Sleep</c> inside a test method body).</summary>
public class ThreadSleepInTestAnalyzerUnitTest
{
    /// <summary>Verifies near-miss marker names with matching lengths and prefixes are not test markers.</summary>
    /// <param name="markerName">The misspelled marker name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Abcd")]
    [Arguments("AbcdAttribute")]
    [Arguments("XestCaseSource")]
    [Arguments("XestCaseSourceAttribute")]
    [Arguments("Facu")]
    [Arguments("Tesu")]
    [Arguments("Theorz")]
    [Arguments("TestCasx")]
    [Arguments("TestCaseSourcx")]
    [Arguments("TestMethox")]
    [Arguments("DataTestMethox")]
    [Arguments("FactAttributx")]
    [Arguments("TestAttributx")]
    [Arguments("TheoryAttributx")]
    [Arguments("TestCaseAttributx")]
    [Arguments("TestCaseSourceAttributx")]
    [Arguments("TestMethodAttributx")]
    [Arguments("DataTestMethodAttributx")]
    public Task MisspelledMarkerNamesAreCleanAsync(string markerName) =>
        VerifyAsync(
            $$"""
            namespace Mine { public sealed class {{markerName}} : System.Attribute { } }
            public class C
            {
                [Mine.{{markerName}}]
                public void M() { System.Threading.Thread.Sleep(1); }
            }
            """);

    /// <summary>Verifies every supported marker works with both its short and suffixed spelling.</summary>
    /// <param name="markerNamespace">The framework namespace.</param>
    /// <param name="markerName">The marker name without its attribute suffix.</param>
    /// <param name="suffix">The suffix used at the attribute application.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Xunit", "Fact", "")]
    [Arguments("Xunit", "Fact", "Attribute")]
    [Arguments("Xunit", "Theory", "")]
    [Arguments("Xunit", "Theory", "Attribute")]
    [Arguments("NUnit.Framework", "Test", "")]
    [Arguments("NUnit.Framework", "Test", "Attribute")]
    [Arguments("NUnit.Framework", "TestCase", "")]
    [Arguments("NUnit.Framework", "TestCase", "Attribute")]
    [Arguments("NUnit.Framework", "TestCaseSource", "")]
    [Arguments("NUnit.Framework", "TestCaseSource", "Attribute")]
    [Arguments("NUnit.Framework", "Theory", "")]
    [Arguments("NUnit.Framework", "Theory", "Attribute")]
    [Arguments("Microsoft.VisualStudio.TestTools.UnitTesting", "TestMethod", "")]
    [Arguments("Microsoft.VisualStudio.TestTools.UnitTesting", "TestMethod", "Attribute")]
    [Arguments("Microsoft.VisualStudio.TestTools.UnitTesting", "DataTestMethod", "")]
    [Arguments("Microsoft.VisualStudio.TestTools.UnitTesting", "DataTestMethod", "Attribute")]
    [Arguments("TUnit.Core", "Test", "")]
    [Arguments("TUnit.Core", "Test", "Attribute")]
    public Task SupportedMarkerSpellingsAreFlaggedAsync(string markerNamespace, string markerName, string suffix) =>
        VerifyAsync(
            $$"""
            using Markers = {{markerNamespace}};
            namespace {{markerNamespace}} { public sealed class {{markerName}}Attribute : System.Attribute { } }
            public class C
            {
                [Markers::{{markerName}}{{suffix}}]
                public void M() => {|SST2506:System.Threading.Thread.Sleep(100)|};
            }
            """);

    /// <summary>Verifies test declarations without a body are skipped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AbstractTestMethodIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            public abstract class C
            {
                [Xunit.Fact]
                public abstract void M();
            }
            """);

    /// <summary>Verifies all nested sleeps are reported after the enclosing test has been identified.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LambdaLocalFunctionAndLoopSleepsAreFlaggedAsync() =>
        VerifyAsync(
            """
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            public class C
            {
                [Xunit.Fact]
                public void M()
                {
                    System.Action action = () => {|SST2506:System.Threading.Thread.Sleep(1)|};
                    void Local() { {|SST2506:System.Threading.Thread.Sleep(2)|}; }
                    for (var i = 0; i < 1; i++) { {|SST2506:System.Threading.Thread.Sleep(3)|}; }
                    action();
                    Local();
                }

                [Xunit.Fact]
                public void N() => {|SST2506:System.Threading.Thread.Sleep(4)|};
            }
            """);

    /// <summary>Verifies unrelated attributes and same-named user markers do not hide a later real marker.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LaterFrameworkMarkerIsRecognizedAsync() =>
        VerifyAsync(
            """
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            namespace Mine { public sealed class FactAttribute : System.Attribute { } }
            public class C
            {
                [System.Obsolete, Mine.Fact]
                [Xunit.Fact]
                public void M() { {|SST2506:System.Threading.Thread.Sleep(1)|}; }
            }
            """);

    /// <summary>Verifies unrelated attributes and unsupported derived-marker spellings remain silent.</summary>
    /// <param name="attribute">The attribute applied to a sleeping method.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("System.Obsolete")]
    [Arguments("Custom")]
    public Task UnsupportedAttributeNamesAreCleanAsync(string attribute) =>
        VerifyAsync(
            $$"""
            namespace Xunit { public class FactAttribute : System.Attribute { } }
            public class CustomAttribute : Xunit.FactAttribute { }
            public class C
            {
                [{{attribute}}]
                public void M() { System.Threading.Thread.Sleep(1); }
            }
            """);

    /// <summary>Verifies marker-shaped user attributes remain silent when their framework is absent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedMarkerWithoutFrameworkIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Mine
            {
                public sealed class FactAttribute : System.Attribute { }
                public sealed class TestAttribute : System.Attribute { }
            }
            public class C
            {
                [Mine.Fact, Mine.Test]
                public void M() { System.Threading.Thread.Sleep(1); }
            }
            """);

    /// <summary>Verifies delegate, conditional, generic and differently named calls are not sleeps.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OtherInvocationShapesAreCleanAsync() =>
        VerifyAsync(
            """
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            public class C
            {
                public void Sleep() { }
                public static void Sleep<T>() { }
                private static void Other() { }

                [Xunit.Fact]
                public void M(C instance, System.Action[] callbacks)
                {
                    Other();
                    Sleep<int>();
                    instance?.Sleep();
                    callbacks[0]();
                }
            }
            """);

    /// <summary>Verifies a delegate named Sleep binds to Invoke rather than the framework method.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DelegateNamedSleepIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            public class C
            {
                [Xunit.Fact]
                public void M(System.Action Sleep) { Sleep(); }
            }
            """);

    /// <summary>Verifies incomplete attribute and invocation bindings cannot produce diagnostics.</summary>
    /// <param name="source">The malformed or dynamically bound test source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("class C { [Fact] void M() { System.Threading.Thread.Sleep(1); } }")]
    [Arguments("class C { [Xunit.Fact] void M() { Missing.Sleep(1); } }")]
    [Arguments("class C { [Xunit.Fact] void M(dynamic target) { target.Sleep(1); } }")]
    [Arguments("class C { [Xunit.Fact] void M() { System.Threading.Thread.Sleep(); } }")]
    public Task UnresolvedBindingsAreCleanAsync(string source) =>
        new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $$"""{{source}} namespace Xunit { public sealed class FactAttribute : System.Attribute { } }""",
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a generic Thread with the expected namespace is distinct from the framework type.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task GenericThreadLookalikeIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            namespace System.Threading { public static class Thread<T> { public static void Sleep(int delay) { } } }
            public class C
            {
                [Xunit.Fact]
                public void M()
                {
                    System.Threading.Thread<int>.Sleep(1);
                    {|SST2506:System.Threading.Thread.Sleep(2)|};
                }
            }
            """);

    /// <summary>Verifies a same-named Thread type must belong to the exact framework namespace.</summary>
    /// <param name="typeNamespace">The namespace containing a user-defined Thread.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Other")]
    [Arguments("Threading")]
    [Arguments("System.Other")]
    [Arguments("Other.Threading")]
    [Arguments("Outer.System.Threading")]
    public Task ThreadInDifferentNamespaceIsCleanAsync(string typeNamespace) =>
        VerifyAsync(
            $$"""
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            namespace {{typeNamespace}} { public static class Thread { public static void Sleep(int delay) { } } }
            public class C
            {
                [Xunit.Fact]
                public void M() { {{typeNamespace}}.Thread.Sleep(1); }
            }
            """);

    /// <summary>Verifies a Thread type in the global namespace is not a framework sleep.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThreadInGlobalNamespaceIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            public static class Thread { public static void Sleep(int delay) { } }
            public class C
            {
                [Xunit.Fact]
                public void M() { Thread.Sleep(1); }
            }
            """);

    /// <summary>Verifies a bound generic Thread candidate is skipped when the nongeneric framework type is absent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingFrameworkThreadIsCleanAsync()
    {
        const string Source = """
            namespace System
            {
                public class Object { }
                public class Attribute { }
                public struct Void { }
                public struct Int32 { }
            }
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            namespace System.Threading { public static class Thread<T> { public static void Sleep(int delay) { } } }
            public class C
            {
                [Xunit.Fact]
                public void M() { System.Threading.Thread<int>.Sleep(1); }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(Source);
        var compilation = CSharpCompilation.Create(nameof(MissingFrameworkThreadIsCleanAsync), [tree]);
        var root = await tree.GetRootAsync();
        var invocation = root.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var symbol = compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol;
        await Assert.That(symbol is IMethodSymbol).IsTrue();
        await Assert.That(compilation.GetTypeByMetadataName("System.Threading.Thread")).IsNull();
        var diagnostics = await compilation.WithAnalyzers([new Sst2506ThreadSleepInTestAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies cached thread and marker symbols are reused for later methods in one compilation.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MultipleTestMethodsReuseResolvedSymbolsAsync()
    {
        const int ExpectedDiagnosticCount = 2;
        const string Source = """
            namespace Xunit { public sealed class FactAttribute : System.Attribute { } }
            public class C
            {
                [Xunit.Fact]
                public void M() { System.Threading.Thread.Sleep(1); }
                [Xunit.Fact]
                public void N() { System.Threading.Thread.Sleep(2); }
            }
            """;
        var compilation = CSharpCompilation.Create(
            nameof(MultipleTestMethodsReuseResolvedSymbolsAsync),
            [CSharpSyntaxTree.ParseText(Source)],
            RuntimeMetadataReferences.Platform);
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false);
        var diagnostics = await compilation.WithAnalyzers([new Sst2506ThreadSleepInTestAnalyzer()], options).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(ExpectedDiagnosticCount);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Id == "SST2506")).IsTrue();
    }

    /// <summary>Verifies a fully-qualified <c>Thread.Sleep</c> in an xUnit fact is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThreadSleepInXunitFactIsFlaggedAsync() =>
        VerifyAsync(
            """
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Fact]
                public void M()
                {
                    {|SST2506:System.Threading.Thread.Sleep(100)|};
                }
            }
            """);

    /// <summary>Verifies a <c>Thread.Sleep</c> nested inside an <c>if</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThreadSleepNestedInIfIsFlaggedAsync() =>
        VerifyAsync(
            """
            using System.Threading;
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Fact]
                public void M(bool wait)
                {
                    if (wait)
                    {
                        {|SST2506:Thread.Sleep(100)|};
                    }
                }
            }
            """);

    /// <summary>Verifies a <c>Sleep</c> reached through <c>using static Thread</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UsingStaticSleepIsFlaggedAsync() =>
        VerifyAsync(
            """
            using static System.Threading.Thread;
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Fact]
                public void M()
                {
                    {|SST2506:Sleep(100)|};
                }
            }
            """);

    /// <summary>Verifies a <c>Thread.Sleep</c> in an NUnit test is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThreadSleepInNUnitTestIsFlaggedAsync() =>
        VerifyAsync(
            """
            using NUnit.Framework;

            namespace NUnit.Framework { using System; public sealed class TestAttribute : Attribute { } }

            public class C
            {
                [Test]
                public void M()
                {
                    {|SST2506:System.Threading.Thread.Sleep(100)|};
                }
            }
            """);

    /// <summary>Verifies an attribute keeping a known spelling but deriving from a marker is treated as a test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DerivedTestAttributeIsFlaggedAsync() =>
        VerifyAsync(
            """
            namespace Xunit { using System; public class FactAttribute : Attribute { } }

            namespace Suite { public class FactAttribute : Xunit.FactAttribute { } }

            public class C
            {
                [Suite.Fact]
                public void M()
                {
                    {|SST2506:System.Threading.Thread.Sleep(100)|};
                }
            }
            """);

    /// <summary>Verifies a test method with no sleep is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TestWithoutSleepIsCleanAsync() =>
        VerifyAsync(
            """
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Fact]
                public void M()
                {
                    System.GC.KeepAlive(this);
                }
            }
            """);

    /// <summary>Verifies a <c>Thread.Sleep</c> outside any test method is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThreadSleepInNonTestMethodIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                public void Helper()
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            """);

    /// <summary>Verifies a same-named user <c>Sleep</c> method called from a test is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UserDefinedSleepInTestIsCleanAsync() =>
        VerifyAsync(
            """
            using Xunit;

            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                private static void Sleep(int milliseconds) { }

                [Fact]
                public void M()
                {
                    Sleep(100);
                }
            }
            """);

    /// <summary>Verifies a same-named user attribute that is not a marker leaves a test's sleep alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameNamedUserFactAttributeIsCleanAsync() =>
        VerifyAsync(
            """
            namespace Xunit { using System; public sealed class FactAttribute : Attribute { } }

            namespace Mine { using System; public sealed class FactAttribute : Attribute { } }

            public class C
            {
                [Mine.Fact]
                public void M()
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            """);

    /// <summary>Verifies nothing is reported when no test framework is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NoTestFrameworkIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M()
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            """);

    /// <summary>Runs analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
