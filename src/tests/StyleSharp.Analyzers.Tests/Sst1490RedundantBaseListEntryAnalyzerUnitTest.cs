// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyBaseList = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1490RedundantBaseListEntryAnalyzer,
    StyleSharp.Analyzers.Sst1490RedundantBaseListEntryCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1490 (base lists should not state what is already implied) and its fix.</summary>
public class Sst1490RedundantBaseListEntryAnalyzerUnitTest
{
    /// <summary>Verifies an interface another interface in the list inherits is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImpliedByAnotherInterfaceIsRemovedAsync()
    {
        const string Source = """
                              public interface IBase
                              {
                                  void Run();
                              }

                              public interface IDerived : IBase
                              {
                              }

                              public sealed class C : IDerived, {|SST1490:IBase|}
                              {
                                  public void Run()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public interface IBase
                                   {
                                       void Run();
                                   }

                                   public interface IDerived : IBase
                                   {
                                   }

                                   public sealed class C : IDerived
                                   {
                                       public void Run()
                                       {
                                       }
                                   }
                                   """;
        await VerifyBaseList.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the entry is reported wherever it sits in the list, not only at the end.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImpliedInterfaceIsRemovedFromTheMiddleOfTheListAsync()
    {
        const string Source = """
                              public interface IBase
                              {
                              }

                              public interface IDerived : IBase
                              {
                              }

                              public interface IOther
                              {
                              }

                              public sealed class C : {|SST1490:IBase|}, IDerived, IOther
                              {
                              }
                              """;
        const string FixedSource = """
                                   public interface IBase
                                   {
                                   }

                                   public interface IDerived : IBase
                                   {
                                   }

                                   public interface IOther
                                   {
                                   }

                                   public sealed class C : IDerived, IOther
                                   {
                                   }
                                   """;
        await VerifyBaseList.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an interface a listed base class already implements is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImpliedByBaseClassIsRemovedAsync()
    {
        const string Source = """
                              public interface IRunnable
                              {
                                  void Run();
                              }

                              public class Base : IRunnable
                              {
                                  public void Run()
                                  {
                                  }
                              }

                              public sealed class Derived : Base, {|SST1490:IRunnable|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   public interface IRunnable
                                   {
                                       void Run();
                                   }

                                   public class Base : IRunnable
                                   {
                                       public void Run()
                                       {
                                       }
                                   }

                                   public sealed class Derived : Base
                                   {
                                   }
                                   """;
        await VerifyBaseList.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a generic interface implied by its constructed base interface is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructedInterfaceIsMatchedByItsTypeArgumentsAsync()
    {
        const string Source = """
                              using System.Collections;
                              using System.Collections.Generic;

                              public sealed class Bag<T> : IReadOnlyList<T>, {|SST1490:IEnumerable<T>|}
                              {
                                  public int Count => 0;

                                  public T this[int index] => default;

                                  public IEnumerator<T> GetEnumerator() => default;

                                  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections;
                                   using System.Collections.Generic;

                                   public sealed class Bag<T> : IReadOnlyList<T>
                                   {
                                       public int Count => 0;

                                       public T this[int index] => default;

                                       public IEnumerator<T> GetEnumerator() => default;

                                       IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
                                   }
                                   """;
        await VerifyBaseList.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies every entry the list already implies is reported, and a fix-all removes them all.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryImpliedEntryOfOneListIsReportedAsync()
    {
        const string Source = """
                              public interface IA
                              {
                              }

                              public interface IB : IA
                              {
                              }

                              public interface IC : IB
                              {
                              }

                              public sealed class C : IC, {|SST1490:IB|}, {|SST1490:IA|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   public interface IA
                                   {
                                   }

                                   public interface IB : IA
                                   {
                                   }

                                   public interface IC : IB
                                   {
                                   }

                                   public sealed class C : IC
                                   {
                                   }
                                   """;
        await VerifyBaseList.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an interface a struct's other interface inherits is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StructBaseListIsCheckedAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IShape
            {
            }

            public interface IPolygon : IShape
            {
            }

            public struct Square : IPolygon, {|SST1490:IShape|}
            {
            }
            """);

    /// <summary>Verifies an interface list that implies nothing is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedInterfacesAreCleanAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IFirst
            {
            }

            public interface ISecond
            {
            }

            public class Base
            {
            }

            public sealed class C : Base, IFirst, ISecond
            {
            }
            """);

    /// <summary>Verifies a single-entry base list is never reported; nothing else in it could imply the entry.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleEntryBaseListIsCleanAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IMarker
            {
            }

            public sealed class C : IMarker
            {
            }
            """);

    /// <summary>Verifies an explicit object base is left to the rule that already owns it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The type is still a class, not an interface, so this rule never considers it.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExplicitObjectBaseIsNotThisRulesJobAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IMarker
            {
            }

            public sealed class C : object, IMarker
            {
            }
            """);

    /// <summary>Verifies a partial type is judged only by the base list being read, not by its other parts.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PartialTypeIsJudgedOneBaseListAtATimeAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IBase
            {
            }

            public interface IDerived : IBase
            {
            }

            public partial class C : IDerived
            {
            }

            public partial class C : IBase
            {
            }
            """);

    /// <summary>Verifies an interface re-listed to re-implement it over a base class is kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Removing the entry would send the interface call back to the base class's member, which is a silent
    /// behavior change rather than a cleanup.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceReimplementedOverABaseClassIsKeptAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IRunnable
            {
                void Run();
            }

            public class Base : IRunnable
            {
                public void Run()
                {
                }
            }

            public sealed class Derived : Base, IRunnable
            {
                public new void Run()
                {
                }
            }
            """);

    /// <summary>Verifies an entry that an explicit implementation depends on is kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Removing this entry would not compile: the explicit implementation would have no interface.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceWithAnExplicitImplementationOverABaseClassIsKeptAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IRunnable
            {
                void Run();
            }

            public class Base : IRunnable
            {
                public void Run()
                {
                }
            }

            public sealed class Derived : Base, IRunnable
            {
                void IRunnable.Run()
                {
                }
            }
            """);

    /// <summary>Verifies an override of the base member does not keep the redundant entry alive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>An override is reached through the base class's own mapping and still runs once the entry goes.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverrideOfTheBaseImplementationStillReportsAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IRunnable
            {
                void Run();
            }

            public class Base : IRunnable
            {
                public virtual void Run()
                {
                }
            }

            public sealed class Derived : Base, {|SST1490:IRunnable|}
            {
                public override void Run()
                {
                }
            }
            """);

    /// <summary>Verifies an entry that re-implements the interface onto a hidden base member is kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The member answering the re-implemented interface is the base class's own <c>new</c> member, not one
    /// declared here: the interface reaches <c>Middle.Run</c> with the entry and <c>Base.Run</c> without it.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceReimplementedOntoAHiddenBaseMemberIsKeptAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IRunnable
            {
                void Run();
            }

            public class Base : IRunnable
            {
                public void Run()
                {
                }
            }

            public class Middle : Base
            {
                public new void Run()
                {
                }
            }

            public sealed class Derived : Middle, IRunnable
            {
            }
            """);

    /// <summary>Verifies a hidden base property keeps the entry alive the same way a hidden method does.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceReimplementedOntoAHiddenBasePropertyIsKeptAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface ICounter
            {
                int Count { get; }
            }

            public class Base : ICounter
            {
                public int Count => 1;
            }

            public class Middle : Base
            {
                public new int Count => 2;
            }

            public sealed class Derived : Middle, ICounter
            {
            }
            """);

    /// <summary>Verifies the hidden member is found however far up the base chain it sits.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceReimplementedOntoAMemberHiddenFurtherUpTheChainIsKeptAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IRunnable
            {
                void Run();
            }

            public class Base : IRunnable
            {
                public void Run()
                {
                }
            }

            public class Middle : Base
            {
                public new void Run()
                {
                }
            }

            public class Between : Middle
            {
            }

            public sealed class Derived : Between, IRunnable
            {
            }
            """);

    /// <summary>Verifies an entry that moves the interface off a base class's explicit implementation is kept.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Without the entry the interface reaches the base class's explicit implementation; with it, the public
    /// member that the explicit implementation left alone.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceReimplementedOffAnExplicitBaseImplementationIsKeptAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IRunnable
            {
                void Run();
            }

            public class Base : IRunnable
            {
                void IRunnable.Run()
                {
                }
            }

            public class Middle : Base
            {
                public void Run()
                {
                }
            }

            public sealed class Derived : Middle, IRunnable
            {
            }
            """);

    /// <summary>Verifies an override in the base chain leaves the entry redundant.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Both mappings run the same code: the base class's mapping dispatches virtually to the override, so
    /// the entry changes nothing.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverrideInTheBaseChainStillReportsAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IRunnable
            {
                void Run();
            }

            public class Base : IRunnable
            {
                public virtual void Run()
                {
                }
            }

            public class Middle : Base
            {
                public override void Run()
                {
                }
            }

            public sealed class Derived : Middle, {|SST1490:IRunnable|}
            {
            }
            """);

    /// <summary>Verifies an entry whose inherited interfaces also map to the base class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceImpliedByBaseClassWithNothingReimplementedIsRemovedAsync()
    {
        const string Source = """
                              public interface IBase
                              {
                                  void Run();
                              }

                              public interface IDerived : IBase
                              {
                              }

                              public class Base : IDerived
                              {
                                  public void Run()
                                  {
                                  }
                              }

                              public sealed class Derived : Base, {|SST1490:IDerived|}
                              {
                              }
                              """;
        const string FixedSource = """
                                   public interface IBase
                                   {
                                       void Run();
                                   }

                                   public interface IDerived : IBase
                                   {
                                   }

                                   public class Base : IDerived
                                   {
                                       public void Run()
                                       {
                                       }
                                   }

                                   public sealed class Derived : Base
                                   {
                                   }
                                   """;
        await VerifyBaseList.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an entry is kept when what it re-implements is an interface it inherits.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Re-listing <c>IDerived</c> re-maps <c>IBase</c> with it, so <c>((IBase)derived).Run()</c> reaches
    /// <c>Derived.Run</c> and would reach <c>Base.Run</c> once the entry is gone.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InterfaceReimplementedThroughAnInheritedInterfaceIsKeptAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface IBase
            {
                void Run();
            }

            public interface IDerived : IBase
            {
            }

            public class Base : IDerived
            {
                public void Run()
                {
                }
            }

            public sealed class Derived : Base, IDerived
            {
                public new void Run()
                {
                }
            }
            """);

    /// <summary>Verifies an overridden property leaves the entry redundant, as an overridden method does.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverrideOfTheBasePropertyStillReportsAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface ICounter
            {
                int Count { get; }
            }

            public class Base : ICounter
            {
                public virtual int Count => 1;
            }

            public sealed class Derived : Base, {|SST1490:ICounter|}
            {
                public override int Count => 2;
            }
            """);

    /// <summary>Verifies an overridden event leaves the entry redundant, as an overridden method does.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task OverrideOfTheBaseEventStillReportsAsync() =>
        VerifyBaseList.VerifyAnalyzerAsync(
            """
            public interface INotifier
            {
                event System.EventHandler Fired;
            }

            public class Base : INotifier
            {
                public virtual event System.EventHandler Fired
                {
                    add
                    {
                    }

                    remove
                    {
                    }
                }
            }

            public sealed class Derived : Base, {|SST1490:INotifier|}
            {
                public override event System.EventHandler Fired
                {
                    add
                    {
                    }

                    remove
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies an explicit implementation of an interface implied by another interface is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The type implements the interface directly either way, so the explicit implementation keeps compiling
    /// and keeps its meaning once the entry is gone.
    /// </remarks>
    [Test]
    public async Task ExplicitImplementationUnderAnImplyingInterfaceIsReportedAsync()
    {
        const string Source = """
                              public interface IBase
                              {
                                  void Run();
                              }

                              public interface IDerived : IBase
                              {
                              }

                              public sealed class C : IDerived, {|SST1490:IBase|}
                              {
                                  void IBase.Run()
                                  {
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public interface IBase
                                   {
                                       void Run();
                                   }

                                   public interface IDerived : IBase
                                   {
                                   }

                                   public sealed class C : IDerived
                                   {
                                       void IBase.Run()
                                       {
                                       }
                                   }
                                   """;
        await VerifyBaseList.VerifyCodeFixAsync(Source, FixedSource);
    }
}
