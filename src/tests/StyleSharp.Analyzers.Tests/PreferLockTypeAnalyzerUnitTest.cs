// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;

using TUnit.Assertions;

using VerifyLock = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1900PreferLockTypeAnalyzer,
    StyleSharp.Analyzers.Sst1900PreferLockTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1900 (use System.Threading.Lock for a dedicated lock object) and its code fix.</summary>
public class PreferLockTypeAnalyzerUnitTest
{
    /// <summary>Verifies a lock-only object field is reported (SST1900) and its type changed to Lock.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LockOnlyObjectFieldReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly object {|SST1900:_gate|} = new();

                                  public void M()
                                  {
                                      lock (_gate)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly System.Threading.Lock _gate = new();

                                       public void M()
                                       {
                                           lock (_gate)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies a <c>new object()</c> initializer is normalised to <c>new()</c> by the fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitObjectInitializerNormalisedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly object {|SST1900:_sync|} = new object();

                                  public void M()
                                  {
                                      lock (_sync)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly System.Threading.Lock _sync = new();

                                       public void M()
                                       {
                                           lock (_sync)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an explicitly qualified System.Object field is also reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedObjectTypeReplacedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly System.Object {|SST1900:_gate|} = new();

                                  public void M()
                                  {
                                      lock (_gate)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly System.Threading.Lock _gate = new();

                                       public void M()
                                       {
                                           lock (_gate)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every lock-only object field in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class A
                              {
                                  private readonly object {|SST1900:_gate|} = new();

                                  public void M()
                                  {
                                      lock (_gate)
                                      {
                                      }
                                  }
                              }

                              public class B
                              {
                                  private readonly object {|SST1900:_sync|} = new();

                                  public void M()
                                  {
                                      lock (_sync)
                                      {
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class A
                                   {
                                       private readonly System.Threading.Lock _gate = new();

                                       public void M()
                                       {
                                           lock (_gate)
                                           {
                                           }
                                       }
                                   }

                                   public class B
                                   {
                                       private readonly System.Threading.Lock _sync = new();

                                       public void M()
                                       {
                                           lock (_sync)
                                           {
                                           }
                                       }
                                   }
                                   """;
        await VerifyNet90Async(Source, FixedSource);
    }

    /// <summary>Verifies an object field used for anything other than locking is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectUsedBeyondLockingIsCleanAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private readonly object _gate = new();

                                  public void M()
                                  {
                                      lock (_gate)
                                      {
                                      }

                                      System.Console.WriteLine(_gate);
                                  }
                              }
                              """;

        var test = new VerifyLock.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = Source, FixedCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent where System.Threading.Lock does not exist (pre-.NET 9).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenLockTypeUnavailableAsync()
        => await VerifyLock.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly object _gate = new();

                public void M()
                {
                    lock (_gate)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a System.Threading.Lock field is rejected by the syntax-only candidate fast path.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxOnlyCandidateCheckRejectsLockTypeField()
    {
        var field = ParseField("private readonly System.Threading.Lock _gate = new();");

        await Assert.That(Sst1900PreferLockTypeAnalyzer.CouldBeCandidateLockField(field)).IsFalse();
    }

    /// <summary>Verifies a partial containing type is rejected by the syntax-only candidate fast path.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxOnlyCandidateCheckRejectsFieldInPartialType()
    {
        var field = ParseFieldFromType(
            "public partial class C { private readonly object _gate = new(); }");

        await Assert.That(Sst1900PreferLockTypeAnalyzer.CouldBeCandidateLockField(field)).IsFalse();
    }

    /// <summary>Verifies object spellings still flow through the syntax-only candidate fast path.</summary>
    /// <param name="source">The field declaration source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    [Arguments("private readonly object _gate = new();")]
    [Arguments("private readonly System.Object _gate = new();")]
    public async Task SyntaxOnlyCandidateCheckKeepsObjectSpellings(string source)
    {
        var field = ParseField(source);

        await Assert.That(Sst1900PreferLockTypeAnalyzer.CouldBeCandidateLockField(field)).IsTrue();
    }

    /// <summary>Verifies the syntax-only usage fast path accepts a simple lock-only field use.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxOnlyUsageCheckAcceptsSimpleLockOnlyField()
    {
        var type = ParseType(
            "public class C { private readonly object _gate = new(); void M() { lock (_gate) { } } }");

        await Assert.That(Sst1900PreferLockTypeAnalyzer.HasOnlyUnshadowedLockUses(type, "_gate")).IsTrue();
    }

    /// <summary>Verifies the syntax-only usage fast path rejects non-lock field uses.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxOnlyUsageCheckRejectsNonLockUse()
    {
        var type = ParseType(
            "public class C { private readonly object _gate = new(); void M() { lock (_gate) { } System.Console.WriteLine(_gate); } }");

        await Assert.That(Sst1900PreferLockTypeAnalyzer.HasOnlyUnshadowedLockUses(type, "_gate")).IsFalse();
    }

    /// <summary>Verifies the syntax-only usage fast path rejects shadowed identifiers.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxOnlyUsageCheckRejectsShadowedIdentifier()
    {
        var type = ParseType(
            "public class C { private readonly object _gate = new(); void M(object _gate) { lock (_gate) { } } }");

        await Assert.That(Sst1900PreferLockTypeAnalyzer.HasOnlyUnshadowedLockUses(type, "_gate")).IsFalse();
    }

    /// <summary>Verifies the single-candidate syntax prepass accepts an unambiguous object field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxOnlySingleCandidateCheckAcceptsUnambiguousObjectField()
    {
        var type = ParseType(
            "public class C { private readonly object _gate = new(); void M() { lock (_gate) { } } }");

        await Assert.That(Sst1900PreferLockTypeAnalyzer.TryGetSingleSyntaxOnlyCandidate(type, out var variable)).IsTrue();
        await Assert.That(variable!.Identifier.ValueText).IsEqualTo("_gate");
    }

    /// <summary>Verifies the single-candidate syntax prepass rejects ambiguous Object identifier spellings.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxOnlySingleCandidateCheckRejectsAmbiguousObjectIdentifier()
    {
        var type = ParseType(
            "public class Object { } public class C { private readonly Object _gate = new(); void M() { lock (_gate) { } } }");

        await Assert.That(Sst1900PreferLockTypeAnalyzer.TryGetSingleSyntaxOnlyCandidate(type, out _)).IsFalse();
    }

    /// <summary>Verifies field-name token classification recognizes a direct lock use.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldNameTokenClassificationRecognizesLockUse()
    {
        var type = ParseType(
            "public class C { private readonly object _gate = new(); void M() { lock (_gate) { } } }");
        var token = type.DescendantTokens().Single(static t => t.ValueText == "_gate" && t.Parent is IdentifierNameSyntax);

        await Assert.That(Sst1900PreferLockTypeAnalyzer.ClassifyFieldNameToken(type, token, "_gate")).IsEqualTo(
            Sst1900PreferLockTypeAnalyzer.FieldNameTokenKind.LockUse);
    }

    /// <summary>Verifies field-name token classification ignores the field declaration token.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldNameTokenClassificationIgnoresFieldDeclaration()
    {
        var type = ParseType(
            "public class C { private readonly object _gate = new(); void M() { lock (_gate) { } } }");
        var token = type.DescendantTokens().Single(static t => t.ValueText == "_gate" && t.Parent is VariableDeclaratorSyntax);

        await Assert.That(Sst1900PreferLockTypeAnalyzer.ClassifyFieldNameToken(type, token, "_gate")).IsEqualTo(
            Sst1900PreferLockTypeAnalyzer.FieldNameTokenKind.Ignore);
    }

    /// <summary>Verifies field-name token classification flags a shadowing parameter declaration as a conflict.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldNameTokenClassificationFlagsParameterDeclaration()
    {
        var type = ParseType(
            "public class C { private readonly object _gate = new(); void M(object _gate) { lock (_gate) { } } }");
        var token = type.DescendantTokens().Single(static t => t.ValueText == "_gate" && t.Parent is ParameterSyntax);

        await Assert.That(Sst1900PreferLockTypeAnalyzer.ClassifyFieldNameToken(type, token, "_gate")).IsEqualTo(
            Sst1900PreferLockTypeAnalyzer.FieldNameTokenKind.Conflict);
    }

    /// <summary>Runs a code-fix verification against the .NET 9 reference assemblies (where the Lock type exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source, string fixedSource)
    {
        var test = new VerifyLock.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Parses a single field declaration for helper-level fast-path tests.</summary>
    /// <param name="source">The field declaration source.</param>
    /// <returns>The parsed field declaration.</returns>
    private static FieldDeclarationSyntax ParseField(string source)
        => (FieldDeclarationSyntax)SyntaxFactory.ParseCompilationUnit($$"""public class C { {{source}} }""")
            .Members[0]
            .ChildNodes()
            .Single();

    /// <summary>Parses a single field declaration from a full type declaration for helper-level fast-path tests.</summary>
    /// <param name="source">The full type declaration source.</param>
    /// <returns>The parsed field declaration.</returns>
    private static FieldDeclarationSyntax ParseFieldFromType(string source)
        => (FieldDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source)
            .Members[0]
            .ChildNodes()
            .Single();

    /// <summary>Parses a single type declaration for helper-level fast-path tests.</summary>
    /// <param name="source">The type declaration source.</param>
    /// <returns>The parsed type declaration.</returns>
    private static TypeDeclarationSyntax ParseType(string source)
        => (TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source)
            .Members[0];
}
