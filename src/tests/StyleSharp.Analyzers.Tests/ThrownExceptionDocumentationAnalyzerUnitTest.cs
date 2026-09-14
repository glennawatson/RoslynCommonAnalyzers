// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using RoslynCommon.Analyzers.Tests;
using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1662ThrownExceptionDocumentationAnalyzer,
    StyleSharp.Analyzers.Sst1662ThrownExceptionDocumentationCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1662 (thrown exceptions should be documented).</summary>
public class ThrownExceptionDocumentationAnalyzerUnitTest
{
    /// <summary>Checks direct throws retain source-order types and descriptions across documentation shapes.</summary>
    /// <param name="documentation">The exception documentation following the summary.</param>
    /// <param name="body">The method body, including its braces or expression arrow.</param>
    /// <param name="types">The missing type names, or null when nothing is reported.</param>
    /// <param name="descriptions">The descriptions aligned with the missing types.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", ";", null, null)]
    [Arguments("<inheritdoc/>", "{ throw new Exception(); }", null, null)]
    [Arguments("", "{ Action<int> a = x => throw new Exception(); Action b = delegate { throw new Exception(); }; void Local() { throw new Exception(); } }", null, null)]
    [Arguments("", "{ try { M(); } catch (Exception e) { if (true) throw e; throw; } }", null, null)]
    [Arguments("", "{ throw new int(); }", null, null)]
    [Arguments("", "{ throw new MissingException(); }", "MissingException", "")]
    [Arguments("", "{ throw new global::System.Exception(); }", "global::System.Exception", "")]
    [Arguments("", "{ throw new E::Exception(); }", "E::Exception", "")]
    [Arguments("", "{ throw new GenericException<int>(); }", "GenericException{int}", "")]
    [Arguments("<exception cref=\"GenericException{T}\"/>", "{ throw new GenericException<int>(); }", null, null)]
    [Arguments("<exception cref=\"Exception\"/>", "{ throw new Exception(); }", null, null)]
    [Arguments("<exception/>", "{ throw new Exception(); }", "Exception", "")]
    [Arguments("<exception>Missing reference.</exception>", "{ throw new Exception(); }", "Exception", "")]
    [Arguments("<exception name=\"other\" cref=\"Exception\"/>", "{ throw new Exception(); }", null, null)]
    [Arguments("<exception cref=\"int\"/>", "{ throw new Exception(); }", "Exception", "")]
    [Arguments("<exception cref=\"Exception[]\"/>", "{ throw new Exception(); }", null, null)]
    [Arguments("<exception cref=\"ArgumentException\"/>", "{ throw new Exception(); }", "Exception", "")]
    [Arguments("", "{ if (true) throw new Exception(); throw new Exception(); }", "Exception", "Thrown when <c>true</c>.")]
    [Arguments("", "{ if (true) throw new Exception(); throw new ArgumentException(); }", "Exception\nArgumentException", "Thrown when <c>true</c>.\n")]
    [Arguments("", "{ if (true) { } else { throw new Exception(); } }", "Exception", "")]
    [Arguments("", "=> true ? throw new Exception() : 0;", "Exception", "")]
    [Arguments("", "{ if (1 < 2 &&\r\n\t 3 > 2) throw new Exception(); }", "Exception", "Thrown when <c>1 &lt; 2 &amp;&amp; 3 &gt; 2</c>.")]
    public async Task DirectThrowPropertiesFollowWrittenSyntaxAsync(string documentation, string body, string? types, string? descriptions)
    {
        var source = $$"""
            using System;
            using E = System;
            class GenericException<T> : Exception { }
            abstract class C
            {
                /// <summary>Runs the operation.</summary>
                /// {{documentation}}
                public int M() {{body}}
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, new(documentationMode: DocumentationMode.Diagnose));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(ImmutableDictionary<string, ReportDiagnostic>.Empty.Add("SST1662", ReportDiagnostic.Warn));
        var compilation = CSharpCompilation.Create(nameof(DirectThrowPropertiesFollowWrittenSyntaxAsync), [tree], RuntimeMetadataReferences.Platform, options);
        var diagnostics = await compilation.WithAnalyzers([new Sst1662ThrownExceptionDocumentationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        if (types is null)
        {
            await Assert.That(diagnostics).IsEmpty();
            return;
        }

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("SST1662");
        await Assert.That(diagnostics[0].Properties[Sst1662ThrownExceptionDocumentationAnalyzer.ThrownTypesKey]).IsEqualTo(types);
        await Assert.That(diagnostics[0].Properties[Sst1662ThrownExceptionDocumentationAnalyzer.ThrownDescriptionsKey]).IsEqualTo(descriptions);
    }

    /// <summary>Checks constructors and both operator forms report at their member-name token.</summary>
    /// <param name="member">The documented member containing a throw.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("public {|SST1662:C|}() { throw new Exception(); }")]
    [Arguments("public static C operator {|SST1662:+|}(C left, C right) => throw new Exception();")]
    [Arguments("public static explicit {|SST1662:operator|} int(C value) => throw new Exception();")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReportsThrownTypesOnNamedMemberKindsAsync(string member) =>
        Verify.VerifyAnalyzerAsync($$"""
            using System;
            class C
            {
                /// <summary>Performs the operation.</summary>
                {{member}}
            }
            """);

    /// <summary>Verifies a documented thrown exception produces no diagnostics.</summary>
    /// <param name="cref">The written reference to the documented exception type or constructor.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("InvalidOperationException")]
    [Arguments("System.InvalidOperationException")]
    [Arguments("global::System.InvalidOperationException")]
    [Arguments("InvalidOperationException()")]
    [Arguments("System.InvalidOperationException(string)")]
    public Task DocumentedThrowIsCleanAsync(string cref) =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            using System;

            internal class C
            {
                /// <summary>Does it.</summary>
                /// <exception cref="{{cref}}">When bad.</exception>
                public void M()
                {
                    throw new InvalidOperationException();
                }
            }
            """);

    /// <summary>Verifies an undocumented member (no documentation comment) is left to the coverage rules.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UndocumentedMemberIsIgnoredAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                public void M()
                {
                    throw new InvalidOperationException();
                }
            }
            """);

    /// <summary>Verifies a throw inside a lambda is not attributed to the enclosing member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ThrowInsideLambdaIsIgnoredAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            using System;

            internal class C
            {
                /// <summary>Does it.</summary>
                public void M()
                {
                    Action a = () => throw new InvalidOperationException();
                    a();
                }
            }
            """);

    /// <summary>Verifies a guarded throw is documented with the condition that reaches it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The guard is written as code rather than turned into prose: it is what the reader has to satisfy,
    /// and restating it verbatim describes the trigger without the fix inventing a sentence.
    /// </remarks>
    [Test]
    public async Task GuardedThrowIsDocumentedWithItsConditionAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  /// <summary>Does it.</summary>
                                  /// <param name="value">The value.</param>
                                  public void {|SST1662:M|}(string value)
                                  {
                                      if (value.Length == 0)
                                      {
                                          throw new InvalidOperationException();
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       /// <summary>Does it.</summary>
                                       /// <param name="value">The value.</param>
                                       /// <exception cref="InvalidOperationException">Thrown when <c>value.Length == 0</c>.</exception>
                                       public void M(string value)
                                       {
                                           if (value.Length == 0)
                                           {
                                               throw new InvalidOperationException();
                                           }
                                       }
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null-coalescing throw is documented as the operand being null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullCoalescingThrowIsDocumentedAsANullOperandAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  /// <summary>Does it.</summary>
                                  /// <param name="value">The value.</param>
                                  /// <returns>The value.</returns>
                                  public string {|SST1662:M|}(string value) =>
                                      value ?? throw new ArgumentNullException(nameof(value));
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   internal class C
                                   {
                                       /// <summary>Does it.</summary>
                                       /// <param name="value">The value.</param>
                                       /// <returns>The value.</returns>
                                       /// <exception cref="ArgumentNullException">Thrown when <c>value</c> is <see langword="null"/>.</exception>
                                       public string M(string value) =>
                                           value ?? throw new ArgumentNullException(nameof(value));
                                   }
                                   """;

        await Verify.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an unconditional throw is reported but offers no fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// There is no condition to state, so the only element the fix could write is an empty one — which is
    /// what SST1665 reports, and it calls that worse than the missing element this rule found. The sentence
    /// is the author's to write.
    /// </remarks>
    [Test]
    public async Task UnconditionalThrowOffersNoFixAsync()
    {
        const string Source = """
                              using System;

                              internal class C
                              {
                                  /// <summary>Does it.</summary>
                                  public void {|SST1662:M|}()
                                  {
                                      throw new InvalidOperationException();
                                  }
                              }
                              """;

        await Verify.VerifyCodeFixAsync(Source, Source);
    }
}
