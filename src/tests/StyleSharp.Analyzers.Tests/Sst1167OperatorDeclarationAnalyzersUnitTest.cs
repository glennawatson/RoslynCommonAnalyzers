// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifysst0018 = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1167OperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    StyleSharp.Analyzers.Sst1167OperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1167 analyzer that requires operator declaration parameters to be on unique lines.</summary>
public class Sst1167OperatorDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies an operator declaration with all parameters on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string Test = """
            public class Foo
            {
                public static Foo operator +(Foo a, Foo b) => a;
            }
            """;

        await Verifysst0018.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies an operator declaration with parameters split unevenly across lines reports the expected diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvalidAsync()
    {
        const string Test = """
            public class Foo
            {
                {|SST1167:public static Foo operator +(
                    Foo a, Foo b) => a;|}
            }
            """;

        await Verifysst0018.VerifyAnalyzerAsync(Test);
    }

    /// <summary>Verifies the code fix rewrites the operator declaration so each parameter is on its own line.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixAsync()
    {
        const string Test = """
            public class Foo
            {
                {|SST1167:public static Foo operator +(
                    Foo a, Foo b) => a;|}
            }
            """;

        const string FixedSource = """
            public class Foo
            {
                public static Foo operator +(
                    Foo a,
                    Foo b) => a;
            }
            """;

        await Verifysst0018.VerifyCodeFixAsync(Test, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every operator declaration in a single document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Test = """
            public class Foo
            {
                {|SST1167:public static Foo operator +(
                    Foo a, Foo b) => a;|}

                {|SST1167:public static Foo operator -(
                    Foo a, Foo b) => a;|}

                {|SST1167:public static Foo operator *(
                    Foo a, Foo b) => a;|}
            }
            """;

        const string FixedSource = """
            public class Foo
            {
                public static Foo operator +(
                    Foo a,
                    Foo b) => a;

                public static Foo operator -(
                    Foo a,
                    Foo b) => a;

                public static Foo operator *(
                    Foo a,
                    Foo b) => a;
            }
            """;

        await Verifysst0018.VerifyCodeFixAsync(Test, FixedSource);
    }
}
