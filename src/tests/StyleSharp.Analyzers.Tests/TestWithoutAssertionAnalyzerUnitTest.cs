// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyKey = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2500TestWithoutAssertionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2500 (a test method that asserts nothing).</summary>
public class TestWithoutAssertionAnalyzerUnitTest
{
    /// <summary>Minimal xUnit attribute stubs the analyzer resolves by name.</summary>
    private const string XunitStubs = """

                                      namespace Xunit
                                      {
                                          public sealed class FactAttribute : System.Attribute { }

                                          public sealed class TheoryAttribute : System.Attribute { }
                                      }
                                      """;

    /// <summary>Minimal NUnit attribute stubs the analyzer resolves by name.</summary>
    private const string NUnitStubs = """

                                      namespace NUnit.Framework
                                      {
                                          public sealed class TestAttribute : System.Attribute { }
                                      }
                                      """;

    /// <summary>Minimal MSTest attribute stubs, including the expected-exception family.</summary>
    private const string MsTestStubs = """

                                       namespace Microsoft.VisualStudio.TestTools.UnitTesting
                                       {
                                           public sealed class TestMethodAttribute : System.Attribute { }

                                           public abstract class ExpectedExceptionBaseAttribute : System.Attribute { }

                                           public sealed class ExpectedExceptionAttribute : ExpectedExceptionBaseAttribute
                                           {
                                               public ExpectedExceptionAttribute(System.Type exceptionType) { }
                                           }
                                       }
                                       """;

    /// <summary>Minimal TUnit attribute stubs the analyzer resolves by name.</summary>
    private const string TUnitStubs = """

                                      namespace TUnit.Core
                                      {
                                          public sealed class TestAttribute : System.Attribute { }
                                      }
                                      """;

    /// <summary>A framework-shaped assertion type defined in the analyzed source.</summary>
    private const string AssertStub = """

                                      public static class Assert
                                      {
                                          public static void Equal(object expected, object actual) { }
                                      }
                                      """;

    /// <summary>Verifies an empty xUnit test body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyFactBodyIsReportedAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void {|SST2500:DoesNothing|}()
                {
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies a body that only computes locals — no call verifies anything — is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactWithOnlyLocalsIsReportedAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void {|SST2500:Computes|}()
                {
                    var x = 1 + 1;
                    _ = x;
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies a body whose only call is a non-verifying platform call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactWithOnlyBclCallIsReportedAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void {|SST2500:WritesToConsole|}()
                {
                    System.Console.WriteLine("hi");
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies a body that only creates and mutates a platform object is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactWithBclObjectCreationIsReportedAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void {|SST2500:BuildsString|}()
                {
                    var builder = new System.Text.StringBuilder();
                    builder.Append("x");
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies an empty NUnit test body is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NUnitTestIsReportedAsync()
        => await VerifyReportAsync(
            """
            using NUnit.Framework;

            public class Tests
            {
                [Test]
                public void {|SST2500:DoesNothing|}()
                {
                }
            }
            """ + NUnitStubs);

    /// <summary>Verifies an empty MSTest test method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MsTestMethodIsReportedAsync()
        => await VerifyReportAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                [TestMethod]
                public void {|SST2500:DoesNothing|}()
                {
                }
            }
            """ + MsTestStubs);

    /// <summary>Verifies an empty TUnit test is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TUnitTestIsReportedAsync()
        => await VerifyReportAsync(
            """
            using TUnit.Core;

            public class Tests
            {
                [Test]
                public void {|SST2500:DoesNothing|}()
                {
                }
            }
            """ + TUnitStubs);

    /// <summary>Verifies a test whose body asserts through a framework-shaped call is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactWithFrameworkAssertIsSilentAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void Asserts()
                {
                    Assert.Equal(2, 1 + 1);
                }
            }
            """ + AssertStub + XunitStubs);

    /// <summary>Verifies a test that calls a user-defined helper — a possible assertion helper — is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactCallingUserHelperIsSilentAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public static class Check
            {
                public static void Invariants() { }
            }

            public class Tests
            {
                [Fact]
                public void UsesHelper()
                {
                    Check.Invariants();
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies a test that constructs a user-defined type is silent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactConstructingUserTypeIsSilentAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public sealed class Thing
            {
            }

            public class Tests
            {
                [Fact]
                public void ConstructsUserType()
                {
                    _ = new Thing();
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies a pending test that throws is silent — it does not pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactWithThrowIsSilentAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public class Tests
            {
                [Fact]
                public void Pending()
                {
                    throw new System.NotImplementedException();
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies a test that declares an expected exception is silent — it verifies by exception.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpectedExceptionMethodIsSilentAsync()
        => await VerifyReportAsync(
            """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            public class Tests
            {
                [TestMethod]
                [ExpectedException(typeof(System.InvalidOperationException))]
                public void ExpectsThrow()
                {
                }
            }
            """ + MsTestStubs);

    /// <summary>Verifies a method with no test attribute is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonTestMethodIsSilentAsync()
        => await VerifyReportAsync(
            """
            using Xunit;

            public class Tests
            {
                public void Ordinary()
                {
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies a same-named attribute from an unrecognized namespace is never treated as a test.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DecoyAttributeWithMatchingNameIsSilentAsync()
        => await VerifyReportAsync(
            """
            using Probe;

            namespace Probe
            {
                public sealed class FactAttribute : System.Attribute { }
            }

            public class Tests
            {
                [Fact]
                public void LooksLikeATest()
                {
                }
            }
            """ + XunitStubs);

    /// <summary>Verifies the rule is silent when no supported test framework is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoFrameworkReferencedIsSilentAsync()
    {
        const string Source = """
                              using Probe;

                              namespace Probe
                              {
                                  public sealed class FactAttribute : System.Attribute { }
                              }

                              public class Tests
                              {
                                  [Fact]
                                  public void LooksLikeATest()
                                  {
                                  }
                              }
                              """;

        var test = new VerifyKey.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportAsync(string source)
    {
        var test = new VerifyKey.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
