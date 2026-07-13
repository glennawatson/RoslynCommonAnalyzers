// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyGuard = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1492SelfAssignmentGuardAnalyzer,
    StyleSharp.Analyzers.Sst1492SelfAssignmentGuardCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1492 (do not test a value against what it is about to be assigned) and its fix.</summary>
public class Sst1492SelfAssignmentGuardAnalyzerUnitTest
{
    /// <summary>Verifies the guarded assignment is reported and the guard removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedAssignmentIsReplacedByTheAssignmentAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public void SetValue(int value)
                                  {
                                      if ({|SST1492:_value != value|})
                                      {
                                          _value = value;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private int _value;

                                       public void SetValue(int value)
                                       {
                                           _value = value;
                                       }
                                   }
                                   """;
        await VerifyGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the guard is reported when the condition names the two sides the other way round.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReversedConditionIsTheSameMistakeAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public void SetValue(int value)
                                  {
                                      if ({|SST1492:value != _value|})
                                      {
                                          _value = value;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private int _value;

                                       public void SetValue(int value)
                                       {
                                           _value = value;
                                       }
                                   }
                                   """;
        await VerifyGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a guard with no braces around its one statement is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BracelessGuardIsReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public void SetValue(int value)
                                  {
                                      if ({|SST1492:_value != value|})
                                          _value = value;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private int _value;

                                       public void SetValue(int value)
                                       {
                                           _value = value;
                                       }
                                   }
                                   """;
        await VerifyGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the inverted shape — an empty branch with the assignment in the else — is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvertedGuardIsReplacedByTheAssignmentAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public void SetValue(int value)
                                  {
                                      if ({|SST1492:_value == value|})
                                      {
                                      }
                                      else
                                      {
                                          _value = value;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private int _value;

                                       public void SetValue(int value)
                                       {
                                           _value = value;
                                       }
                                   }
                                   """;
        await VerifyGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member-access target is matched like any other name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedTargetIsReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private int _value;

                                  public void SetValue(int value)
                                  {
                                      if ({|SST1492:this._value != value|})
                                      {
                                          this._value = value;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private int _value;

                                       public void SetValue(int value)
                                       {
                                           this._value = value;
                                       }
                                   }
                                   """;
        await VerifyGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an auto-property target is reported; its setter only stores.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AutoPropertyTargetIsReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int Value { get; set; }

                                  public void SetValue(int value)
                                  {
                                      if ({|SST1492:Value != value|})
                                      {
                                          Value = value;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int Value { get; set; }

                                       public void SetValue(int value)
                                       {
                                           Value = value;
                                       }
                                   }
                                   """;
        await VerifyGuard.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a property with a hand-written setter is left alone; the guard may be load-bearing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyWithHandWrittenSetterIsCleanAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _value;

                public int Value
                {
                    get => _value;
                    set
                    {
                        _value = value;
                        Raise();
                    }
                }

                public void SetValue(int value)
                {
                    if (Value != value)
                    {
                        Value = value;
                    }
                }

                private void Raise()
                {
                }
            }
            """);

    /// <summary>Verifies the change-notification shape — a guard around more than the assignment — is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardAroundMoreThanTheAssignmentIsCleanAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _value;

                public void SetValue(int value)
                {
                    if (_value != value)
                    {
                        _value = value;
                        Raise();
                    }
                }

                private void Raise()
                {
                }
            }
            """);

    /// <summary>Verifies a guard whose assignment stores something else is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardAroundADifferentValueIsCleanAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _value;

                public void SetValue(int value, int fallback)
                {
                    if (_value != value)
                    {
                        _value = fallback;
                    }
                }
            }
            """);

    /// <summary>Verifies a compound assignment is never the operation the condition tested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompoundAssignmentIsCleanAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _value;

                public void Add(int value)
                {
                    if (_value != value)
                    {
                        _value += value;
                    }
                }
            }
            """);

    /// <summary>Verifies a guard over a call is clean; skipping the assignment also skips the call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardOverASideEffectingValueIsCleanAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _value;

                public void Refresh()
                {
                    if (_value != Next())
                    {
                        _value = Next();
                    }
                }

                private int Next() => 1;
            }
            """);

    /// <summary>Verifies an element access is not treated as a plain read; an indexer can do anything.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElementAccessTargetIsCleanAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly int[] _values = new int[1];

                public void SetValue(int value)
                {
                    if (_values[0] != value)
                    {
                        _values[0] = value;
                    }
                }
            }
            """);

    /// <summary>Verifies an if with an else branch that does something is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardWithARealElseBranchIsCleanAsync()
        => await VerifyGuard.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _value;

                public void SetValue(int value)
                {
                    if (_value != value)
                    {
                        _value = value;
                    }
                    else
                    {
                        _value = 0;
                    }
                }
            }
            """);
}
