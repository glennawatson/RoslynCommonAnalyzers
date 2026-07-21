// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySetter = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2332PrivateSetterOnlyWrittenDuringConstructionAnalyzer,
    StyleSharp.Analyzers.Sst2332PrivateSetterOnlyWrittenDuringConstructionCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2332PrivateSetterOnlyWrittenDuringConstructionCodeFixProvider"/> (SST2332 make get-only).</summary>
public class Sst2332PrivateSetterOnlyWrittenDuringConstructionCodeFixUnitTest
{
    /// <summary>A private-set property written only in the constructor.</summary>
    private const string ConstructorOnlySource = """
        public class Counter
        {
            public int {|SST2332:Value|} { get; private set; }

            public Counter(int value) => Value = value;
        }
        """;

    /// <summary>The property after the fix becomes get-only.</summary>
    private const string ConstructorOnlyFixed = """
        public class Counter
        {
            public int Value { get; }

            public Counter(int value) => Value = value;
        }
        """;

    /// <summary>A private-set property that also carries an initializer.</summary>
    private const string WithInitializerSource = """
        public class Counter
        {
            public int {|SST2332:Value|} { get; private set; } = 5;

            public Counter(int value) => Value = value;
        }
        """;

    /// <summary>The property after the fix keeps its initializer.</summary>
    private const string WithInitializerFixed = """
        public class Counter
        {
            public int Value { get; } = 5;

            public Counter(int value) => Value = value;
        }
        """;

    /// <summary>Verifies the fix removes the private setter, leaving a get-only property.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RemovesPrivateSetterAsync()
        => await VerifySetter.VerifyCodeFixAsync(ConstructorOnlySource, ConstructorOnlyFixed);

    /// <summary>Verifies the fix keeps the property's initializer.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task KeepsInitializerAsync()
        => await VerifySetter.VerifyCodeFixAsync(WithInitializerSource, WithInitializerFixed);
}
