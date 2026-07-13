// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1018RedundantParamsArrayAnalyzer,
    PerformanceSharp.Analyzers.Psh1018RedundantParamsArrayCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1018RedundantParamsArrayAnalyzer"/> (PSH1018 hand-written params arrays).</summary>
public class RedundantParamsArrayAnalyzerUnitTest
{
    /// <summary>Verifies an explicitly typed array at a params site is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitArrayIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void Log(params object[] args)
                                  {
                                  }

                                  public void M(object a, object b) => Log({|PSH1018:new object[] { a, b }|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Log(params object[] args)
                                       {
                                       }

                                       public void M(object a, object b) => Log(a, b);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an implicitly typed array at a params site is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplicitArrayIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void Log(params object[] args)
                                  {
                                  }

                                  public void M(object a, object b) => Log({|PSH1018:new[] { a, b }|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Log(params object[] args)
                                       {
                                       }

                                       public void M(object a, object b) => Log(a, b);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a zero-length array is flagged and the call left with no arguments at all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroLengthArrayIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void Log(params object[] args)
                                  {
                                  }

                                  public void M() => Log({|PSH1018:new object[0]|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Log(params object[] args)
                                       {
                                       }

                                       public void M() => Log();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty initializer is flagged and the call left with no arguments at all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyInitializerIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void Log(params object[] args)
                                  {
                                  }

                                  public void M() => Log({|PSH1018:new object[] { }|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Log(params object[] args)
                                       {
                                       }

                                       public void M() => Log();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the shared empty array is flagged; once the argument is gone the compiler reuses it anyway.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedEmptyArrayIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void Log(params object[] args)
                                  {
                                  }

                                  public void M() => Log({|PSH1018:Array.Empty<object>()|});
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void Log(params object[] args)
                                       {
                                       }

                                       public void M() => Log();
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies arguments before the params array are left exactly where they were.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LeadingArgumentsArePreservedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public void Write(string format, params object[] args)
                                  {
                                  }

                                  public void M(object a, object b) => Write("x", {|PSH1018:new object[] { a, b }|});
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public void Write(string format, params object[] args)
                                       {
                                       }

                                       public void M(object a, object b) => Write("x", a, b);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies an overload that would capture the unwrapped call keeps the array; the rewrite would change the callee.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CapturingOverloadIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void Log(params object[] args)
                {
                }

                public void Log(object a, object b)
                {
                }

                public void M(object a, object b) => Log(new object[] { a, b });
            }
            """);

    /// <summary>Verifies a single element that is itself the params array type is left alone; unwrapping would pass it as the whole array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleArrayElementIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void Log(params object[] args)
                {
                }

                public void M(object[] values) => Log(new object[] { values });
            }
            """);

    /// <summary>Verifies a lone null element is left alone; unwrapping would pass a null array instead of an array holding null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleNullElementIsCleanAsync()
        => await VerifyAsync(
            """
            #nullable enable

            public class C
            {
                public void Log(params object?[] args)
                {
                }

                public void M() => Log(new object?[] { null });
            }
            """);

    /// <summary>Verifies a covariant array is left alone; the compiler passes it straight through, so unwrapping changes the runtime array type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CovariantArrayIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void Log(params object[] args)
                {
                }

                public void M() => Log(new string[] { "a", "b" });
            }
            """);

    /// <summary>Verifies an array handed to a plain array parameter is left alone; there is no params expansion to remove.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonParamsParameterIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void Take(object[] values)
                {
                }

                public void M(object a) => Take(new object[] { a });
            }
            """);

    /// <summary>Verifies an array held in a variable is left alone; the rule only unwraps arrays written at the call site.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArrayVariableIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void Log(params object[] args)
                {
                }

                public void M(object a)
                {
                    var values = new object[] { a };
                    Log(values);
                }
            }
            """);

    /// <summary>Verifies a sized array without an initializer is left alone; its elements were never written at the call site.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SizedArrayIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void Log(params object[] args)
                {
                }

                public void M() => Log(new object[3]);
            }
            """);

    /// <summary>Verifies a named argument is left alone; the array is not sitting in a positional params slot.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void Log(params object[] args)
                {
                }

                public void M(object a, object b) => Log(args: new object[] { a, b });
            }
            """);

    /// <summary>Verifies a params collection that is not an array is left alone; the array binds through a conversion, not an expansion.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParamsSpanIsCleanAsync()
        => await VerifyAsync(
            """
            using System;

            public class C
            {
                public void Log(params ReadOnlySpan<object> args)
                {
                }

                public void M(object a, object b) => Log(new object[] { a, b });
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies and C# 13.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp13));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
