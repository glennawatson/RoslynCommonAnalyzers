// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySealAttribute = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1401SealAttributeTypesAnalyzer,
    PerformanceSharp.Analyzers.Psh1401SealAttributeTypesCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1401 (seal attribute types) and its fix.</summary>
public class SealAttributeTypesAnalyzerUnitTest
{
    /// <summary>Verifies an unsealed attribute subclass is reported and sealed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsealedAttributeIsSealedAsync()
    {
        const string Source = """
                              public class {|PSH1401:MyAttribute|} : System.Attribute
                              {
                              }
                              """;
        const string FixedSource = """
                                   public sealed class MyAttribute : System.Attribute
                                   {
                                   }
                                   """;
        await VerifySealAttribute.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an attribute class without any modifiers gains sealed as its first modifier.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ModifierlessAttributeIsSealedAsync()
    {
        const string Source = """
                              class {|PSH1401:MyAttribute|} : System.Attribute
                              {
                              }
                              """;
        const string FixedSource = """
                                   sealed class MyAttribute : System.Attribute
                                   {
                                   }
                                   """;
        await VerifySealAttribute.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All seals every reported attribute type in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class {|PSH1401:FirstAttribute|} : System.Attribute
                              {
                              }

                              public class {|PSH1401:SecondAttribute|} : System.Attribute
                              {
                              }
                              """;
        const string FixedSource = """
                                   public sealed class FirstAttribute : System.Attribute
                                   {
                                   }

                                   public sealed class SecondAttribute : System.Attribute
                                   {
                                   }
                                   """;
        await VerifySealAttribute.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a class deriving from an intermediate attribute base is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndirectAttributeInheritanceIsReportedAsync()
    {
        const string Source = """
                              public abstract class MyBaseAttribute : System.Attribute
                              {
                              }

                              public class {|PSH1401:CustomAttribute|} : MyBaseAttribute
                              {
                              }
                              """;
        const string FixedSource = """
                                   public abstract class MyBaseAttribute : System.Attribute
                                   {
                                   }

                                   public sealed class CustomAttribute : MyBaseAttribute
                                   {
                                   }
                                   """;
        await VerifySealAttribute.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an already sealed attribute type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SealedAttributeIsCleanAsync()
        => await VerifySealAttribute.VerifyAnalyzerAsync(
            """
            public sealed class MyAttribute : System.Attribute
            {
            }
            """);

    /// <summary>Verifies an abstract attribute base type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractAttributeBaseIsCleanAsync()
        => await VerifySealAttribute.VerifyAnalyzerAsync(
            """
            public abstract class ValidationAttribute : System.Attribute
            {
            }
            """);

    /// <summary>Verifies an unsealed class that is not an attribute is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnsealedNonAttributeClassIsCleanAsync()
        => await VerifySealAttribute.VerifyAnalyzerAsync(
            """
            public class Widget
            {
            }
            """);
}
