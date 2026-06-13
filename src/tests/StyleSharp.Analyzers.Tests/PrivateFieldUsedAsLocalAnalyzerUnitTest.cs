// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFieldLocal = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1422PrivateFieldUsedAsLocalAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1422 (private field used only as local storage).</summary>
public class PrivateFieldUsedAsLocalAnalyzerUnitTest
{
    /// <summary>Verifies a field reset at the start of its only using method is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResetTemporaryFieldIsReportedAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1422:_total|};

                public int Sum(int value)
                {
                    _total = 0;
                    _total += value;
                    return _total;
                }
            }
            """);

    /// <summary>Verifies cross-method state is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FieldUsedByMultipleMethodsIsCleanAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _total;

                public void Add(int value) => _total += value;

                public int Read() => _total;
            }
            """);

    /// <summary>Verifies several scratch fields in one type are each reported independently.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MultipleScratchFieldsAreEachReportedAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1422:_a|};
                private int {|SST1422:_b|};

                public int First(int value)
                {
                    _a = 0;
                    _a += value;
                    return _a;
                }

                public int Second(int value)
                {
                    _b = 0;
                    _b += value;
                    return _b;
                }
            }
            """);

    /// <summary>Verifies a same-named local in another method does not count as a second using method.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SameNameLocalInOtherMethodDoesNotBlockReportAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1422:_total|};

                public int Sum(int value)
                {
                    _total = 0;
                    _total += value;
                    return _total;
                }

                public int Other()
                {
                    int _total = 7;
                    return _total;
                }
            }
            """);

    /// <summary>Verifies a field referenced inside a lambda is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FieldReferencedInsideLambdaIsCleanAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private int _total;

                public int Sum(int value)
                {
                    _total = 0;
                    Action add = () => _total += value;
                    add();
                    return _total;
                }
            }
            """);

    /// <summary>Verifies a lazily-initialized cache (<c>??=</c> reads before writing) is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LazyInitializedFieldIsCleanAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            #nullable enable
            using System;

            public sealed class C
            {
                private Action? _handler;

                public void Post(Action drain)
                {
                    _handler ??= drain;
                    Run(_handler);
                }

                private static void Run(Action handler) => handler();
            }
            """);

    /// <summary>Verifies a compound assignment as the first access (read before write) is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CompoundAssignmentFirstAccessIsCleanAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _acc;

                public int Add(int value)
                {
                    _acc += value;
                    return _acc;
                }
            }
            """);

    /// <summary>Verifies a guarded null-assignment (lazy cache) is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GuardedNullAssignmentIsCleanAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            #nullable enable
            public sealed class C
            {
                private string? _cache;

                public string Get(string compute)
                {
                    if (_cache is null)
                    {
                        _cache = compute;
                    }

                    return _cache;
                }
            }
            """);

    /// <summary>Verifies a field whose reset reads its own previous value (a running counter) is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SelfReferencingResetIsCleanAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _counter;

                public int Next()
                {
                    _counter = _counter + 1;
                    return _counter;
                }
            }
            """);

    /// <summary>Verifies an unconditional reset that does not read the field is still reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnconditionalResetStillReportedAsync()
        => await VerifyFieldLocal.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int {|SST1422:_scratch|};

                public int Compute(int value)
                {
                    _scratch = value * 2;
                    return _scratch;
                }
            }
            """);
}
