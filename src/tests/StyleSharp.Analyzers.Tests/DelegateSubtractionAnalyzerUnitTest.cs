// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDelegateSubtraction = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2448DelegateSubtractionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2448 (delegate subtraction has order-dependent results).</summary>
public class DelegateSubtractionAnalyzerUnitTest
{
    /// <summary>Verifies a binary subtraction of two delegate values is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BinaryDelegateSubtractionIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(System.Action a, System.Action b)
                {
                    var remaining = {|SST2448:a - b|};
                    remaining?.Invoke();
                }
            }
            """);

    /// <summary>Verifies a remove accessor spelled as a binary subtraction is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BinarySubtractionInRemoveAccessorIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private System.EventHandler _handler;

                public event System.EventHandler Changed
                {
                    add => _handler += value;
                    remove => _handler = {|SST2448:_handler - value|};
                }
            }
            """);

    /// <summary>Verifies subtracting an inline delegate combination is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InlineCombinationRemovalIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public event System.EventHandler Changed;

                public void M(System.EventHandler first, System.EventHandler second)
                {
                    Changed -= {|SST2448:first + second|};
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies subtracting a parenthesized inline combination is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParenthesizedCombinationRemovalIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(System.Action target, System.Action a, System.Action b)
                {
                    target -= {|SST2448:(a + b)|};
                    target();
                }
            }
            """);

    /// <summary>Verifies subtracting a local that was built as a combination is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinedLocalRemovalIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(System.Action target, System.Action a, System.Action b)
                {
                    var combined = a + b;
                    target -= {|SST2448:combined|};
                    target();
                }
            }
            """);

    /// <summary>Verifies subtracting a local that was grown with '+=' is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AugmentedLocalRemovalIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(System.Action target, System.Action a, System.Action b)
                {
                    System.Action combined = a;
                    combined += b;
                    target -= {|SST2448:combined|};
                    target();
                }
            }
            """);

    /// <summary>Verifies subtracting a field that the same member combined is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinedFieldRemovalIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private System.EventHandler _all;

                public event System.EventHandler Changed;

                public void M(System.EventHandler extra)
                {
                    _all = _all + extra;
                    Changed -= {|SST2448:_all|};
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies a combined local subtracted from inside a lambda is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CombinedLocalRemovalInsideLambdaIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public System.Action M(System.Action target, System.Action a, System.Action b)
                {
                    var combined = a + b;
                    return () => target -= {|SST2448:combined|};
                }
            }
            """);

    /// <summary>Verifies subtracting a delegate returned by a call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvocationResultRemovalIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public event System.EventHandler Changed;

                public void M()
                {
                    Changed -= {|SST2448:Find()|};
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }

                private System.EventHandler Find() => (s, e) => { };
            }
            """);

    /// <summary>Verifies subtracting a conditionally selected delegate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalRemovalIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(System.Action target, System.Action a, System.Action b, bool flag)
                {
                    target -= {|SST2448:flag ? a : b|};
                    target();
                }
            }
            """);

    /// <summary>Verifies subtracting a coalesced delegate is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CoalesceRemovalIsReportedAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(System.Action target, System.Action a, System.Action b)
                {
                    target -= {|SST2448:a ?? b|};
                    target();
                }
            }
            """);

    /// <summary>Verifies ordinary method-group unsubscription is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupUnsubscriptionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public event System.EventHandler Changed;

                public void M()
                {
                    Changed -= Handler;
                    Changed -= this.Handler;
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }

                private void Handler(object sender, System.EventArgs e)
                {
                }
            }
            """);

    /// <summary>Verifies a generic method-group unsubscription is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericMethodGroupUnsubscriptionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public event System.EventHandler Changed;

                public void M()
                {
                    Changed -= Handle<int>;
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }

                private void Handle<T>(object sender, System.EventArgs e)
                {
                }
            }
            """);

    /// <summary>Verifies lambda and anonymous-method unsubscription is clean; that no-op removal is a different defect.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaUnsubscriptionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public event System.EventHandler Changed;

                public void M()
                {
                    Changed -= (s, e) => { };
                    Changed -= delegate { };
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies unsubscribing a stored handler that was never combined is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StoredHandlerUnsubscriptionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private System.EventHandler _handler;

                public event System.EventHandler Changed;

                public void M()
                {
                    Changed -= _handler;
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }

                public void Store(System.EventHandler handler) => _handler = handler;
            }
            """);

    /// <summary>Verifies unsubscribing an explicitly created delegate is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateCreationUnsubscriptionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public event System.EventHandler Changed;

                public void M()
                {
                    Changed -= new System.EventHandler(Handler);
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }

                private void Handler(object sender, System.EventArgs e)
                {
                }
            }
            """);

    /// <summary>Verifies unsubscribing a cast method group is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastMethodGroupUnsubscriptionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public event System.EventHandler Changed;

                public void M()
                {
                    Changed -= (System.EventHandler)Handler;
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }

                private void Handler(object sender, System.EventArgs e)
                {
                }
            }
            """);

    /// <summary>Verifies unsubscribing a delegate read from a collection is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElementAccessUnsubscriptionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public event System.EventHandler Changed;

                public void M(System.EventHandler[] handlers)
                {
                    Changed -= handlers[0];
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies the mirror-shaped custom remove accessor is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomRemoveAccessorMirrorIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private System.EventHandler _backing;

                public event System.EventHandler Changed
                {
                    add => _backing += value;
                    remove => _backing -= value;
                }
            }
            """);

    /// <summary>Verifies numeric subtraction shapes are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NumericSubtractionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int total, int x, int y)
                {
                    var difference = x - y;
                    total -= 1;
                    total -= x;
                    total -= x * y;
                    total -= x + 1;
                    return total + difference;
                }
            }
            """);

    /// <summary>Verifies a numeric local built by addition and then subtracted is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NumericCombinedLocalIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int total, int a, int b)
                {
                    var offset = a + b;
                    total -= offset;
                    total -= a + b;
                    return total;
                }
            }
            """);

    /// <summary>Verifies subtraction through a user-defined struct operator is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TimeSpanSubtractionIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public System.TimeSpan M(System.DateTime end, System.DateTime start, System.TimeSpan pause)
                {
                    var elapsed = end - start;
                    elapsed -= pause;
                    return elapsed;
                }
            }
            """);

    /// <summary>Verifies a name-only match against a different symbol's combination is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShadowedCombinationNameIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void M(System.Action target, System.Action a, System.Action b)
                {
                    {
                        var mixed = a + b;
                        mixed();
                    }

                    {
                        System.Action mixed = a;
                        target -= mixed;
                        target();
                    }
                }
            }
            """);

    /// <summary>Verifies a field combined only in another member is clean at this site.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FieldCombinedElsewhereIsCleanAsync()
        => await VerifyDelegateSubtraction.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private System.EventHandler _handler;

                public event System.EventHandler Changed;

                public void Combine(System.EventHandler extra) => _handler = _handler + extra;

                public void M()
                {
                    Changed -= _handler;
                    Changed?.Invoke(this, System.EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies a combined local subtracted in a top-level statement is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TopLevelCombinedLocalRemovalIsReportedAsync()
    {
        var test = new VerifyDelegateSubtraction.Test
        {
            TestState =
            {
                OutputKind = Microsoft.CodeAnalysis.OutputKind.ConsoleApplication,
            },
            TestCode = """
                System.Action a = () => { };
                System.Action b = () => { };
                System.Action target = a;
                var combined = a + b;
                target -= {|SST2448:combined|};
                target();
                """,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
