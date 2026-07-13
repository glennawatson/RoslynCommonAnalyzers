// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1219UseIsNullOrWhiteSpaceAnalyzer,
    PerformanceSharp.Analyzers.Psh1219UseIsNullOrWhiteSpaceCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1219UseIsNullOrWhiteSpaceAnalyzer"/> (PSH1219 trim-and-measure blank tests).</summary>
public class UseIsNullOrWhiteSpaceAnalyzerUnitTest
{
    /// <summary>Verifies <c>Trim().Length == 0</c> is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimLengthZeroIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text)
                                      => {|PSH1219:text.Trim().Length == 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text)
                                           => string.IsNullOrWhiteSpace(text);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>Trim().Length != 0</c> asks the opposite question and so gains a negation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimLengthNotZeroIsNegatedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text)
                                      => {|PSH1219:text.Trim().Length != 0|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text)
                                           => !string.IsNullOrWhiteSpace(text);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the zero-on-the-left form is reported too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroOnLeftIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text)
                                      => {|PSH1219:0 == text.Trim().Length|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text)
                                           => string.IsNullOrWhiteSpace(text);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>Trim() == ""</c> is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimComparedToEmptyLiteralIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text)
                                      => {|PSH1219:text.Trim() == ""|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text)
                                           => string.IsNullOrWhiteSpace(text);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>Trim() != string.Empty</c> is reported and negated.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimComparedToStringEmptyIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text)
                                      => {|PSH1219:text.Trim() != string.Empty|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text)
                                           => !string.IsNullOrWhiteSpace(text);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>string.IsNullOrEmpty(text.Trim())</c> is reported and rewritten.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNullOrEmptyOfTrimIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text)
                                      => {|PSH1219:string.IsNullOrEmpty(text.Trim())|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text)
                                           => string.IsNullOrWhiteSpace(text);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a negated <c>IsNullOrEmpty</c> keeps its negation, because only the call is replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NegatedIsNullOrEmptyOfTrimKeepsItsNegationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public bool M(string text)
                                      => !{|PSH1219:string.IsNullOrEmpty(text.Trim())|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public bool M(string text)
                                           => !string.IsNullOrWhiteSpace(text);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a property receiver is carried through to the helper call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyReceiverIsCarriedThroughAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string Name { get; set; } = "x";

                                  public bool M()
                                  {
                                      if ({|PSH1219:Name.Trim().Length == 0|})
                                      {
                                          return true;
                                      }

                                      return false;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string Name { get; set; } = "x";

                                       public bool M()
                                       {
                                           if (string.IsNullOrWhiteSpace(Name))
                                           {
                                               return true;
                                           }

                                           return false;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>Trim(char)</c> is not reported: trimming a specific character is a different question.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimWithCharArgumentIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public bool M(string text) => text.Trim(',').Length == 0;
            }
            """);

    /// <summary>Verifies <c>Trim(char[])</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimWithCharArrayArgumentIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public bool M(string text, char[] trimmed) => text.Trim(trimmed) == "";
            }
            """);

    /// <summary>Verifies a trimmed value that is stored is not reported: something else may still read the copy.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StoredTrimmedValueIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string M(string text)
                {
                    var trimmed = text.Trim();
                    return trimmed.Length == 0 ? "blank" : trimmed;
                }
            }
            """);

    /// <summary>Verifies a length test that is not an emptiness test is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimLengthGreaterThanZeroIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public bool M(string text) => text.Trim().Length > 0;
            }
            """);

    /// <summary>Verifies a comparison against something other than the empty string is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimComparedToOtherValueIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public bool M(string text, string other) => text.Trim() == other;
            }
            """);

    /// <summary>Verifies a trimmed value handed to something other than the emptiness helper is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrimPassedToAnotherMethodIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public bool M(string text) => Use(text.Trim());

                private static bool Use(string value) => value.Length == 0;
            }
            """);

    /// <summary>Verifies a test inside an expression tree is not reported: the provider translates the call, it does not run it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionTreeLambdaIsCleanAsync()
        => await VerifyCleanAsync(
            """
            public class C
            {
                public string Name { get; set; } = "x";

                public System.Linq.Expressions.Expression<System.Func<C, bool>> M()
                    => c => c.Name.Trim().Length == 0;
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a no-diagnostic verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source expected to produce no diagnostics.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyAsync(source, source);
}
