// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyTest = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2505ParameterizedTestWithoutDataSourceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2505 (a parameterized test method that declares no data source).</summary>
public class ParameterizedTestWithoutDataSourceAnalyzerUnitTest
{
    /// <summary>Minimal xUnit attribute stubs, including the data-attribute base its data attributes derive from.</summary>
    private const string XunitStubs = """
        namespace Xunit
        {
            using System;
            public class FactAttribute : Attribute { }
            public class TheoryAttribute : FactAttribute { }
            namespace Sdk { public abstract class DataAttribute : Attribute { } }
            public sealed class InlineDataAttribute : Xunit.Sdk.DataAttribute { public InlineDataAttribute(params object[] data) { } }
            public sealed class MemberDataAttribute : Xunit.Sdk.DataAttribute { public MemberDataAttribute(string memberName) { } }
        }
        """;

    /// <summary>Minimal NUnit attribute stubs, including the test-builder and per-parameter data-source interfaces.</summary>
    private const string NUnitStubs = """
        namespace NUnit.Framework
        {
            using System;
            namespace Interfaces
            {
                public interface ITestBuilder { }
                public interface IParameterDataSource { }
            }
            public class TestAttribute : Attribute { }
            public class TheoryAttribute : Attribute { }
            public class TestCaseAttribute : Attribute, Interfaces.ITestBuilder { public TestCaseAttribute(params object[] args) { } }
            public class TestCaseSourceAttribute : Attribute, Interfaces.ITestBuilder { public TestCaseSourceAttribute(string sourceName) { } }
            public class ValuesAttribute : Attribute, Interfaces.IParameterDataSource { public ValuesAttribute(params object[] args) { } }
        }
        """;

    /// <summary>Minimal MSTest attribute stubs, including the test-data-source interface its row attributes implement.</summary>
    private const string MsTestStubs = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting
        {
            using System;
            public interface ITestDataSource { }
            public class TestMethodAttribute : Attribute { }
            public class DataTestMethodAttribute : TestMethodAttribute { }
            public sealed class DataRowAttribute : Attribute, ITestDataSource { public DataRowAttribute(params object[] data) { } }
        }
        """;

    /// <summary>Verifies an xUnit theory with a parameter and no data attribute is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitTheoryWithoutDataIsReportedAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Theory]
                public void {|SST2505:Case|}(int value) { }
            }
            """);

    /// <summary>Verifies an xUnit fact that mistakenly declares a parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitFactWithParameterIsReportedAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public void {|SST2505:Case|}(int value) { }
            }
            """);

    /// <summary>Verifies an xUnit theory that supplies inline data is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitTheoryWithInlineDataIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Theory]
                [Xunit.InlineData(1)]
                public void Case(int value) { }
            }
            """);

    /// <summary>Verifies an xUnit theory that supplies member data is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitTheoryWithMemberDataIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Theory]
                [Xunit.MemberData("Source")]
                public void Case(int value) { }
            }
            """);

    /// <summary>Verifies a parameterless xUnit fact is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task XunitParameterlessFactIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                [Xunit.Fact]
                public void Case() { }
            }
            """);

    /// <summary>Verifies an NUnit test with a parameter and no data source is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitTestWithoutDataIsReportedAsync()
        => await VerifyAsync(
            NUnitStubs + """

            public class Tests
            {
                [NUnit.Framework.Test]
                public void {|SST2505:Case|}(int value) { }
            }
            """);

    /// <summary>Verifies an NUnit test case supplying arguments on the method is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitTestCaseIsCleanAsync()
        => await VerifyAsync(
            NUnitStubs + """

            public class Tests
            {
                [NUnit.Framework.TestCase(1)]
                public void Case(int value) { }
            }
            """);

    /// <summary>Verifies an NUnit test whose parameter carries a per-parameter source is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitPerParameterValuesIsCleanAsync()
        => await VerifyAsync(
            NUnitStubs + """

            public class Tests
            {
                [NUnit.Framework.Test]
                public void Case([NUnit.Framework.Values(1, 2)] int value) { }
            }
            """);

    /// <summary>Verifies an MSTest data test method with a parameter and no row source is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MsTestDataTestMethodWithoutRowIsReportedAsync()
        => await VerifyAsync(
            MsTestStubs + """

            public class Tests
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethod]
                public void {|SST2505:Case|}(int value) { }
            }
            """);

    /// <summary>Verifies a plain MSTest test method with a parameter and no row source is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MsTestMethodWithParameterIsReportedAsync()
        => await VerifyAsync(
            MsTestStubs + """

            public class Tests
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.TestMethod]
                public void {|SST2505:Case|}(int value) { }
            }
            """);

    /// <summary>Verifies an MSTest data test method with a data row is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MsTestDataRowIsCleanAsync()
        => await VerifyAsync(
            MsTestStubs + """

            public class Tests
            {
                [Microsoft.VisualStudio.TestTools.UnitTesting.DataTestMethod]
                [Microsoft.VisualStudio.TestTools.UnitTesting.DataRow(1)]
                public void Case(int value) { }
            }
            """);

    /// <summary>Verifies an ordinary (non-test) method with parameters is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTestMethodIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public class Tests
            {
                public void Helper(int value) { }
            }
            """);

    /// <summary>Verifies a same-named attribute from an unrelated namespace is never treated as a test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LookalikeTestAttributeIsCleanAsync()
        => await VerifyAsync(
            XunitStubs + """

            public sealed class TheoryAttribute : System.Attribute { }

            public class Tests
            {
                [Theory]
                public void Case(int value) { }
            }
            """);

    /// <summary>Verifies nothing is reported when no test framework is referenced at all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoFrameworkReferencedIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public sealed class TheoryAttribute : Attribute { }

            public class Tests
            {
                [Theory]
                public void Case(int value) { }
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
}
