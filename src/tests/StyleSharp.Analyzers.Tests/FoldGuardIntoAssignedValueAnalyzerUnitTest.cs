// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

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

    /// <summary>Runs the analyzer and, when a fix is expected, the code fix against the given sources.</summary>
    /// <param name="source">The test source with markup.</param>
    /// <param name="fixedSource">The expected fixed source, or <see langword="null"/> when no fix is expected.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source, string? fixedSource = null)
    {
        var test = new VerifyFoldGuard.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
            FixedCode = fixedSource ?? source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
