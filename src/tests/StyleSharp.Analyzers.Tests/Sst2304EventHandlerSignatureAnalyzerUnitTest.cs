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

    /// <summary>Verifies a hand-written delegate of the right shape is left alone.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The rule is about the shape a consumer can handle, not about the name of the delegate.</remarks>
    [Test]
    public async Task CustomDelegateWithTheRightShapeIsCleanAsync()
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
            """);

    /// <summary>Verifies a generic delegate is judged by what its constraint promises.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenericHandlerConstrainedToEventArgsIsCleanAsync()
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
