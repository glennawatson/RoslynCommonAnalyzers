// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEventNeverRaised = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2407EventNeverRaisedAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2407 (an event nothing in the compilation raises).</summary>
public class EventNeverRaisedAnalyzerUnitTest
{
    /// <summary>Verifies an event nothing raises is reported, on its own declaration.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NeverRaisedEventIsReportedAsync()
        => await VerifyEventNeverRaised.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class Service
            {
                public event EventHandler? {|SST2407:Started|};
            }
            """);

    /// <summary>Verifies subscribing to an event is not raising it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribedButNeverRaisedEventIsReportedAsync()
        => await VerifyEventNeverRaised.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class Service
            {
                public event EventHandler? {|SST2407:Started|};
            }

            public sealed class Listener
            {
                public void Attach(Service service)
                {
                    service.Started += OnStarted;
                    service.Started -= OnStarted;
                }

                private void OnStarted(object? sender, EventArgs args)
                {
                }
            }
            """);

    /// <summary>Verifies an event the type raises is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RaisedEventIsCleanAsync()
        => await VerifyEventNeverRaised.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class Service
            {
                public event EventHandler? Started;

                public void Start() => Started?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies an event raised through a copy of the delegate is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventRaisedThroughACopyIsCleanAsync()
        => await VerifyEventNeverRaised.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class Service
            {
                public event EventHandler? Started;

                public void Start()
                {
                    var handler = Started;
                    handler?.Invoke(this, EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies an event whose raising lives in another file of the same compilation is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventRaisedInAnotherFileIsCleanAsync()
    {
        var test = new VerifyEventNeverRaised.Test
        {
            TestState =
            {
                Sources =
                {
                    """
                    #nullable enable

                    using System;

                    public sealed partial class Service
                    {
                        public event EventHandler? Started;
                    }
                    """,
                    """
                    #nullable enable

                    using System;

                    public sealed partial class Service
                    {
                        public void Start() => Started?.Invoke(this, EventArgs.Empty);
                    }
                    """,
                },
            },
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an event the type does not decide the existence of is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceAndInheritedEventsAreCleanAsync()
        => await VerifyEventNeverRaised.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public interface INotifier
            {
                event EventHandler? Changed;
            }

            public abstract class Notifier
            {
                public abstract event EventHandler? Failed;
            }

            public sealed class Service : Notifier, INotifier
            {
                public event EventHandler? Changed;

                public override event EventHandler? Failed;
            }
            """);

    /// <summary>Verifies a custom event, whose accessors choose their own backing store, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomEventIsCleanAsync()
        => await VerifyEventNeverRaised.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class Service
            {
                private EventHandler? _started;

                public event EventHandler? Started
                {
                    add => _started += value;
                    remove => _started -= value;
                }

                public void Start() => _started?.Invoke(this, EventArgs.Empty);
            }
            """);
}
