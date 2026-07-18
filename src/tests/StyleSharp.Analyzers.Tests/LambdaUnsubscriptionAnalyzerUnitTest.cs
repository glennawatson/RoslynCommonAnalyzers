// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLambdaUnsubscription = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2449LambdaUnsubscriptionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2449 (unsubscribing an event or delegate with a freshly created lambda).</summary>
public class LambdaUnsubscriptionAnalyzerUnitTest
{
    /// <summary>Verifies unsubscribing an event with a lambda is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventUnsubscribedWithLambdaIsReportedAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public event EventHandler Saved;

                public void Detach() => Saved -= {|SST2449:(sender, args) => Console.WriteLine(sender)|};
            }
            """);

    /// <summary>Verifies unsubscribing an event with an anonymous method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventUnsubscribedWithAnonymousMethodIsReportedAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public event EventHandler Saved;

                public void Detach() => Saved -= {|SST2449:delegate { }|};
            }
            """);

    /// <summary>Verifies unsubscribing another object's event with a parenthesized lambda is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedEventUnsubscribedWithParenthesizedLambdaIsReportedAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Publisher
            {
                public event EventHandler Saved;

                public void Raise() => Saved?.Invoke(this, EventArgs.Empty);
            }

            public sealed class Subscriber
            {
                public void Detach(Publisher publisher) => publisher.Saved -= ({|SST2449:(sender, args) => { }|});
            }
            """);

    /// <summary>Verifies subtracting a lambda from a delegate-typed local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateLocalSubtractedWithLambdaIsReportedAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Run()
                {
                    Action<int> pipeline = _ => { };
                    pipeline -= {|SST2449:_ => { }|};
                    pipeline(1);
                }
            }
            """);

    /// <summary>Verifies subtracting a lambda from a delegate-typed field is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateFieldSubtractedWithLambdaIsReportedAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private Action _pipeline = () => { };

                public void Reset() => _pipeline -= {|SST2449:() => { }|};

                public void Run() => _pipeline();
            }
            """);

    /// <summary>Verifies subtracting a lambda from a delegate-typed property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegatePropertySubtractedWithLambdaIsReportedAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public Action Pipeline { get; set; } = () => { };

                public void Reset() => Pipeline -= {|SST2449:() => { }|};
            }
            """);

    /// <summary>Verifies subtracting a lambda from a delegate-typed parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateParameterSubtractedWithLambdaIsReportedAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void Detach(Action pipeline)
                {
                    pipeline -= {|SST2449:() => { }|};
                    pipeline();
                }
            }
            """);

    /// <summary>Verifies unsubscribing with a method group is clean: it compares equal by method and target.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodGroupUnsubscriptionIsCleanAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public event EventHandler Saved;

                public void Attach() => Saved += OnSaved;

                public void Detach() => Saved -= OnSaved;

                private void OnSaved(object sender, EventArgs e)
                {
                }
            }
            """);

    /// <summary>Verifies subscribing with a lambda is clean: only the removal is this rule's business.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LambdaSubscriptionIsCleanAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public event EventHandler Saved;

                public void Attach() => Saved += (sender, args) => { };

                public void Raise() => Saved?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies unsubscribing with a stored delegate is clean: it is the same reference that was added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StoredDelegateUnsubscriptionIsCleanAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public event EventHandler Saved;

                private EventHandler _handler;

                public C()
                {
                    _handler = (sender, args) => { };
                    Saved += _handler;
                }

                public void Detach() => Saved -= _handler;
            }
            """);

    /// <summary>Verifies arithmetic compound subtraction is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NumericCompoundSubtractionIsCleanAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int Remaining { get; private set; }

                public void Consume(int amount) => Remaining -= amount;
            }
            """);

    /// <summary>Verifies a custom subtraction operator that accepts a delegate is clean: nothing is being unsubscribed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomSubtractionOperatorTakingDelegateIsCleanAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Pipeline
            {
                public static Pipeline operator -(Pipeline source, Func<int> stage) => source;
            }

            public sealed class C
            {
                public Pipeline Stages { get; set; } = new Pipeline();

                public void Trim() => Stages -= () => 1;
            }
            """);

    /// <summary>Verifies subtracting a lambda from a delegate array element is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateArrayElementSubtractionIsCleanAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private readonly Action[] _handlers = new Action[1];

                public void Reset() => _handlers[0] -= () => { };
            }
            """);

    /// <summary>Verifies a target that does not bind is not reported: broken code is the compiler's to explain.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnresolvedTargetIsCleanAsync()
        => await VerifyLambdaUnsubscription.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public void Detach() => {|CS0103:missing|} -= (int x) => { };
            }
            """);
}
