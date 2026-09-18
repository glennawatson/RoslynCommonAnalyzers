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
            public class TestAttribute : Attribute { public object ExpectedResult { get; set; } }
            [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
            public class TestCaseAttribute : Attribute
            {
                public TestCaseAttribute(params object[] arguments) { }
                public object ExpectedResult { get; set; }
            }
            public class TestCaseSourceAttribute : Attribute
            {
                public TestCaseSourceAttribute(string sourceName) { }
            }
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

    /// <summary>The cached NUnit references used to verify the framework's actual attribute contracts.</summary>
    private static readonly ReferenceAssemblies NUnitReferences = ReferenceAssemblies.Net.Net90.AddPackages([new PackageIdentity("NUnit", "4.2.2")]);

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

    /// <summary>Verifies NUnit compares value returns with each case's expected result, including false and null.</summary>
    /// <param name="returnType">The declared return type.</param>
    /// <param name="result">The expression returned and expected by the test case.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("bool", "true")]
    [Arguments("bool", "false")]
    [Arguments("string", "null")]
    [Arguments("int", "0")]
    public Task NUnitExpectedResultAcceptsValueReturnsAsync(string returnType, string result) =>
        VerifyAsync(NUnitStubs + $$"""

            public class Tests
            {
                [NUnit.Framework.TestCase("a.b", "a", ExpectedResult = {{result}})]
                [NUnit.Framework.TestCase("a.b", "c", ExpectedResult = {{result}})]
                public {{returnType}} Case(string path, string key) => {{result}};
            }
            """);

    /// <summary>Verifies NUnit's expected-result contracts against the framework assembly.</summary>
    /// <param name="attributes">The NUnit attributes that provide expected results.</param>
    /// <param name="declaration">The runnable method declaration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("[NUnit.Framework.Test(ExpectedResult = false)]", "public bool Case() => false;")]
    [Arguments("[NUnit.Framework.Test(ExpectedResult = null)]", "public string Case() => null;")]
    [Arguments("[NUnit.Framework.TestCase(0, ExpectedResult = false)]", "public bool Case(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCaseAttribute(0, ExpectedResult = null)]", "public string Case(int value) => null;")]
    [Arguments("[NUnit.Framework.TestCase(0, TestName = \"NamedCase\", ExpectedResult = false)]", "public bool Case(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCase(0, ExpectedResult = 0)]", "public T Case<T>(T value) => value;")]
    [Arguments("[NUnit.Framework.Test][NUnit.Framework.TestCase(0, ExpectedResult = false)]", "public static bool Case(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCase(0, ExpectedResult = false)]", "public System.Threading.Tasks.Task<bool> Case(int value) => System.Threading.Tasks.Task.FromResult(false);")]
    [Arguments("[NUnit.Framework.TestCase(0, ExpectedResult = false)]", "public System.Threading.Tasks.ValueTask<bool> Case(int value) => default;")]
    [Arguments("[NUnit.Framework.TestCaseSource(nameof(Cases))]", "public bool Case(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCase(0, ExpectedResult = false)][NUnit.Framework.TestCaseSource(nameof(Cases))]", "public bool Case(int value) => false;")]
    [Arguments("[NUnit.Framework.Theory][NUnit.Framework.TestCase(ExpectedResult = false)]", "public bool Case() => false;")]
    [Arguments("[NUnit.Framework.Theory][NUnit.Framework.TestCase(0, ExpectedResult = false)]", "public bool Case(int value) => false;")]
    [Arguments(
        "[NUnit.Framework.TestCase(false, false, ExpectedResult = false)]",
        "public bool Case([NUnit.Framework.Values(false, true)] bool first, [NUnit.Framework.ValueSource(nameof(Empty))] bool second) => false;")]
    public async Task NUnitResultContractsAreAcceptedAsync(string attributes, string declaration)
    {
        var test = new VerifyTest.Test
        {
            ReferenceAssemblies = NUnitReferences,
            TestCode = $$"""
                public class Tests
                {
                    public static NUnit.Framework.TestCaseData[] Cases => new[] { new NUnit.Framework.TestCaseData(0).Returns(false) };
                    public static bool[] Empty => new bool[0];
                    {{attributes}}
                    {{declaration}}
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies every NUnit test builder accepts the signature when expected-result cases are combined.</summary>
    /// <param name="attributes">The combined NUnit test builders.</param>
    /// <param name="parameterData">Optional data for independently generated theory cases.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("[NUnit.Framework.Test(ExpectedResult = false)][NUnit.Framework.TestCase(false, ExpectedResult = false)]", "")]
    [Arguments("[NUnit.Framework.TestCase(false, ExpectedResult = false)][NUnit.Framework.Test(ExpectedResult = false)]", "")]
    [Arguments("[NUnit.Framework.Test(ExpectedResult = false)][NUnit.Framework.TestCaseSource(nameof(Cases))]", "")]
    [Arguments("[NUnit.Framework.TestCaseSource(nameof(Cases))][NUnit.Framework.Test(ExpectedResult = false)]", "")]
    [Arguments("[NUnit.Framework.Theory][NUnit.Framework.TestCase(false, ExpectedResult = false)]", "[NUnit.Framework.Values(false, true)]")]
    [Arguments("[NUnit.Framework.TestCase(false, ExpectedResult = false)][NUnit.Framework.Theory]", "[NUnit.Framework.Values(false, true)]")]
    [Arguments("[NUnit.Framework.Theory][NUnit.Framework.TestCaseSource(nameof(Cases))]", "[NUnit.Framework.Values(false, true)]")]
    [Arguments("[NUnit.Framework.TestCaseSource(nameof(Cases))][NUnit.Framework.Theory]", "[NUnit.Framework.Values(false, true)]")]
    public async Task NUnitCombinedBuildersKeepReturnValidationAsync(string attributes, string parameterData)
    {
        var test = new VerifyTest.Test
        {
            ReferenceAssemblies = NUnitReferences,
            TestCode = $$"""
                public class Tests
                {
                    public static NUnit.Framework.TestCaseData[] Cases => new[] { new NUnit.Framework.TestCaseData(false).Returns(false) };
                    {{attributes}}
                    public bool {|SST2509:Case|}({{parameterData}} bool value) => value;
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies known parameter values create result-less cases while runtime data sources remain deferred.</summary>
    /// <param name="marker">The optional theory marker.</param>
    /// <param name="parameterData">The attribute supplying parameter data.</param>
    /// <param name="reports">Whether generated cases are known to lack expected results.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("", "[NUnit.Framework.Values(false, true)]", true)]
    [Arguments("", "[NUnit.Framework.Values]", true)]
    [Arguments("", "[NUnit.Framework.Values(false)]", true)]
    [Arguments("", "[NUnit.Framework.Values(new object[] { false, true })]", true)]
    [Arguments("", "[NUnit.Framework.Values(new object[0])]", true)]
    [Arguments("", "[NUnit.Framework.Values((object[])null)]", true)]
    [Arguments("[NUnit.Framework.Theory]", "", true)]
    [Arguments("", "[NUnit.Framework.ValueSource(nameof(Empty))]", false)]
    [Arguments("", "[CustomData]", false)]
    [Arguments("", "[Values(false, true)]", false)]
    public async Task NUnitParameterDataPreservesKnownReturnContractsAsync(string marker, string parameterData, bool reports)
    {
        var name = reports ? "{|SST2509:Case|}" : "Case";
        var test = new VerifyTest.Test
        {
            ReferenceAssemblies = NUnitReferences,
            TestCode = $$"""
                public class CustomDataAttribute : System.Attribute, NUnit.Framework.Interfaces.IParameterDataSource
                {
                    public System.Collections.IEnumerable GetData(NUnit.Framework.Interfaces.IParameterInfo parameter) => new object[0];
                }
                public class ValuesAttribute : System.Attribute { public ValuesAttribute(params object[] values) { } }
                public class Tests
                {
                    public static bool[] Empty => new bool[0];
                    {{marker}}
                    [NUnit.Framework.TestCase(false, ExpectedResult = false)]
                    public bool {{name}}({{parameterData}} bool value) => value;
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies automatic data is nonempty for nullable booleans and enums with declared values.</summary>
    /// <param name="marker">The optional theory marker.</param>
    /// <param name="parameterData">The optional values attribute.</param>
    /// <param name="parameterType">The type NUnit uses to generate data.</param>
    /// <param name="argument">The argument for the explicit expected-result case.</param>
    /// <param name="reports">Whether automatic data creates additional result-less cases.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("[NUnit.Framework.Theory]", "", "bool?", "null", true)]
    [Arguments("", "[NUnit.Framework.Values]", "bool?", "null", true)]
    [Arguments("[NUnit.Framework.Theory]", "", "Choice", "Choice.First", true)]
    [Arguments("", "[NUnit.Framework.Values]", "Choice", "Choice.First", true)]
    [Arguments("[NUnit.Framework.Theory]", "", "Choice?", "null", true)]
    [Arguments("", "[NUnit.Framework.Values]", "Choice?", "null", true)]
    [Arguments("[NUnit.Framework.Theory]", "", "Empty", "0", false)]
    [Arguments("", "[NUnit.Framework.Values]", "Empty", "0", false)]
    [Arguments("[NUnit.Framework.Theory]", "", "Empty?", "null", false)]
    [Arguments("", "[NUnit.Framework.Values]", "int", "0", false)]
    public async Task NUnitAutomaticDataRequiresNonemptyTypeValuesAsync(string marker, string parameterData, string parameterType, string argument, bool reports)
    {
        var name = reports ? "{|SST2509:Case|}" : "Case";
        var test = new VerifyTest.Test
        {
            ReferenceAssemblies = NUnitReferences,
            TestCode = $$"""
                public enum Choice { First, Second }
                public enum Empty { }
                public class Tests
                {
                    {{marker}}
                    [NUnit.Framework.TestCase({{argument}}, ExpectedResult = false)]
                    public bool {{name}}({{parameterData}} {{parameterType}} value) => false;
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies invalid values arguments do not imply generated cases while the source is being edited.</summary>
    /// <param name="argument">An argument that is nonconstant or names a nonexistent constructor parameter.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("Input")]
    [Arguments("missing: true")]
    public async Task NUnitInvalidValuesArgumentsRemainDeferredAsync(string argument)
    {
        var test = new VerifyTest.Test
        {
            ReferenceAssemblies = NUnitReferences,
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = $$"""
                public class Tests
                {
                    public static bool Input => false;
                    [NUnit.Framework.TestCase(false, ExpectedResult = false)]
                    public bool Case([NUnit.Framework.Values({{argument}})] bool value) => value;
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies NUnit result data does not excuse a known invalid signature or a case missing its expected result.</summary>
    /// <param name="attributes">The test attributes.</param>
    /// <param name="declaration">The unsupported method declaration.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("[NUnit.Framework.TestCase(0, ExpectedResult = false)]", "private bool {|SST2509:Case|}(int value) => false;")]
    [Arguments("[NUnit.Framework.Test(ExpectedResult = false)]", "internal bool {|SST2509:Case|}() => false;")]
    [Arguments("[NUnit.Framework.TestCase(ExpectedResult = false)]", "public bool {|SST2509:Case|}<T>() => false;")]
    [Arguments("[NUnit.Framework.TestCaseSource(\"Cases\")]", "private bool {|SST2509:Case|}(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCaseSource(\"Cases\")]", "public bool {|SST2509:Case|}<T>() => false;")]
    [Arguments("[NUnit.Framework.TestCase(0)]", "public bool {|SST2509:Case|}(int value) => false;")]
    [Arguments("[NUnit.Framework.Test]", "public bool {|SST2509:Case|}() => false;")]
    [Arguments("[NUnit.Framework.Test(ExpectedResult = false)]", "public bool {|SST2509:Case|}(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCase(0, ExpectedResult = false)][NUnit.Framework.TestCase(1)]", "public bool {|SST2509:Case|}(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCase(0)][NUnit.Framework.TestCase(1, ExpectedResult = false)]", "public bool {|SST2509:Case|}(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCase(0)][NUnit.Framework.TestCaseSource(\"Cases\")]", "public bool {|SST2509:Case|}(int value) => false;")]
    [Arguments("[NUnit.Framework.TestCaseSource(\"Cases\")][NUnit.Framework.TestCase(0)]", "public bool {|SST2509:Case|}(int value) => false;")]
    public Task NUnitResultDataKeepsSignatureValidationAsync(string attributes, string declaration) =>
        VerifyAsync(NUnitStubs + $$"""

            public class Tests
            {
                {{attributes}}
                {{declaration}}
            }
            """);

    /// <summary>Verifies expected results on unrelated attributes cannot legalize value returns in other frameworks.</summary>
    /// <param name="marker">The framework's actual test marker.</param>
    /// <param name="expectedResultAttribute">The unrelated attribute carrying a similarly named property.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [MatrixDataSource]
    public Task NonNUnitResultAttributesKeepReturnValidationAsync(
        [Matrix("Xunit.Fact", "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod", "TUnit.Core.Test", "NUnit.Framework.Test")] string marker,
        [Matrix("Foreign.TestCase", "Foreign.Test", "Foreign.TestCaseSource")] string expectedResultAttribute) =>
        VerifyAsync(XunitStubs + MsTestStubs + TUnitStubs + NUnitStubs + $$"""

            namespace Foreign
            {
                public class TestCaseAttribute : System.Attribute { public object ExpectedResult { get; set; } }
                public class TestAttribute : System.Attribute { public object ExpectedResult { get; set; } }
                public class TestCaseSourceAttribute : System.Attribute { public object ExpectedResult { get; set; } }
            }
            public class Tests
            {
                [{{marker}}]
                [{{expectedResultAttribute}}(ExpectedResult = false)]
                public bool {|SST2509:Case|}() => false;
            }
            """);

    /// <summary>Verifies a NUnit expected result cannot bypass a second framework's return-type requirement.</summary>
    /// <param name="marker">The additional framework's test marker.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Xunit.Fact")]
    [Arguments("Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod")]
    [Arguments("TUnit.Core.Test")]
    public Task MixedFrameworkValueReturnsAreReportedAsync(string marker) =>
        VerifyAsync(XunitStubs + MsTestStubs + TUnitStubs + NUnitStubs + $$"""

            public class Tests
            {
                [NUnit.Framework.TestCase(ExpectedResult = false)]
                [{{marker}}]
                public bool {|SST2509:Case|}() => false;
            }
            """);

    /// <summary>Verifies namespace aliases and derived NUnit case markers keep their result contracts.</summary>
    /// <param name="attributes">The recognized NUnit case attributes.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("[N::TestCaseAttribute(0, ExpectedResult = false)]")]
    [Arguments("[N::Test][CustomCase(ExpectedResult = false)]")]
    public Task NUnitCaseAliasesAndDerivedMarkersAreCleanAsync(string attributes) =>
        VerifyAsync($$"""
            using N = NUnit.Framework;
            public class CustomCaseAttribute : NUnit.Framework.TestCaseAttribute { }
            public class Tests
            {
                {{attributes}}
                public bool Case(int value) => false;
            }
            """ + NUnitStubs);

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

    /// <summary>Verifies unmatched return-target attributes with failed constructor binding do not mark a test.</summary>
    /// <param name="attribute">An unresolved attribute name or an invalid constructor invocation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Xunit.Fact(1)")]
    [Arguments("Fact")]
    public Task InvalidReturnAttributeConstructorsRemainQuietAsync(string attribute) =>
        VerifyIgnoringCompilerDiagnosticsAsync(XunitStubs + $$"""

            public class Tests
            {
                [return: {{attribute}}]
                private int Case() => 0;
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
