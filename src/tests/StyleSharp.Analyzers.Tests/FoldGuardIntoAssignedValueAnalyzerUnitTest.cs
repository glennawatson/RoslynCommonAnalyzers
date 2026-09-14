// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using VerifyFoldGuard = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2283FoldGuardIntoAssignedValueAnalyzer,
    StyleSharp.Analyzers.Sst2283FoldGuardIntoAssignedValueCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2283 (fold a preceding null guard into the assigned value).</summary>
public class FoldGuardIntoAssignedValueAnalyzerUnitTest
{
    /// <summary>Verifies a non-argument-null guard before a field assignment folds into a throw expression.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GuardBeforeFieldAssignmentFoldsAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private readonly string _value;

                                  public C(string value)
                                  {
                                      {|SST2283:if|} (value is null)
                                      {
                                          throw new InvalidOperationException();
                                      }

                                      _value = value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       private readonly string _value;

                                       public C(string value)
                                       {
                                           _value = value ?? throw new InvalidOperationException();
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an <c>== null</c> guard before a property assignment folds, using a single-line throw.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EqualityGuardBeforePropertyAssignmentFoldsAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public object Value { get; private set; }

                                  public void Set(object value)
                                  {
                                      {|SST2283:if|} (value == null)
                                          throw new InvalidOperationException();
                                      Value = value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public sealed class C
                                   {
                                       public object Value { get; private set; }

                                       public void Set(object value)
                                       {
                                           Value = value ?? throw new InvalidOperationException();
                                       }
                                   }
                                   """;
        await RunAsync(Source, FixedSource);
    }

    /// <summary>Verifies an argument-null guard is left alone, because the runtime null-check helper owns it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ArgumentNullGuardIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private readonly string _value;

                                  public C(string value)
                                  {
                                      if (value is null)
                                          throw new ArgumentNullException(nameof(value));
                                      _value = value;
                                  }
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Verifies a guard followed by returning the guarded value is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GuardBeforeReturnIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  public string M(string value)
                                  {
                                      if (value is null)
                                          throw new InvalidOperationException();
                                      return value;
                                  }
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Verifies a nullable value type is left alone, because the coalescing throw would box it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NullableValueTypeGuardIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private int? _value;

                                  public void M(int? value)
                                  {
                                      if (value is null)
                                          throw new InvalidOperationException();
                                      _value = value;
                                  }
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Verifies a statement between the guard and the assignment prevents the fold.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StatementBetweenGuardAndAssignmentIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private string _value = "";

                                  public void M(string value)
                                  {
                                      if (value is null)
                                          throw new InvalidOperationException();
                                      Console.WriteLine("x");
                                      _value = value;
                                  }
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Verifies a guard body with more than the single throw is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MultiStatementThrowBlockIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  private string _value = "";

                                  public void M(string value)
                                  {
                                      if (value is null)
                                      {
                                          Console.WriteLine("log");
                                          throw new InvalidOperationException();
                                      }

                                      _value = value;
                                  }
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Verifies an assignment target with its own receiver is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AssignmentTargetWithReceiverIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class Box
                              {
                                  public string Field = "";
                              }

                              public sealed class C
                              {
                                  public void M(string value, Box other)
                                  {
                                      if (value is null)
                                          throw new InvalidOperationException();
                                      other.Field = value;
                                  }
                              }
                              """;
        await RunAsync(Source);
    }

    /// <summary>Verifies reversed equality and local reference values fold into safe assignments.</summary>
    /// <param name="body">The guard and assignment before the fix.</param>
    /// <param name="fixedBody">The assignment after the fix.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("{|SST2283:if|} ((null) == (value)) throw new Exception();\n        this.Value = (value);", "this.Value = value ?? throw new Exception();")]
    [Arguments(
        "var local = value;\n        {|SST2283:if|} (local is null) throw new Exception();\n        Value = local;",
        "var local = value;\n        Value = local ?? throw new Exception();")]
    public Task SafeAssignmentFormsAreFixedAsync(string body, string fixedBody) =>
        RunAsync(
            $$"""
            using System;
            public class C
            {
                public string Value;
                public void M(string value)
                {
                    {{body}}
                }
            }
            """,
            $$"""
            using System;
            public class C
            {
                public string Value;
                public void M(string value)
                {
                    {{fixedBody}}
                }
            }
            """);

    /// <summary>Verifies guards with unsafe syntax or assignment ordering are left unchanged.</summary>
    /// <param name="body">The method body containing a guard that cannot be folded.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("if (value is null) throw new Exception(); else Value = other; Value = value;")]
    [Arguments("/* keep */ if (value is null) throw new Exception(); Value = value;")]
    [Arguments("if (value is null) throw new Exception(); // keep\nValue = value;")]
    [Arguments("if (value != null) throw new Exception(); Value = value;")]
    [Arguments("if (value == other) throw new Exception(); Value = value;")]
    [Arguments("if (this.Value == null) throw new Exception(); Value = value;")]
    [Arguments("if (Get() is null) throw new Exception(); Value = value;")]
    [Arguments("if (value is null) { } Value = value;")]
    [Arguments("if (value is null) throw new Exception();")]
    [Arguments("if (other is null) if (value is null) throw new Exception(); Value = value;")]
    [Arguments("if (value is null) throw new Exception(); string local = value;")]
    [Arguments("if (value is null) throw new Exception(); Get();")]
    [Arguments("if (value is null) throw new Exception(); Value += value;")]
    [Arguments("if (value is null) throw new Exception(); Value = other;")]
    [Arguments("if (value is null) throw new Exception(); array[0] = value;")]
    [Arguments("try { Get(); } catch { if (value is null) throw; Value = value; }")]
    [Arguments("if (Value is null) throw new Exception(); value = Value;")]
    public Task UnsafeGuardShapesAreCleanAsync(string body) =>
        RunAsync(
            $$"""
            using System;
            public class C
            {
                public string Value;
                public string Get() => Value;
                public void M(string value, string other, string[] array)
                {
                    {{body}}
                }
            }
            """);

    /// <summary>Verifies user-defined equality is not replaced with the built-in null test.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverloadedEqualityIsCleanAsync() =>
        RunAsync(
            """
            using System;
            public class C
            {
                public C Value;
                public static bool operator ==(C left, C right) => true;
                public static bool operator !=(C left, C right) => false;
                public override bool Equals(object other) => false;
                public override int GetHashCode() => 0;
                public void M(C value)
                {
                    if (value == null) throw new Exception();
                    Value = value;
                }
            }
            """);

    /// <summary>Verifies a reference-type generic constraint makes the coalescing throw legal.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReferenceConstrainedValueIsReportedAsync() =>
        VerifyFoldGuard.VerifyAnalyzerAsync(
            """
            using System;
            public class C<T> where T : class
            {
                public T Value;
                public void M(T value)
                {
                    {|SST2283:if|} (value is null) throw new Exception();
                    Value = value;
                }
            }
            """);

    /// <summary>Verifies an unconstrained generic value is not assumed to be a reference type.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnconstrainedGenericValueIsCleanAsync() =>
        RunAsync(
            """
            using System;
            public class C<T>
            {
                public T Value;
                public void M(T value)
                {
                    if (value is null) throw new Exception();
                    Value = value;
                }
            }
            """);

    /// <summary>Verifies old language versions retain guards because throw expressions are unavailable.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CSharpSixGuardIsCleanAsync()
    {
        var test = new VerifyFoldGuard.Test
        {
            TestCode = "using System; class C { string field; void M(string value) { if (value == null) throw new Exception(); field = value; } }",
            ReferenceAssemblies = AnalyzerFrameworks.Net80,
        };
        test.SolutionTransforms.Add(static (solution, projectId) => solution.WithProjectParseOptions(projectId, new CSharpParseOptions(LanguageVersion.CSharp6)));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies old frameworks allow argument-null guards to fold when the helper is unavailable.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ArgumentNullGuardWithoutRuntimeHelperIsReportedAsync()
    {
        var test = new VerifyFoldGuard.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.NetStandard20,
            TestCode = "using System; class C { string field; void M(string value) { {|SST2283:if|} (value is null) throw new ArgumentNullException(nameof(value)); field = value; } }",
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies only a static method counts as the runtime argument-null helper.</summary>
    /// <param name="member">A field or instance method with the helper's name.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("public object ThrowIfNull;")]
    [Arguments("public void ThrowIfNull(object value) { }")]
    public async Task NonstaticHelperLookalikesDoNotOwnGuardAsync(string member)
    {
        var test = new VerifyFoldGuard.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net80,
            TestCode = $$"""
                using System;
                namespace System
                {
                    public class ArgumentNullException : Exception
                    {
                        public ArgumentNullException(string name) { }
                        {{member}}
                    }
                }
                class C
                {
                    string field;
                    void M(string value)
                    {
                        {|SST2283:if|} (value is null) throw new ArgumentNullException(nameof(value));
                        field = value;
                    }
                }
                """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies code-fix revalidation rejects stale guards and honors argument-null ownership.</summary>
    /// <param name="body">The candidate guard and its following statement.</param>
    /// <param name="argumentNullFolded">Whether the argument-null helper owns this guard.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("if (value is null) throw new Exception();", false)]
    [Arguments("if (value is null) throw new ArgumentNullException(nameof(value)); field = value;", true)]
    [Arguments("if (field is null) throw new Exception(); value = field;", false)]
    public async Task RevalidationRejectsInapplicableGuardsAsync(string body, bool argumentNullFolded)
    {
        var tree = CSharpSyntaxTree.ParseText($$"""
            using System;
            class C { string field; void M(string value) { {{body}} } }
            """);
        var compilation = CSharpCompilation.Create(nameof(RevalidationRejectsInapplicableGuardsAsync), [tree], RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var guard = root.DescendantNodes().OfType<IfStatementSyntax>().Single();
        var canFold = Sst2283FoldGuardIntoAssignedValueAnalyzer.TryGetFold(
            guard,
            compilation.GetSemanticModel(tree),
            argumentNullFolded,
            CancellationToken.None,
            out var value,
            out var thrown,
            out var assignment);
        await Assert.That(canFold).IsFalse();
        await Assert.That(value).IsNull();
        await Assert.That(thrown).IsNull();
        await Assert.That(assignment).IsNull();
    }

    /// <summary>Verifies argument-null ownership does not block an unrelated exception during revalidation.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RevalidationRetainsUnrelatedExceptionGuardAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("using System; class C { string field; void M(string value) { if (value is null) throw new Exception(); field = value; } }");
        var compilation = CSharpCompilation.Create(nameof(RevalidationRetainsUnrelatedExceptionGuardAsync), [tree], RuntimeMetadataReferences.Platform);
        var root = await tree.GetRootAsync();
        var guard = root.DescendantNodes().OfType<IfStatementSyntax>().Single();
        var canFold = Sst2283FoldGuardIntoAssignedValueAnalyzer.TryGetFold(
            guard,
            compilation.GetSemanticModel(tree),
            true,
            CancellationToken.None,
            out var value,
            out var thrown,
            out var assignment);
        await Assert.That(canFold).IsTrue();
        await Assert.That(value.ToString()).IsEqualTo("value");
        await Assert.That(thrown.ToString()).IsEqualTo("new Exception()");
        await Assert.That(assignment.ToString()).IsEqualTo("field = value;");
    }

    /// <summary>Verifies an unresolved guarded identifier does not produce a diagnostic.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnresolvedGuardedValueIsCleanAsync()
    {
        var test = new VerifyFoldGuard.Test
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestCode = "using System; class C { string field; void M() { if (missing is null) throw new Exception(); field = missing; } }",
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies guard matching still reports when the thrown argument-null exception type is unresolved.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingArgumentNullExceptionTypeStillReportsGuardAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { string field; void M(string value) { if (value is null) throw new System.ArgumentNullException(nameof(value)); field = value; } }");
        var compilation = CSharpCompilation.Create(nameof(MissingArgumentNullExceptionTypeStillReportsGuardAsync), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2283FoldGuardIntoAssignedValueAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.Single();
        var source = await tree.GetTextAsync();
        await Assert.That(compilation.GetTypeByMetadataName("System.ArgumentNullException")).IsNull();
        await Assert.That(diagnostic.Id).IsEqualTo("SST2283");
        await Assert.That(source.ToString(diagnostic.Location.SourceSpan)).IsEqualTo("if");
    }

    /// <summary>Runs the analyzer and, when a fix is expected, the code fix against the given sources.</summary>
    /// <param name="source">The test source with markup.</param>
    /// <param name="fixedSource">The expected fixed source, or <see langword="null"/> when no fix is expected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source, string? fixedSource = null)
    {
        var test = new VerifyFoldGuard.Test { ReferenceAssemblies = AnalyzerFrameworks.Net80, TestCode = source, FixedCode = fixedSource ?? source, };

        await test.RunAsync(CancellationToken.None);
    }
}
