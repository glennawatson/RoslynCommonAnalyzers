// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

using VerifyChainedConditionalToSwitch = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2246ChainedConditionalToSwitchAnalyzer,
    StyleSharp.Analyzers.Sst2246ChainedConditionalToSwitchCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2246 (express a same-value conditional chain as a switch).</summary>
public class ChainedConditionalToSwitchAnalyzerUnitTest
{
    /// <summary>Verifies a same-value constant chain is reported and rewritten as a switch expression.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameValueChainIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int value)
                                  {
                                      return {|SST2246:value == 1 ? "one" : value == 2 ? "two" : "other"|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(int value)
                                       {
                                           return value switch
                                           {
                                               1 => "one",
                                               2 => "two",
                                               _ => "other"
                                           };
                                       }
                                   }
                                   """;
        await VerifyChainedConditionalToSwitch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an enum chain that tests named enum values is rewritten with constant patterns.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumChainIsFixedAsync()
    {
        const string Source = """
                              public enum Color
                              {
                                  Red,
                                  Green,
                                  Blue
                              }

                              public sealed class C
                              {
                                  public string M(Color value)
                                  {
                                      return {|SST2246:value == Color.Red ? "r" : value == Color.Green ? "g" : "other"|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public enum Color
                                   {
                                       Red,
                                       Green,
                                       Blue
                                   }

                                   public sealed class C
                                   {
                                       public string M(Color value)
                                       {
                                           return value switch
                                           {
                                               Color.Red => "r",
                                               Color.Green => "g",
                                               _ => "other"
                                           };
                                       }
                                   }
                                   """;
        await VerifyChainedConditionalToSwitch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a string chain is rewritten with string constant patterns.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringSubjectChainIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(string value)
                                  {
                                      return {|SST2246:value == "a" ? 1 : value == "b" ? 2 : 3|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(string value)
                                       {
                                           return value switch
                                           {
                                               "a" => 1,
                                               "b" => 2,
                                               _ => 3
                                           };
                                       }
                                   }
                                   """;
        await VerifyChainedConditionalToSwitch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a chain over a local variable is rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalSubjectChainIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int seed)
                                  {
                                      var value = seed + 1;
                                      return {|SST2246:value == 1 ? "one" : value == 2 ? "two" : "other"|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(int seed)
                                       {
                                           var value = seed + 1;
                                           return value switch
                                           {
                                               1 => "one",
                                               2 => "two",
                                               _ => "other"
                                           };
                                       }
                                   }
                                   """;
        await VerifyChainedConditionalToSwitch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a chain over an instance field is rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldSubjectChainIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  private readonly int _value;

                                  public string M()
                                  {
                                      return {|SST2246:_value == 1 ? "one" : _value == 2 ? "two" : "other"|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       private readonly int _value;

                                       public string M()
                                       {
                                           return _value switch
                                           {
                                               1 => "one",
                                               2 => "two",
                                               _ => "other"
                                           };
                                       }
                                   }
                                   """;
        await VerifyChainedConditionalToSwitch.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies chains that mix subjects or are not constant chains are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantSameValueChainsAreCleanAsync()
        => await VerifyChainedConditionalToSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string DifferentSubjects(int value, int other)
                    => value == 1 ? "a" : other == 2 ? "b" : "c";

                public string NonConstantTests(int value, int first, int second)
                    => value == first ? "a" : value == second ? "b" : "c";

                public string SingleTest(int value)
                    => value == 1 ? "a" : "b";

                public string DuplicateConstant(int value)
                    => value == 1 ? "a" : value == 1 ? "b" : "c";
            }
            """);

    /// <summary>Verifies a chain over a property is not reported because reading it may have a side effect.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertySubjectChainIsCleanAsync()
        => await VerifyChainedConditionalToSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Value { get; set; }

                public string M()
                    => Value == 1 ? "a" : Value == 2 ? "b" : "c";
            }
            """);

    /// <summary>Verifies a floating-point chain is not reported because its equality can differ from a pattern.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FloatingPointChainIsCleanAsync()
        => await VerifyChainedConditionalToSwitch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M(double value)
                    => value == 1.0 ? "a" : value == 2.0 ? "b" : "c";
            }
            """);

    /// <summary>Verifies the chain is left alone before C# 8, where switch expressions do not exist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentBelowCSharp8Async()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(int value)
                                  {
                                      return value == 1 ? "one" : value == 2 ? "two" : "other";
                                  }
                              }
                              """;
        var test = new VerifyChainedConditionalToSwitch.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source
        };
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.CSharp7));
        });
        await test.RunAsync(CancellationToken.None);
    }
}
