// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    [Test]
    public async Task XunitPrivateFactIsReportedAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                private void {|SST2509:Case|}() { }
            }
            """);

    /// <summary>Verifies an internal xUnit theory is reported, exercising the derived-attribute marker walk.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitInternalTheoryIsReportedAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Theory]
                internal void {|SST2509:Case|}() { }
            }
            """);

    /// <summary>Verifies a non-public NUnit test is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitPrivateTestIsReportedAsync()
        => await VerifyAsync(
            NUnitStubs + """

            public class Tests
            {
                [NUnit.Framework.Test]
                private void {|SST2509:Case|}() { }
            }
            """);

    /// <summary>Verifies a non-public MSTest test method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MsTestPrivateMethodIsReportedAsync()
        => await VerifyAsync(
            MsTestStubs + """

            public class Tests
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                private void {|SST2509:Case|}() { }
            }
            """);

    /// <summary>Verifies a parameterless generic xUnit fact is reported, because its type argument cannot be inferred.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitGenericParameterlessFactIsReportedAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public void {|SST2509:Case|}<T>() { }
            }
            """);

    /// <summary>Verifies a parameterless generic TUnit test is reported even though TUnit is exempt from the public check.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TUnitGenericParameterlessTestIsReportedAsync()
        => await VerifyAsync(
            TUnitStubs + """

            public class Tests
            {
                [TUnit.Core.Test]
                public void {|SST2509:Case|}<T>() { }
            }
            """);

    /// <summary>Verifies an xUnit fact that returns a non-awaitable value type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitFactReturningIntIsReportedAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public int {|SST2509:Case|}() => 0;
            }
            """);

    /// <summary>Verifies an NUnit test that returns a non-awaitable reference type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitTestReturningStringIsReportedAsync()
        => await VerifyAsync(
            NUnitStubs + """

            public class Tests
            {
                [NUnit.Framework.Test]
                public string {|SST2509:Case|}() => "";
            }
            """);

    /// <summary>Verifies a public, non-generic, void-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PublicVoidFactIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public void Case() { }
            }
            """);

    /// <summary>Verifies a static public, void-returning xUnit fact is never reported, because static is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticPublicVoidFactIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public static void Case() { }
            }
            """);

    /// <summary>Verifies a Task-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitFactReturningTaskIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public System.Threading.Tasks.Task Case() => System.Threading.Tasks.Task.CompletedTask;
            }
            """);

    /// <summary>Verifies a ValueTask-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitFactReturningValueTaskIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public System.Threading.Tasks.ValueTask Case() => default;
            }
            """);

    /// <summary>Verifies a Task&lt;T&gt;-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitFactReturningTaskOfIntIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public System.Threading.Tasks.Task<int> Case() => System.Threading.Tasks.Task.FromResult(0);
            }
            """);

    /// <summary>Verifies a ValueTask&lt;T&gt;-returning xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitFactReturningValueTaskOfIntIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public System.Threading.Tasks.ValueTask<int> Case() => default;
            }
            """);

    /// <summary>Verifies a non-public TUnit test is never reported, because TUnit does not universally require public methods.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TUnitPrivateTestIsCleanAsync()
        => await VerifyAsync(
            TUnitStubs + """

            public class Tests
            {
                [TUnit.Core.Test]
                private void Case() { }
            }
            """);

    /// <summary>Verifies a generic test method that declares parameters is not reported for shape, leaving data-source concerns to another rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericFactWithParametersIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public void Case<T>(T value) { }
            }
            """);

    /// <summary>Verifies an ordinary method that carries no test attribute is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTestMethodIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [System.Obsolete]
                private int Helper() => 0;
            }
            """);

    /// <summary>Verifies a same-named attribute from an unrelated namespace on a suspect-shaped method is never treated as a test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LookalikeTestAttributeIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task NoFrameworkReferencedIsCleanAsync()
        => await VerifyAsync(
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
    [Test]
    public async Task UnresolvedReturnTypeIsCleanAsync()
        => await VerifyIgnoringCompilerDiagnosticsAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public Undefined Case() => default;
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies with the source's own framework stubs.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyTest.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification that ignores compiler diagnostics, for sources that intentionally do not resolve.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyIgnoringCompilerDiagnosticsAsync(string source)
    {
        var test = new VerifyTest.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
