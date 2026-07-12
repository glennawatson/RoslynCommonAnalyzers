// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyVirtualCall = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst1483VirtualCallInConstructorAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1483 (constructors should not call overridable members).</summary>
public class VirtualCallInConstructorAnalyzerUnitTest
{
    /// <summary>Verifies a virtual and an abstract call are reported, whether or not <c>this</c> is written.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task VirtualAndAbstractCallsAreReportedAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public abstract class Control
            {
                protected Control()
                {
                    {|SST1483:Render|}();
                    this.{|SST1483:Layout|}();
                }

                public virtual void Render()
                {
                }

                protected abstract void Layout();
            }
            """);

    /// <summary>Verifies an override is still open to a further override, while a sealed override is closed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverrideIsReportedButSealedOverrideIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public abstract class Base
            {
                public abstract void Init();

                public abstract void Load();
            }

            public class Middle : Base
            {
                public Middle()
                {
                    {|SST1483:Init|}();
                    Load();
                }

                public override void Init()
                {
                }

                public sealed override void Load()
                {
                }
            }
            """);

    /// <summary>Verifies a sealed type has no derived type to surprise, so its constructor is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealedTypeIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Render()
                {
                }
            }

            public sealed class Leaf : Base
            {
                public Leaf() => Render();
            }
            """);

    /// <summary>Verifies a <c>sealed</c> modifier on another partial half still closes the type.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealedOnAnotherPartialHalfIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Render()
                {
                }
            }

            public partial class Leaf : Base
            {
                public Leaf() => Render();
            }

            public sealed partial class Leaf
            {
            }
            """);

    /// <summary>Verifies a struct and a record struct are implicitly sealed, so neither is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StructIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public struct Point
            {
                public Point(int x)
                {
                    Value = x;
                    Text = ToString();
                }

                public int Value { get; set; }

                public string Text { get; set; }
            }

            public record struct Size
            {
                public Size(int x)
                {
                    Value = x;
                    Text = ToString();
                }

                public int Value { get; set; }

                public string Text { get; set; }
            }
            """);

    /// <summary>Verifies a <c>base.</c> call is a non-virtual call and can never land in a derived override.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task BaseQualifiedCallIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Render()
                {
                }
            }

            public class Derived : Base
            {
                public Derived() => base.Render();

                public override void Render()
                {
                }
            }
            """);

    /// <summary>Verifies a call on another instance is that object's business, not this half-built one's.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CallOnAnotherInstanceIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Control
            {
                public virtual void Render()
                {
                }
            }

            public class Host
            {
                public Host(Control other) => other.Render();
            }
            """);

    /// <summary>Verifies a private, a static and a non-virtual member cannot be overridden and are not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PrivateStaticAndNonVirtualCallsAreCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly int _value;

                public C(int value)
                {
                    _value = Compute(value);
                    Helper();
                    Total = _value;
                }

                public int Total { get; set; }

                private static int Compute(int value) => value * 2;

                private void Helper()
                {
                }
            }
            """);

    /// <summary>Verifies naming a virtual property is calling it: every read and every write runs an accessor.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task VirtualPropertyReadAndWriteAreReportedAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual int Width { get; set; }

                public virtual int Height => 1;
            }

            public class Derived : Base
            {
                private readonly int _area;

                public Derived(int width)
                {
                    {|SST1483:Width|} = width;
                    _area = {|SST1483:Width|} * {|SST1483:Height|};
                }
            }
            """);

    /// <summary>Verifies code that only runs later is not code that runs during construction.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A method group builds a delegate, and a lambda or a local function runs when something invokes it — which
    /// may be long after the constructor has returned.
    /// </remarks>
    [Test]
    public async Task MethodGroupLambdaAndLocalFunctionAreCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Render()
                {
                }
            }

            public class Derived : Base
            {
                private readonly System.Action _deferred;

                private readonly System.Action _group;

                public Derived()
                {
                    _deferred = () => Render();
                    _group = Render;

                    void Local() => Render();
                }
            }
            """);

    /// <summary>Verifies <c>nameof</c> yields a name at compile time and dispatches nothing.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NameofIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Render()
                {
                }
            }

            public class Derived : Base
            {
                private readonly string _name;

                public Derived() => _name = nameof(Render);
            }
            """);

    /// <summary>Verifies an object initializer names a virtual member of the object being built, not of this one.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ObjectInitializerMemberIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Box
            {
                public virtual int Width { get; set; }
            }

            public class Host
            {
                private readonly Box _box;

                public Host(int width) => _box = new Box { Width = width };
            }
            """);

    /// <summary>Verifies an inherited virtual event dispatches on subscription, while a field-like one at home does not.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Inside the type that declares it, a field-like event <em>is</em> its backing field: <c>+=</c> combines
    /// delegates directly and no accessor runs, so there is nothing for a derived type to override.
    /// </remarks>
    [Test]
    public async Task InheritedVirtualEventIsReportedAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual event System.EventHandler Refreshed;

                protected void Raise() => Refreshed?.Invoke(this, System.EventArgs.Empty);
            }

            public class Derived : Base
            {
                public Derived() => {|SST1483:Refreshed|} += OnRefreshed;

                private void OnRefreshed(object sender, System.EventArgs e)
                {
                }
            }

            public class Own
            {
                public virtual event System.EventHandler Changed;

                public Own() => Changed += OnChanged;

                protected void Raise() => Changed?.Invoke(this, System.EventArgs.Empty);

                private void OnChanged(object sender, System.EventArgs e)
                {
                }
            }
            """);

    /// <summary>Verifies a primary constructor has nowhere to make the call, while a regular constructor beside it does.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A primary constructor's base arguments and the field initializers it feeds cannot reach an instance member
    /// at all (CS0120, CS0236), so there is no virtual call there to report.
    /// </remarks>
    [Test]
    public async Task PrimaryConstructorIsCleanButRegularConstructorIsReportedAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Control
            {
                public virtual void Render()
                {
                }
            }

            public class Widget(int size) : Control
            {
                private readonly int _size = size;

                public int Size => _size;
            }

            public class Panel(int size) : Control
            {
                private readonly int _size = size;

                public Panel()
                    : this(0)
                {
                    {|SST1483:Render|}();
                }

                public int Size => _size;
            }
            """);

    /// <summary>Verifies a static constructor has no instance to dispatch on.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticConstructorIsCleanAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class Base
            {
                public virtual void Render()
                {
                }
            }

            public class Derived : Base
            {
                private static readonly int Shared;

                static Derived() => Shared = 1;
            }
            """);

    /// <summary>Verifies the virtual members object itself declares are reported like any others.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks><c>object.ToString</c> is virtual, so a derived type that overrides it sees a half-built object.</remarks>
    [Test]
    public async Task ObjectToStringIsReportedAsync()
        => await VerifyVirtualCall.VerifyAnalyzerAsync(
            """
            public class C
            {
                private readonly string _text;

                public C() => _text = {|SST1483:ToString|}();
            }
            """);
}
