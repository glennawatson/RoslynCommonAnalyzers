// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyDefaultValue = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2460DefaultValueOnParameterAnalyzer>;
using VerifyDefaultValueFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2460DefaultValueOnParameterAnalyzer,
    StyleSharp.Analyzers.Sst2460DefaultValueOnParameterCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2460 ([DefaultValue] on a parameter, where nothing reads it).</summary>
public class DefaultValueOnParameterAnalyzerUnitTest
{
    /// <summary>Verifies the designer attribute on an ordinary parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultValueOnParameterIsReportedAsync()
        => await VerifyAnalyzerAsync(
            """
            using System.ComponentModel;

            public static class Api
            {
                public static void Connect([{|SST2460:DefaultValue(5)|}] int retries)
                {
                }
            }
            """);

    /// <summary>Verifies the near-miss pairing — [Optional] with [DefaultValue] — is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalPairingIsReportedAsync()
        => await VerifyAnalyzerAsync(
            """
            using System.ComponentModel;
            using System.Runtime.InteropServices;

            public static class Api
            {
                public static void Connect([Optional, {|SST2460:DefaultValue(30)|}] int timeout)
                {
                }
            }
            """);

    /// <summary>Verifies a namespace-qualified spelling of the attribute is still recognised.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedSpellingIsReportedAsync()
        => await VerifyAnalyzerAsync(
            """
            public static class Api
            {
                public static void Connect([{|SST2460:System.ComponentModel.DefaultValue(5)|}] int retries)
                {
                }
            }
            """);

    /// <summary>Verifies a record positional parameter without a target specifier is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordParameterWithoutTargetIsReportedAsync()
        => await VerifyAnalyzerAsync(
            """
            using System.ComponentModel;

            public sealed record Options([{|SST2460:DefaultValue(5)|}] int Retries);
            """);

    /// <summary>Verifies the attribute on a property — its real home — is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyDefaultValueIsCleanAsync()
        => await VerifyAnalyzerAsync(
            """
            using System.ComponentModel;

            public class Settings
            {
                [DefaultValue(30)]
                public int Timeout { get; set; }
            }
            """);

    /// <summary>Verifies the correct interop pairing is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CorrectDefaultParameterValueIsCleanAsync()
        => await VerifyAnalyzerAsync(
            """
            using System.Runtime.InteropServices;

            public static class Api
            {
                public static void Connect([Optional, DefaultParameterValue(30)] int timeout)
                {
                }
            }
            """);

    /// <summary>Verifies an unrelated attribute that happens to share the name is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedDefaultValueAttributeIsCleanAsync()
        => await VerifyAnalyzerAsync(
            """
            namespace Custom
            {
                [System.AttributeUsage(System.AttributeTargets.Parameter)]
                public sealed class DefaultValueAttribute : System.Attribute
                {
                    public DefaultValueAttribute(int value) => Value = value;

                    public int Value { get; }
                }

                public static class Api
                {
                    public static void Connect([DefaultValue(5)] int retries)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a record parameter that retargets the attribute to its property is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyTargetedRecordParameterIsCleanAsync()
        => await VerifyAnalyzerAsync(
            """
            using System.ComponentModel;

            public sealed record Options([property: DefaultValue(5)] int Retries);
            """);

    /// <summary>Verifies the fix rewrites the pairing using the namespace the file already imports.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalPairingFixKeepsExistingImportAsync()
    {
        const string Source = """
                              using System.ComponentModel;
                              using System.Runtime.InteropServices;

                              public static class Api
                              {
                                  public static void Connect([Optional, {|SST2460:DefaultValue(30)|}] int timeout)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.ComponentModel;
                                   using System.Runtime.InteropServices;

                                   public static class Api
                                   {
                                       public static void Connect([Optional, DefaultParameterValue(30)] int timeout)
                                       {
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix falls back to the fully qualified name when the namespace is not imported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixQualifiesWhenNamespaceNotImportedAsync()
    {
        const string Source = """
                              using System.ComponentModel;

                              public static class Api
                              {
                                  public static void Connect([{|SST2460:DefaultValue(5)|}] int retries)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.ComponentModel;

                                   public static class Api
                                   {
                                       public static void Connect([global::System.Runtime.InteropServices.DefaultParameterValue(5)] int retries)
                                       {
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fix keeps the explicit Attribute suffix when the source spelled it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SuffixedSpellingFixKeepsSuffixAsync()
    {
        const string Source = """
                              using System.ComponentModel;
                              using System.Runtime.InteropServices;

                              public static class Api
                              {
                                  public static void Connect([{|SST2460:DefaultValueAttribute(30)|}] int timeout)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.ComponentModel;
                                   using System.Runtime.InteropServices;

                                   public static class Api
                                   {
                                       public static void Connect([DefaultParameterValueAttribute(30)] int timeout)
                                       {
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null default for a reference parameter is rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullLiteralFixOnReferenceParameterAsync()
    {
        const string Source = """
                              using System.ComponentModel;
                              using System.Runtime.InteropServices;

                              public static class Api
                              {
                                  public static void Connect([Optional, {|SST2460:DefaultValue(null)|}] string name)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.ComponentModel;
                                   using System.Runtime.InteropServices;

                                   public static class Api
                                   {
                                       public static void Connect([Optional, DefaultParameterValue(null)] string name)
                                       {
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies every occurrence in a document is rewritten together.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllReplacesEveryAttributeAsync()
    {
        const string Source = """
                              using System.ComponentModel;
                              using System.Runtime.InteropServices;

                              public static class Api
                              {
                                  public static void Connect([Optional, {|SST2460:DefaultValue(30)|}] int timeout)
                                  {
                                  }

                                  public static void Retry([{|SST2460:DefaultValue(3)|}] int attempts)
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.ComponentModel;
                                   using System.Runtime.InteropServices;

                                   public static class Api
                                   {
                                       public static void Connect([Optional, DefaultParameterValue(30)] int timeout)
                                       {
                                       }

                                       public static void Retry([DefaultParameterValue(3)] int attempts)
                                       {
                                       }
                                   }
                                   """;
        await VerifyFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies no fix is offered when the stored value cannot be the parameter's default.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchedArgumentTypeHasNoFixAsync()
    {
        const string Source = """
                              using System.ComponentModel;

                              public static class Api
                              {
                                  public static void Connect([{|SST2460:DefaultValue("slow")|}] int mode)
                                  {
                                  }
                              }
                              """;
        await VerifyFixAsync(Source, Source);
    }

    /// <summary>Verifies no fix is offered for the converter form, which has no interop counterpart.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConverterFormIsReportedWithoutFixAsync()
    {
        const string Source = """
                              using System.ComponentModel;

                              public static class Api
                              {
                                  public static void Connect([{|SST2460:DefaultValue(typeof(int), "5")|}] int retries)
                                  {
                                  }
                              }
                              """;
        await VerifyFixAsync(Source, Source);
    }

    /// <summary>Runs a diagnostic-markup verification against the .NET 8 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAnalyzerAsync(string source)
    {
        var test = new VerifyDefaultValue.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the code fix against the .NET 8 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyFixAsync(string source, string fixedSource)
    {
        var test = new VerifyDefaultValueFix.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
