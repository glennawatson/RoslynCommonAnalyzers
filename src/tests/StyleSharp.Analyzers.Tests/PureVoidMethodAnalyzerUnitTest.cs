// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyPure = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2452PureVoidMethodAnalyzer>;
using VerifyPureFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2452PureVoidMethodAnalyzer,
    StyleSharp.Analyzers.Sst2452PureVoidMethodCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2452 (a pure-marked method with no result a caller could observe).</summary>
public class PureVoidMethodAnalyzerUnitTest
{
    /// <summary>Verifies a pure-marked void method is reported and the attribute removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureVoidMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Diagnostics.Contracts;

                              public class C
                              {
                                  [{|SST2452:Pure|}]
                                  public void Reset()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics.Contracts;

                                   public class C
                                   {
                                       public void Reset()
                                       {
                                       }
                                   }
                                   """;
        await VerifyPureFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an expression-bodied pure-marked void method is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedPureVoidMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Diagnostics.Contracts;

                              public class C
                              {
                                  [{|SST2452:Pure|}]
                                  public void Log(string message) => System.Console.WriteLine(message);
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics.Contracts;

                                   public class C
                                   {
                                       public void Log(string message) => System.Console.WriteLine(message);
                                   }
                                   """;
        await VerifyPureFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the fully qualified attribute spelling is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedPureAttributeIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  [{|SST2452:System.Diagnostics.Contracts.Pure|}]
                                  public void Reset()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Reset()
                                       {
                                       }
                                   }
                                   """;
        await VerifyPureFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies only the pure attribute is removed when it shares a list with another.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureAmongOtherAttributesInOneListIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Diagnostics.Contracts;

                              public class C
                              {
                                  [{|SST2452:Pure|}, Obsolete("legacy")]
                                  public void Reset()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Diagnostics.Contracts;

                                   public class C
                                   {
                                       [Obsolete("legacy")]
                                       public void Reset()
                                       {
                                       }
                                   }
                                   """;
        await VerifyPureFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies removing a pure attribute in its own later list leaves the first list intact.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureInSeparateSecondListIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;
                              using System.Diagnostics.Contracts;

                              public class C
                              {
                                  [Obsolete("legacy")]
                                  [{|SST2452:Pure|}]
                                  public void Reset()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Diagnostics.Contracts;

                                   public class C
                                   {
                                       [Obsolete("legacy")]
                                       public void Reset()
                                       {
                                       }
                                   }
                                   """;
        await VerifyPureFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the method's documentation comment survives the attribute's removal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DocumentedPureVoidMethodKeepsItsDocCommentAsync()
    {
        const string Source = """
                              using System.Diagnostics.Contracts;

                              public class C
                              {
                                  /// <summary>Resets the state.</summary>
                                  [{|SST2452:Pure|}]
                                  public void Reset()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics.Contracts;

                                   public class C
                                   {
                                       /// <summary>Resets the state.</summary>
                                       public void Reset()
                                       {
                                       }
                                   }
                                   """;
        await VerifyPureFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a pure-marked method returning a bare task is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureTaskMethodIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Diagnostics.Contracts;
                              using System.Threading.Tasks;

                              public class C
                              {
                                  [{|SST2452:Pure|}]
                                  public Task RunAsync() => Task.CompletedTask;
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics.Contracts;
                                   using System.Threading.Tasks;

                                   public class C
                                   {
                                       public Task RunAsync() => Task.CompletedTask;
                                   }
                                   """;
        await VerifyPureFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a pure-marked method returning a bare value task is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureValueTaskMethodIsFlaggedAsync()
    {
        var test = new VerifyPure.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       using System.Diagnostics.Contracts;
                       using System.Threading.Tasks;

                       public class C
                       {
                           [{|SST2452:Pure|}]
                           public ValueTask RunAsync() => default;
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies fix-all removes the attribute from every reported method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRemovesEveryPureAttributeAsync()
    {
        const string Source = """
                              using System.Diagnostics.Contracts;

                              public class C
                              {
                                  [{|SST2452:Pure|}]
                                  public void First()
                                  {
                                  }

                                  [{|SST2452:Pure|}]
                                  public void Second()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics.Contracts;

                                   public class C
                                   {
                                       public void First()
                                       {
                                       }

                                       public void Second()
                                       {
                                       }
                                   }
                                   """;
        await VerifyPureFix.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a pure-marked interface method with no result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureVoidInterfaceMethodIsFlaggedAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.Contracts;

            public interface IWorker
            {
                [{|SST2452:Pure|}]
                void Run();
            }
            """);

    /// <summary>Verifies a pure-marked method that returns a value is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureValueReturningMethodIsCleanAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.Contracts;

            public class C
            {
                [Pure]
                public int Total(int left, int right) => left + right;
            }
            """);

    /// <summary>Verifies a pure-marked method returning a task with a value is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureGenericTaskMethodIsCleanAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.Contracts;
            using System.Threading.Tasks;

            public class C
            {
                [Pure]
                public Task<int> TotalAsync() => Task.FromResult(1);
            }
            """);

    /// <summary>Verifies a same-named attribute from another namespace is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedPureAttributeIsCleanAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            namespace Custom.Annotations
            {
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class PureAttribute : System.Attribute
                {
                }
            }

            public class C
            {
                [Custom.Annotations.Pure]
                public void Reset()
                {
                }
            }
            """);

    /// <summary>Verifies a void method without attributes is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VoidMethodWithoutAttributesIsCleanAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void Reset()
                {
                }
            }
            """);

    /// <summary>Verifies an unrelated attribute on a void method is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OtherAttributeOnVoidMethodIsCleanAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                [Obsolete("legacy")]
                public void Reset()
                {
                }
            }
            """);

    /// <summary>Verifies a pure void method whose result flows through an out parameter is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureVoidMethodWithOutParameterIsCleanAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.Contracts;

            public class C
            {
                [Pure]
                public void Deconstruct(out int width, out int height)
                {
                    width = 1;
                    height = 2;
                }
            }
            """);

    /// <summary>Verifies a pure void method writing through a ref parameter is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PureVoidMethodWithRefParameterIsCleanAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.Contracts;

            public class C
            {
                [Pure]
                public void Normalize(ref int value)
                {
                    value = value < 0 ? 0 : value;
                }
            }
            """);

    /// <summary>Verifies a user type that merely shares the task name is treated as a real result.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomTaskReturnTypeIsCleanAsync()
        => await VerifyPure.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.Contracts;

            public sealed class Task
            {
            }

            public class C
            {
                [Pure]
                public Task Create() => new Task();
            }
            """);
}
