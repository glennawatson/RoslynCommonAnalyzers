// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyRedeclaredFieldLikeEvent = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2456RedeclaredFieldLikeEventAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2456 (a field-like event that overrides or hides an inherited event).</summary>
public class RedeclaredFieldLikeEventAnalyzerUnitTest
{
    /// <summary>Verifies a field-like override of a virtual event is reported on the override.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideOfVirtualEventIsReportedAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public class Publisher
            {
                public virtual event EventHandler Changed;

                protected void Raise() => Changed?.Invoke(this, EventArgs.Empty);
            }

            public class LoudPublisher : Publisher
            {
                public override event EventHandler {|SST2456:Changed|};

                public void RaiseLoud() => Changed?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies a field-like override of an abstract event is reported on the override.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideOfAbstractEventIsReportedAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public abstract class Publisher
            {
                public abstract event EventHandler Changed;
            }

            public sealed class Timer : Publisher
            {
                public override event EventHandler {|SST2456:Changed|};

                public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies the generic handler override form is reported the same way.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericHandlerOverrideIsReportedAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public class Publisher
            {
                public virtual event EventHandler<EventArgs> Completed;

                protected void Raise() => Completed?.Invoke(this, EventArgs.Empty);
            }

            public class LoudPublisher : Publisher
            {
                public override event EventHandler<EventArgs> {|SST2456:Completed|};

                public void RaiseLoud() => Completed?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies every event a single override declaration declares is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EachEventInOverrideDeclarationIsReportedAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public class Publisher
            {
                public virtual event EventHandler Started, Stopped;

                protected void Raise()
                {
                    Started?.Invoke(this, EventArgs.Empty);
                    Stopped?.Invoke(this, EventArgs.Empty);
                }
            }

            public class LoudPublisher : Publisher
            {
                public override event EventHandler {|SST2456:Started|}, {|SST2456:Stopped|};

                public void RaiseLoud()
                {
                    Started?.Invoke(this, EventArgs.Empty);
                    Stopped?.Invoke(this, EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies a field-like <c>new</c> event that hides an inherited event is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewFieldLikeEventHidingInheritedEventIsReportedAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public class Publisher
            {
                public event EventHandler Changed;

                protected void Raise() => Changed?.Invoke(this, EventArgs.Empty);
            }

            public class SilentPublisher : Publisher
            {
                public new event EventHandler {|SST2456:Changed|};

                public void RaiseNew() => Changed?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies a plain field-like event keeps its single backing field and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainFieldLikeEventIsCleanAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public class Publisher
            {
                public event EventHandler Changed;

                public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies a virtual field-like event with no override or new is left to the other rule.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VirtualFieldLikeEventWithoutOverrideOrNewIsCleanAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public class Publisher
            {
                public virtual event EventHandler Changed;

                public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies an abstract event has no backing field to split and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractEventIsCleanAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public abstract class Publisher
            {
                public abstract event EventHandler Changed;
            }
            """);

    /// <summary>Verifies an abstract override that re-abstracts declares no backing field and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AbstractOverrideEventIsCleanAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public abstract class Publisher
            {
                public abstract event EventHandler Changed;
            }

            public abstract class MidPublisher : Publisher
            {
                public abstract override event EventHandler Changed;
            }
            """);

    /// <summary>Verifies an override with explicit accessors shares the author's storage and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OverrideEventWithExplicitAccessorsIsCleanAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public class Publisher
            {
                private EventHandler _changed;

                public virtual event EventHandler Changed
                {
                    add => _changed += value;
                    remove => _changed -= value;
                }

                protected void Raise() => _changed?.Invoke(this, EventArgs.Empty);
            }

            public class LoudPublisher : Publisher
            {
                private EventHandler _loud;

                public override event EventHandler Changed
                {
                    add => _loud += value;
                    remove => _loud -= value;
                }
            }
            """);

    /// <summary>Verifies an interface event declares no storage and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceEventIsCleanAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public interface IPublisher
            {
                event EventHandler Changed;
            }
            """);

    /// <summary>Verifies a <c>new</c> interface event hides no backing storage and is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewInterfaceEventHidingBaseInterfaceEventIsCleanAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public interface IPublisher
            {
                event EventHandler Changed;
            }

            public interface ILoudPublisher : IPublisher
            {
                new event EventHandler Changed;
            }
            """);

    /// <summary>Verifies a <c>new</c> field-like event that hides nothing is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpuriousNewFieldLikeEventIsCleanAsync()
        => await VerifyRedeclaredFieldLikeEvent.VerifyAnalyzerAsync(
            """
            using System;

            public class Publisher
            {
                public new event EventHandler Changed;

                public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
            }
            """);
}
