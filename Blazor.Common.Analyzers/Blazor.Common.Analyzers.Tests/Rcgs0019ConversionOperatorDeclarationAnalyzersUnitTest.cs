// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verifyrcgs0019 = Blazor.Common.Analyzers.Tests.CSharpCodeFixVerifier<
    Blazor.Common.Analyzers.Rcgs0019ConversionOperatorDeclarationParameterMustBeOnUniqueLinesAnalyzer,
    Blazor.Common.Analyzers.Rcgs0019ConversionOperatorDeclarationParameterMustBeOnUniqueLinesCodeFixProvider>;

namespace Blazor.Common.Analyzers.Tests;

/// <summary>Unit tests for the RCGS0019 analyzer that requires conversion operator declaration parameters to be on unique lines.</summary>
public class Rcgs0019ConversionOperatorDeclarationAnalyzersUnitTest
{
    /// <summary>Verifies a conversion operator declaration on a single line produces no diagnostics.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidAsync()
    {
        const string test = """
            public class Foo
            {
                public static implicit operator int(Foo f) => 0;
            }
            """;

        await Verifyrcgs0019.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies a conversion operator declaration whose single parameter spans multiple lines still produces no diagnostics, because a single parameter cannot violate the rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleParameterAcrossLinesProducesNoDiagnosticAsync()
    {
        const string test = """
            public class Foo
            {
                public static implicit operator int(
                    Foo f) => 0;
            }
            """;

        await Verifyrcgs0019.VerifyAnalyzerAsync(test);
    }

    /// <summary>Verifies the code fix is not offered when no diagnostic is produced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CodeFixNotAppliedAsync()
    {
        const string test = """
            public class Foo
            {
                public static implicit operator int(
                    Foo f) => 0;
            }
            """;

        await Verifyrcgs0019.VerifyCodeFixAsync(test, test);
    }
}
