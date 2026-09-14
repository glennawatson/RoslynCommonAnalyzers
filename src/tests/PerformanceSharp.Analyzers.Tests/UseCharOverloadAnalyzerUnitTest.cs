// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyCharOverload = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1201UseCharOverloadAnalyzer,
    PerformanceSharp.Analyzers.Psh1201UseCharOverloadCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1201 (use the char overload for single-character strings) and its code fix.</summary>
public class UseCharOverloadAnalyzerUnitTest
{
    /// <summary>A method name outside the supported string searches.</summary>
    private const string UnknownMethod = "Unknown";

    /// <summary>Verifies unsupported syntax and non-string bindings are left unchanged.</summary>
    /// <param name="expression">The candidate invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Contains(\"x\")")]
    [Arguments("value?.Contains(\"x\")")]
    [Arguments("value->Contains(\"x\")")]
    [Arguments("value.Contains<int>(\"x\")")]
    [Arguments("value.Equals(\"x\")")]
    [Arguments("value.Contains()")]
    [Arguments("value.Contains(\"x\", System.StringComparison.Ordinal)")]
    [Arguments("value.Contains(\"\")")]
    [Arguments("value.Contains(needle)")]
    [Arguments("value.StartsWith(\"x\", comparison)")]
    [Arguments("value.StartsWith(\"x\", C.Ordinal)")]
    [Arguments("value.StartsWith(\"x\", ComparisonValues.Ordinal)")]
    [Arguments("value.IndexOf(\"x\", Other.Ordinal)")]
    [Arguments("C.Contains(\"x\")")]
    [Arguments("other.Contains(\"x\")")]
    [Arguments("missing.Contains(\"x\")")]
    public async Task NoncandidateInvocationIsIgnoredAsync(string expression)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            class C
            {
                static System.StringComparison Ordinal => System.StringComparison.Ordinal;
                static bool Contains(string text) => false;
                object M(string value, Other other, string needle, System.StringComparison comparison) => {{expression}};
            }
            class Other
            {
                public const int Ordinal = 0;
                public bool Contains(string value) => false;
            }
            class ComparisonValues
            {
                public const System.StringComparison Ordinal = System.StringComparison.Ordinal;
            }
            """);
        var compilation = CSharpCompilation.Create("CharNearMiss", [tree], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Psh1201UseCharOverloadAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies overload availability reflects each flag independently and rejects unknown methods.</summary>
    /// <param name="method">The only available char overload.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Contains")]
    [Arguments("StartsWith")]
    [Arguments("EndsWith")]
    [Arguments("IndexOf")]
    [Arguments("LastIndexOf")]
    [Arguments(UnknownMethod)]
    public async Task CharOverloadFlagsMatchRequestedMethodAsync(string method)
    {
        var flags = new Psh1201UseCharOverloadAnalyzer.CharOverloads(
            method == "Contains",
            method == "StartsWith",
            method == "EndsWith",
            method == "IndexOf",
            method == "LastIndexOf");
        await Assert.That(flags.HasAny).IsEqualTo(method != UnknownMethod);
        await Assert.That(flags.HasOverload(method)).IsEqualTo(method != UnknownMethod);
        await Assert.That(flags.HasOverload(UnknownMethod)).IsFalse();
        await Assert.That(default(Psh1201UseCharOverloadAnalyzer.CharOverloads).HasOverload(method)).IsFalse();
    }

    /// <summary>Verifies an unresolved string special type advertises no char overloads.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MissingCoreLibraryHasNoCharOverloadsAsync()
    {
        var compilation = CSharpCompilation.Create("MissingString");
        var flags = Psh1201UseCharOverloadAnalyzer.CharOverloads.Resolve(compilation);
        await Assert.That(flags.HasAny).IsFalse();
        await Assert.That(flags).IsEqualTo(default(Psh1201UseCharOverloadAnalyzer.CharOverloads));
    }

    /// <summary>Verifies target APIs with optional parameters, non-string inputs, or another comparison enum are rejected.</summary>
    /// <param name="member">The target string API declaration.</param>
    /// <param name="expression">The bound invocation to inspect.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public bool Contains(string text, bool extra = false) => false;", "value.Contains(\"x\")")]
    [Arguments("public bool Contains(object text) => false;", "value.Contains(\"x\")")]
    [Arguments("public bool StartsWith(string text, Comparison comparison) => false;", "value.StartsWith(\"x\", System.Comparison.Ordinal)")]
    public async Task DifferentTargetSignaturesAreNotRewrittenAsync(string member, string expression)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            namespace System
            {
                public class Object {}
                public abstract class ValueType {}
                public abstract class Enum : ValueType {}
                public struct Void {}
                public struct Boolean {}
                public struct Char {}
                public struct Int32 {}
                public enum Comparison { Ordinal }
                public sealed class String
                {
                    public bool Contains(char value) => false;
                    public bool StartsWith(char value) => false;
                    {{member}}
                }
            }
            class C { bool M(string value) => {{expression}}; }
            """);
        var compilation = CSharpCompilation.Create("CharTargetShape", [tree]);
        var invocation = (await tree.GetRootAsync()).DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        await Assert.That(compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol).IsNotNull();
        var diagnostics = await compilation.WithAnalyzers([new Psh1201UseCharOverloadAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Verifies Contains with a single-character literal is reported (PSH1201) and fixed to the char overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsSingleCharacterLiteralReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains({|PSH1201:"x"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string value)
                                           => value.Contains('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies StartsWith with an explicit ordinal comparison is fixed and the comparison dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartsWithOrdinalReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string value)
                                      => value.StartsWith({|PSH1201:"x"|}, StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string value)
                                           => value.StartsWith('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies EndsWith with an explicit ordinal comparison is fixed and the comparison dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EndsWithOrdinalReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string value)
                                      => value.EndsWith({|PSH1201:"x"|}, StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public bool M(string value)
                                           => value.EndsWith('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies IndexOf with an explicit ordinal comparison is fixed and the comparison dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexOfOrdinalReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(string value)
                                      => value.IndexOf({|PSH1201:"x"|}, StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(string value)
                                           => value.IndexOf('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies LastIndexOf with an explicit ordinal comparison is fixed and the comparison dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LastIndexOfOrdinalReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public int M(string value)
                                      => value.LastIndexOf({|PSH1201:"x"|}, StringComparison.Ordinal);
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public int M(string value)
                                           => value.LastIndexOf('x');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies bare IndexOf(string) is not reported — it is culture-sensitive, the char overload is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexOfWithoutComparisonIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public int M(string value)
                                      => value.IndexOf("x");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies bare StartsWith(string) is not reported — it is culture-sensitive, the char overload is not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartsWithWithoutComparisonIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.StartsWith("x");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a non-ordinal comparison argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StartsWithOrdinalIgnoreCaseIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public bool M(string value)
                                      => value.StartsWith("x", StringComparison.OrdinalIgnoreCase);
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a multi-character literal is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsMultiCharacterLiteralIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains("xy");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies a single-quote literal is fixed with correct char escaping.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsSingleQuoteLiteralEscapedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains({|PSH1201:"'"|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string value)
                                           => value.Contains('\'');
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a verbatim literal is not reported — the rule is scoped to plain literals.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsVerbatimLiteralIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains(@"x");
                              }
                              """;
        await VerifyNet90Async(Source, Source);
    }

    /// <summary>Verifies the rule stays silent where string.Contains(char) does not exist (.NET Framework 4.7.2).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ContainsSilentWhereCharOverloadUnavailableAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string value)
                                      => value.Contains("x");
                              }
                              """;

        var test = new VerifyCharOverload.Test { ReferenceAssemblies = ReferenceAssemblies.NetFramework.Net472.Default, TestCode = Source, FixedCode = Source };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies (where the char overloads exist).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyCharOverload.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, FixedCode = fixedSource };

        await test.RunAsync(CancellationToken.None);
    }
}
