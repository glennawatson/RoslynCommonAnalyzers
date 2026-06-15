// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDefaultCtor = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1129DefaultValueTypeConstructorAnalyzer,
    StyleSharp.Analyzers.Sst1129DefaultValueTypeConstructorCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the default-value-type-constructor rule (SST1129).</summary>
public class DefaultValueTypeConstructorAnalyzerUnitTest
{
    /// <summary>Verifies a parameterless value-type construction is reported (SST1129) and replaced with default(T).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessStructConstructionReplacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static System.DateTime M() => {|SST1129:new System.DateTime()|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static System.DateTime M() => default(System.DateTime);
                                   }
                                   """;
        await VerifyDefaultCtor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All replaces every parameterless value-type construction in a document in a single pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private static System.DateTime First() => {|SST1129:new System.DateTime()|};

                                  private static System.TimeSpan Second() => {|SST1129:new System.TimeSpan()|};

                                  private static System.Guid Third() => {|SST1129:new System.Guid()|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private static System.DateTime First() => default(System.DateTime);

                                       private static System.TimeSpan Second() => default(System.TimeSpan);

                                       private static System.Guid Third() => default(System.Guid);
                                   }
                                   """;
        await VerifyDefaultCtor.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a reference-type construction and a constructor with arguments are not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceTypeAndArgumentsAreCleanAsync()
        => await VerifyDefaultCtor.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static object M() => new object();

                private static System.DateTime N() => new System.DateTime(2026, 1, 1);
            }
            """);
}
