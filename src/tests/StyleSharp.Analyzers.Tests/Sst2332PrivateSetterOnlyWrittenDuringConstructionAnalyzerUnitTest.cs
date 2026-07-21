// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySetter = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2332PrivateSetterOnlyWrittenDuringConstructionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2332 (make a construction-only property get-only).</summary>
public class Sst2332PrivateSetterOnlyWrittenDuringConstructionAnalyzerUnitTest
{
    /// <summary>Verifies a private setter written only in the constructor is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SetterWrittenOnlyInConstructorIsReportedAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public int {|SST2332:Value|} { get; private set; }

                public Counter(int value) => Value = value;
            }
            """);

    /// <summary>Verifies a property carrying an initializer as well as a constructor write is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SetterWithInitializerIsReportedAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public int {|SST2332:Value|} { get; private set; } = 5;

                public Counter(int value) => Value = value;
            }
            """);

    /// <summary>Verifies a private setter written after construction is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SetterWrittenInMethodIsCleanAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public int Value { get; private set; }

                public void Set(int value) => Value = value;
            }
            """);

    /// <summary>Verifies a private setter written through <c>this</c> after construction is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SetterWrittenThroughThisInMethodIsCleanAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public int Value { get; private set; }

                public void Reset() => this.Value = 0;
            }
            """);

    /// <summary>Verifies an <c>init</c> accessor is not this rule's concern.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InitAccessorIsCleanAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public int Value { get; init; }
            }
            """);

    /// <summary>Verifies a public setter is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicSetterIsCleanAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public int Value { get; set; }
            }
            """);

    /// <summary>Verifies a partial type is skipped, because a write could sit in an unseen part.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PartialTypeIsCleanAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            public partial class Counter
            {
                public int Value { get; private set; }

                public Counter(int value) => Value = value;
            }
            """);

    /// <summary>Verifies a private setter reached only through a compound assignment in the constructor is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CompoundAssignmentInConstructorIsReportedAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            public class Counter
            {
                public int {|SST2332:Value|} { get; private set; }

                public Counter(int value)
                {
                    Value = 0;
                    Value += value;
                }
            }
            """);

    /// <summary>Verifies a private setter written inside a lambda in the constructor is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The lambda may run after the constructor returns, so the write is not construction-time.</remarks>
    [Test]
    public async Task SetterWrittenInLambdaIsCleanAsync()
        => await VerifySetter.VerifyAnalyzerAsync(
            """
            using System;

            public class Counter
            {
                public int Value { get; private set; }

                public Action Update { get; }

                public Counter() => Update = () => Value = 1;
            }
            """);
}
