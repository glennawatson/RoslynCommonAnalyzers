// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEvents = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2304EventHandlerSignatureAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2304 (events should use the standard handler signature).</summary>
public class Sst2304EventHandlerSignatureAnalyzerUnitTest
{
    /// <summary>Verifies an event whose delegate has an unrelated shape is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CustomDelegateWithTheWrongShapeIsReportedAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            public delegate void ValueChanged(int oldValue, int newValue);

            public class Slider
            {
                public event ValueChanged {|SST2304:Changed|};
            }
            """);

    /// <summary>Verifies a second parameter that carries no event payload is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonEventArgsPayloadIsReportedAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            public delegate void Notified(object sender, string payload);

            public class Feed
            {
                public event Notified {|SST2304:Updated|};
            }
            """);

    /// <summary>Verifies a handler that returns something is reported: an event has no one to return it to.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task HandlerWithAReturnValueIsReportedAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            using System;

            public delegate bool Approving(object sender, EventArgs e);

            public class Gate
            {
                public event Approving {|SST2304:Approved|};
            }
            """);

    /// <summary>Verifies the framework's own handler delegates are the shape by definition.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FrameworkHandlersAreCleanAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            using System;

            public class ValueChangedEventArgs : EventArgs
            {
            }

            public class Slider
            {
                public event EventHandler Closed;

                public event EventHandler<ValueChangedEventArgs> Changed;
            }
            """);

    /// <summary>Verifies a hand-written delegate of the right shape is told to become the framework handler.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The shape already matches, so the message names the exact replacement.</remarks>
    [Test]
    public async Task CustomDelegateWithTheRightShapeIsReportedWithItsReplacementAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            using System;

            public class ValueChangedEventArgs : EventArgs
            {
            }

            public delegate void ValueChangedHandler(object sender, ValueChangedEventArgs e);

            public class Slider
            {
                public event ValueChangedHandler Changed;
            }
            """,
            VerifyEvents.Diagnostic().WithSpan(11, 38, 11, 45).WithArguments("Changed", "EventHandler<ValueChangedEventArgs>", "ValueChangedHandler"));

    /// <summary>Verifies a payload of exactly <c>EventArgs</c> is pointed at the non-generic handler.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EventArgsPayloadSuggestsTheNonGenericHandlerAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            using System;

            public delegate void Poked(object sender, EventArgs e);

            public class Button
            {
                public event Poked Pressed;
            }
            """,
            VerifyEvents.Diagnostic().WithSpan(7, 24, 7, 31).WithArguments("Pressed", "EventHandler", "Poked"));

    /// <summary>Verifies a generic delegate constrained to <c>EventArgs</c> is offered the generic handler over its own type parameter.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenericHandlerConstrainedToEventArgsIsReportedWithItsReplacementAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            using System;

            public delegate void Handler<T>(object sender, T e)
                where T : EventArgs;

            public class Feed<T>
                where T : EventArgs
            {
                public event Handler<T> Updated;
            }
            """,
            VerifyEvents.Diagnostic().WithSpan(9, 29, 9, 36).WithArguments("Updated", "EventHandler<T>", "Handler<T>"));

    /// <summary>Verifies a constructed generic delegate is offered the handler closed over the same payload.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructedGenericHandlerIsReportedWithItsReplacementAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class MovedEventArgs : EventArgs
            {
            }

            public delegate void Handler<T>(object sender, T e)
                where T : EventArgs;

            public class Feed
            {
                public event Handler<MovedEventArgs> Moved;
            }
            """,
            VerifyEvents.Diagnostic().WithSpan(12, 42, 12, 47).WithArguments("Moved", "EventHandler<MovedEventArgs>", "Handler<MovedEventArgs>"));

    /// <summary>Verifies a by-reference parameter disqualifies a delegate from the mechanical replacement.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The signature differs from the standard shape, so only the shape itself is suggested.</remarks>
    [Test]
    public async Task ByRefLookalikeIsNotOfferedTheMechanicalReplacementAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            using System;

            public delegate void Prodded(ref object sender, EventArgs e);

            public class Widget
            {
                public event Prodded Poked;
            }
            """,
            VerifyEvents.Diagnostic().WithSpan(7, 26, 7, 31).WithArguments("Poked", "EventHandler<TEventArgs>", "Prodded"));

    /// <summary>Verifies a right-shape delegate dictated by an interface is reported at the interface, not the implementation.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RightShapeInterfaceImplementationIsReportedAtItsSourceAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class MovedEventArgs : EventArgs
            {
            }

            public delegate void MovedHandler(object sender, MovedEventArgs e);

            public interface ITracker
            {
                event MovedHandler {|SST2304:Moved|};
            }

            public class Tracker : ITracker
            {
                public event MovedHandler Moved;
            }
            """);

    /// <summary>Verifies an implementing event is not reported; the interface that dictates the shape is.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InterfaceImplementationIsReportedAtItsSourceAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            public delegate void ValueChanged(int value);

            public interface ISlider
            {
                event ValueChanged {|SST2304:Changed|};
            }

            public class Slider : ISlider
            {
                public event ValueChanged Changed;
            }

            public class Dial : ISlider
            {
                event ValueChanged ISlider.Changed
                {
                    add { }
                    remove { }
                }
            }
            """);

    /// <summary>Verifies an override takes its shape from the declaration it follows.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverrideIsReportedAtItsSourceAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            public delegate void ValueChanged(int value);

            public abstract class Control
            {
                public abstract event ValueChanged {|SST2304:Changed|};
            }

            public class Slider : Control
            {
                public override event ValueChanged Changed;
            }
            """);

    /// <summary>Verifies a class that declares no events is never looked at.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TypeWithoutEventsIsCleanAsync()
        => await VerifyEvents.VerifyAnalyzerAsync(
            """
            public delegate void ValueChanged(int oldValue, int newValue);

            public class Slider
            {
                public ValueChanged Callback { get; set; }
            }
            """);
}
