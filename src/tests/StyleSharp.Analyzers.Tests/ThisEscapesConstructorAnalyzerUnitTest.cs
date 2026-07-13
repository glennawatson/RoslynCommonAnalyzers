// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyThisEscapes = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2403ThisEscapesConstructorAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2403 (a half-built instance escaping its own constructor).</summary>
public class ThisEscapesConstructorAnalyzerUnitTest
{
    /// <summary>Verifies handing the object to somebody else as an argument is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentEscapeIsReportedAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            public static class Registry
            {
                public static void Add(object item)
                {
                }
            }

            public sealed class C
            {
                public C() => Registry.Add({|SST2403:this|});
            }
            """);

    /// <summary>Verifies handing the object to another object being built is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectCreationEscapeIsReportedAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            public sealed class Child
            {
                public Child(object parent) => Parent = parent;

                public object Parent { get; }
            }

            public sealed class C
            {
                public C() => Attached = new Child({|SST2403:this|});

                public Child Attached { get; }
            }
            """);

    /// <summary>Verifies storing the object in the type's own static state is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticFieldEscapeIsReportedAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            #nullable enable

            public sealed class C
            {
                public static C? Current;

                public C() => Current = {|SST2403:this|};
            }
            """);

    /// <summary>Verifies subscribing a closure over the object to another object's event is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventSubscriptionEscapeIsReportedAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class Service
            {
                public event EventHandler? Updated;

                public void Raise() => Updated?.Invoke(this, EventArgs.Empty);
            }

            public sealed class C
            {
                public C(Service service) => service.Updated += {|SST2403:(sender, args) => this.Refresh()|};

                private void Refresh()
                {
                }
            }
            """);

    /// <summary>Verifies a closure that touches the object several times is still one escape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClosureIsReportedOnceAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public C(Action<Action> schedule)
                    => schedule({|SST2403:() =>
                    {
                        this.Refresh();
                        this.Refresh();
                    }|});

                private void Refresh()
                {
                }
            }
            """);

    /// <summary>Verifies the object talking to itself is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OwnMemberAccessIsCleanAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly int _value;

                public C(int value)
                {
                    this._value = value;
                    this.Initialize();
                }

                public int Value => this._value;

                private void Initialize()
                {
                }
            }
            """);

    /// <summary>Verifies storing the object in its own instance state, or in a local, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OwnStateAndLocalsAreCleanAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly object _self;

                public C()
                {
                    _self = this;
                    var alias = this;
                    Alias = alias;
                }

                public object Alias { get; }

                public object Self => _self;
            }
            """);

    /// <summary>Verifies subscribing to the object's own event is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OwnEventSubscriptionIsCleanAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public C() => this.Updated += (sender, args) => this.Refresh();

                public event EventHandler? Updated;

                public void Raise() => Updated?.Invoke(this, EventArgs.Empty);

                private void Refresh()
                {
                }
            }
            """);

    /// <summary>Verifies passing a member's value, rather than the object, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PassingAMemberValueIsCleanAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private readonly string _name;

                public C(string name)
                {
                    _name = name;
                    Console.WriteLine(this._name);
                }
            }
            """);

    /// <summary>Verifies a method other than a constructor is not measured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MethodIsCleanAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            public static class Registry
            {
                public static void Add(object item)
                {
                }
            }

            public sealed class C
            {
                public void Publish() => Registry.Add(this);
            }
            """);

    /// <summary>Verifies a struct built around <c>this</c> and stored in this object's own field is not reported.</summary>
    /// <remarks>
    /// A struct field's storage is part of the object, so the copy holding the reference lives inside the
    /// very object it points at. There is no second reference for anything else to reach it through, and
    /// nothing can get to the struct without already holding the object.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StructStoredInThisObjectsOwnFieldIsNotReportedAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            using System;

            public readonly struct Pump(object owner, Action drain)
            {
                public object Owner => owner;

                public void Run() => drain();
            }

            public sealed class C
            {
                private Pump _pump;

                public C() => _pump = new(this, Drain);

                private void Drain()
                {
                }
            }
            """);

    /// <summary>Verifies a class built around <c>this</c> and stored in this object's own field is still reported.</summary>
    /// <remarks>
    /// A class is a separate object the constructor could have handed somewhere else first, so storing it
    /// in a field proves nothing about who else can already see it.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ClassStoredInThisObjectsOwnFieldIsStillReportedAsync()
        => await VerifyThisEscapes.VerifyAnalyzerAsync(
            """
            public sealed class Sink(object owner)
            {
                public object Owner => owner;
            }

            public sealed class C
            {
                private readonly Sink _sink;

                public C() => _sink = new({|SST2403:this|});
            }
            """);
}
