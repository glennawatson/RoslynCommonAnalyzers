// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNamedEnum = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2264UseNamedEnumMemberAnalyzer,
    StyleSharp.Analyzers.Sst2264UseNamedEnumMemberCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2264UseNamedEnumMemberAnalyzer"/> and its code fix (SST2264).</summary>
public class UseNamedEnumMemberAnalyzerUnitTest
{
    /// <summary>Verifies a numeric cast to a framework enum is reported and named.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FrameworkEnumIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Text.RegularExpressions;

                              internal class C
                              {
                                  public RegexOptions M() => {|SST2264:(RegexOptions)1|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Text.RegularExpressions;

                                   internal class C
                                   {
                                       public RegexOptions M() => RegexOptions.IgnoreCase;
                                   }
                                   """;
        await VerifyNamedEnum.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a zero literal cast names the zero member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ZeroLiteralNamesZeroMemberAsync()
    {
        const string Source = """
                              internal enum Color
                              {
                                  None = 0,
                                  Red = 1,
                                  Green = 2,
                                  Blue = 4,
                              }

                              internal class C
                              {
                                  public Color M() => {|SST2264:(Color)0|};
                              }
                              """;
        const string FixedSource = """
                                   internal enum Color
                                   {
                                       None = 0,
                                       Red = 1,
                                       Green = 2,
                                       Blue = 4,
                                   }

                                   internal class C
                                   {
                                       public Color M() => Color.None;
                                   }
                                   """;
        await VerifyNamedEnum.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a parenthesized numeric literal is still reported and named.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedLiteralIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal enum Color
                              {
                                  None = 0,
                                  Red = 1,
                              }

                              internal class C
                              {
                                  public Color M() => {|SST2264:(Color)(1)|};
                              }
                              """;
        const string FixedSource = """
                                   internal enum Color
                                   {
                                       None = 0,
                                       Red = 1,
                                   }

                                   internal class C
                                   {
                                       public Color M() => Color.Red;
                                   }
                                   """;
        await VerifyNamedEnum.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a value shared by two aliased members is left alone; the name is ambiguous.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AmbiguousAliasIsCleanAsync()
        => await VerifyNamedEnum.VerifyAnalyzerAsync(
            """
            internal enum Color
            {
                None = 0,
                Red = 1,
                Crimson = 1,
            }

            internal class C
            {
                public Color M() => (Color)1;
            }
            """);

    /// <summary>Verifies a value that combines members names none is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinationValueIsCleanAsync()
        => await VerifyNamedEnum.VerifyAnalyzerAsync(
            """
            internal enum Color
            {
                None = 0,
                Red = 1,
                Green = 2,
                Blue = 4,
            }

            internal class C
            {
                public Color M() => (Color)3;
            }
            """);

    /// <summary>Verifies a value that matches no member is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnmatchedValueIsCleanAsync()
        => await VerifyNamedEnum.VerifyAnalyzerAsync(
            """
            internal enum Color
            {
                None = 0,
                Red = 1,
                Green = 2,
                Blue = 4,
            }

            internal class C
            {
                public Color M() => (Color)8;
            }
            """);

    /// <summary>Verifies a cast of a non-literal expression is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonLiteralCastIsCleanAsync()
        => await VerifyNamedEnum.VerifyAnalyzerAsync(
            """
            internal enum Color
            {
                None = 0,
                Red = 1,
            }

            internal class C
            {
                public Color M(int value) => (Color)value;
            }
            """);
}
