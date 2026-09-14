// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyFactBodyIsReportedAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactWithOnlyLocalsIsReportedAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactWithOnlyBclCallIsReportedAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactWithBclObjectCreationIsReportedAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NUnitTestIsReportedAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MsTestMethodIsReportedAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TUnitTestIsReportedAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactWithFrameworkAssertIsSilentAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactCallingUserHelperIsSilentAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactConstructingUserTypeIsSilentAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FactWithThrowIsSilentAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExpectedExceptionMethodIsSilentAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonTestMethodIsSilentAsync() =>
        VerifyReportAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DecoyAttributeWithMatchingNameIsSilentAsync() =>
        VerifyReportAsync(
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

    /// <summary>Verifies a framework attribute on an abstract method has no body to inspect.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AbstractTestMethodIsSilentAsync() =>
        VerifyReportAsync("""
            public abstract class Tests
            {
                [Xunit.Fact]
                public abstract void Pending();
            }
            """ + XunitStubs);

    /// <summary>Verifies qualified, alias-qualified and suffixed framework attributes are recognized.</summary>
    /// <param name="attribute">The spelling of the test attribute.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Xunit.Fact")]
    [Arguments("Xunit.FactAttribute")]
    [Arguments("Alias::Fact")]
    [Arguments("Alias::TheoryAttribute")]
    public Task QualifiedTestAttributeIsReportedAsync(string attribute) =>
        VerifyReportAsync($$"""
            using Alias = Xunit;
            public class Tests
            {
                [{{attribute}}]
                public int {|SST2500:Computes|}() => 1 + 1;
            }
            """ + XunitStubs);

    /// <summary>Verifies every NUnit and data-driven MSTest marker activates the rule.</summary>
    /// <param name="frameworkNamespace">The recognized framework namespace.</param>
    /// <param name="attribute">The recognized marker's simple name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("NUnit.Framework", "TestCase")]
    [Arguments("NUnit.Framework", "TestCaseSource")]
    [Arguments("NUnit.Framework", "Theory")]
    [Arguments("Microsoft.VisualStudio.TestTools.UnitTesting", "DataTestMethod")]
    public Task DataDrivenFrameworkMarkerIsReportedAsync(string frameworkNamespace, string attribute) =>
        VerifyReportAsync($$"""
            public class Tests
            {
                [{{frameworkNamespace}}.{{attribute}}]
                public void {|SST2500:Empty|}() { }
            }
            namespace {{frameworkNamespace}}
            {
                public sealed class {{attribute}}Attribute : System.Attribute { }
            }
            """);

    /// <summary>Verifies all attribute lists are scanned while unrelated markers do not make a method a test.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MixedAttributeListsStillFindTestMarkerAsync() =>
        VerifyReportAsync("""
            public class MarkerAttribute : System.Attribute { }
            public class Tests
            {
                [Marker]
                [Xunit.Fact, Xunit.Theory]
                public void {|SST2500:Empty|}() { }
                [Marker]
                public void Ordinary() { }
            }
            """ + XunitStubs);

    /// <summary>Verifies similarly spelled attributes do not pass the syntactic test-marker check.</summary>
    /// <param name="attribute">The unrecognized attribute name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Fast")]
    [Arguments("Themed")]
    [Arguments("Task")]
    [Arguments("Note")]
    [Arguments("TestData")]
    [Arguments("TestDataSource")]
    [Arguments("TestAction")]
    [Arguments("DataTestAction")]
    [Arguments("OtherTestNames")]
    [Arguments("Attribute")]
    public Task UnrecognizedAttributeSpellingIsSilentAsync(string attribute) =>
        VerifyReportAsync($$"""
            public sealed class {{attribute}}Attribute : System.Attribute { }
            public class Tests { [{{attribute}}] public void Ordinary() { } }
            """ + XunitStubs);

    /// <summary>Verifies BCL assertion helpers count as verification.</summary>
    /// <param name="call">The verification helper invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("System.Diagnostics.Debug.Assert(true)")]
    [Arguments("System.Diagnostics.Trace.Assert(true)")]
    [Arguments("System.Diagnostics.Contracts.Contract.Assert(true)")]
    public Task BclVerificationHelperIsSilentAsync(string call) =>
        VerifyReportAsync($$"""
            public class Tests
            {
                [Xunit.Fact]
                public void Verifies() { {{call}}; }
            }
            """ + XunitStubs);

    /// <summary>Verifies target-typed platform construction without an assertion is reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ImplicitPlatformConstructionIsReportedAsync() =>
        VerifyReportAsync("""
            public class Tests
            {
                [Xunit.Fact]
                public void {|SST2500:Builds|}() { System.Text.StringBuilder builder = new(); }
            }
            """ + XunitStubs);

    /// <summary>Verifies a throw expression in an expression-bodied test is not reported.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThrowExpressionIsSilentAsync() =>
        VerifyReportAsync("""
            public class Tests
            {
                [Xunit.Fact]
                public int Pending() => throw new System.NotImplementedException();
            }
            """ + XunitStubs);

    /// <summary>Verifies an unresolved invocation may be an assertion and must remain silent.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnresolvedInvocationIsSilentAsync() =>
        new VerifyKey.Test
        {
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.Net90,
            TestCode = $$"""class Tests { [Xunit.Fact] public void Verifies() { Missing(); } }{{XunitStubs}}""",
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies an incomplete neighbouring attribute does not hide a recognized test marker.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task IncompleteAttributeStillReportsEmptyTestAsync() =>
        new VerifyKey.Test
        {
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.Net90,
            TestCode = $$"""class Tests { [Xunit.Fact, ] public void {|SST2500:Empty|}() { } }{{XunitStubs}}""",
            CompilerDiagnostics = CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies function-pointer calls remain silent because they have no containing platform assembly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FunctionPointerInvocationIsSilentAsync()
    {
        var test = new VerifyKey.Test
        {
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.Net90,
            TestCode = $$"""class Tests { [Xunit.Fact] public unsafe void Verifies(delegate*<void> check) { check(); } }{{XunitStubs}}""",
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectCompilationOptions(
            projectId,
            ((Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions)solution.GetProject(projectId)!.CompilationOptions!).WithAllowUnsafe(true)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies platform types with assertion-like names still need the exact verification namespace.</summary>
    /// <param name="typeNamespace">The namespace that does not identify a BCL assertion helper.</param>
    /// <param name="typeName">The assertion-like type name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Other", "Debug")]
    [Arguments("Other.Diagnostics", "Trace")]
    [Arguments("Other.System.Diagnostics", "Debug")]
    [Arguments("Other", "Contract")]
    [Arguments("Other.Contracts", "Contract")]
    [Arguments("Other.Diagnostics.Contracts", "Contract")]
    [Arguments("Other.System.Diagnostics.Contracts", "Contract")]
    public async Task PlatformDecoyVerificationTypeIsReportedAsync(string typeNamespace, string typeName)
    {
        var test = new VerifyKey.Test
        {
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.Net90,
            TestCode = $$"""
                class Tests
                {
                    [Xunit.Fact]
                    public void {|SST2500:Computes|}() { {{typeNamespace}}.{{typeName}}.Check(); }
                }
                namespace {{typeNamespace}}
                {
                    public static class {{typeName}} { public static void Check() { } }
                }
                """ + XunitStubs,
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectAssemblyName(projectId, "System.VerificationProbe"));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies platform assembly identities are recognized without accepting a similar user assembly name.</summary>
    /// <param name="assemblyName">The assembly containing the invoked helper.</param>
    /// <param name="reports">Whether calls from this assembly are classified as platform operations.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("mscorlib", true)]
    [Arguments("netstandard", true)]
    [Arguments("System.Private.CoreLib", true)]
    [Arguments("System", true)]
    [Arguments("Systematic.Tests", false)]
    public async Task PlatformAssemblyIdentityControlsReportingAsync(string assemblyName, bool reports)
    {
        var name = reports ? "{|SST2500:Computes|}" : "Computes";
        var test = new VerifyKey.Test
        {
            ReferenceAssemblies = RoslynCommon.Analyzers.Tests.AnalyzerFrameworks.Net90,
            TestCode = $$"""
                public class Tests
                {
                    [Xunit.Fact]
                    public void {{name}}() { Helper.Compute(); }
                }
                public static class Helper { public static void Compute() { } }
                """ + XunitStubs,
        };
        test.SolutionTransforms.Add((solution, projectId) => solution.WithProjectAssemblyName(projectId, assemblyName));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the concrete expected-exception marker is used when its framework has no base marker.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ConcreteExpectedExceptionFallbackIsSilentAsync() =>
        VerifyReportAsync("""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public class Tests
            {
                [ExpectedException, TestMethod]
                public void ExpectsThrow() { }
            }
            namespace Microsoft.VisualStudio.TestTools.UnitTesting
            {
                public sealed class TestMethodAttribute : System.Attribute { }
                public sealed class ExpectedExceptionAttribute : System.Attribute { }
            }
            """);

    /// <summary>Verifies multiple derived expected-exception attributes preserve the exception verdict.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DerivedExpectedExceptionAttributesAreSilentAsync() =>
        VerifyReportAsync("""
            using Microsoft.VisualStudio.TestTools.UnitTesting;
            public sealed class FirstAttribute : ExpectedExceptionBaseAttribute { }
            public sealed class SecondAttribute : ExpectedExceptionBaseAttribute { }
            public class Tests
            {
                [First, Second, TestMethod]
                public void ExpectsThrow() { }
            }
            """ + MsTestStubs);

    /// <summary>Verifies an empty test marked with an attribute derived from the xUnit fact is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DerivedFactMarkerIsReportedAsync() =>
        VerifyReportAsync(
            """
            public class Tests
            {
                [Xunit.SkippableFact]
                public void {|SST2500:Case|}()
                {
                }
            }

            namespace Xunit
            {
                public class FactAttribute : System.Attribute { }

                public sealed class SkippableFactAttribute : FactAttribute { }
            }
            """);

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

        var test = new VerifyKey.Test { ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with any diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyReportAsync(string source)
    {
        var test = new VerifyKey.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };

        await test.RunAsync(CancellationToken.None);
    }
}
