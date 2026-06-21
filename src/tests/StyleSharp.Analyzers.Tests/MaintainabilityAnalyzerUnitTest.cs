// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyAccess = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1400AccessModifierAnalyzer,
    StyleSharp.Analyzers.Sst1400AccessModifierCodeFixProvider>;
using VerifyDebug = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DebugMessageAnalyzer>;
using VerifyField = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1401FieldVisibilityAnalyzer>;
using VerifyFile = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.FileTypeNamespaceAnalyzer>;
using VerifyParens = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantParenthesesAnalyzer,
    StyleSharp.Analyzers.RedundantParenthesesCodeFixProvider>;
using VerifyPrecedence = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.PrecedenceAnalyzer,
    StyleSharp.Analyzers.PrecedenceCodeFixProvider>;
using VerifyPrivateUsage = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1440PrivateMemberUsageAnalyzer,
    StyleSharp.Analyzers.Sst1440PrivateMemberUsageCodeFixProvider>;
using VerifyPrivateUsageAnalyzer = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1440PrivateMemberUsageAnalyzer>;
using VerifySuppress = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1404SuppressionJustificationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the maintainability rules (SST1400–SST1411).</summary>
public class MaintainabilityAnalyzerUnitTest
{
    /// <summary>Verifies a top-level type with no access modifier is reported (SST1400) and gets 'internal'.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TopLevelTypeGetsInternalAsync()
        => await VerifyAccess.VerifyCodeFixAsync("class {|SST1400:C|} { }", "internal class C { }");

    /// <summary>Verifies a member with no access modifier is reported (SST1400) and gets 'private'.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberGetsPrivateAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  void {|SST1400:M|}() { }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private void M() { }
                                   }
                                   """;
        await VerifyAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an interface member is not required to declare accessibility.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceMemberAllowedAsync()
        => await VerifyAccess.VerifyAnalyzerAsync(
            """
            internal interface I
            {
                void M();
            }
            """);

    /// <summary>Verifies an exposed field is reported (SST1401) while constants, static readonly singletons, and private fields are not.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExposedFieldReportedAsync()
        => await VerifyField.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int {|SST1401:Exposed|};
                public const int Allowed = 1;
                public static readonly C Instance = new();
                private int _hidden;
            }
            """);

    /// <summary>Verifies a second top-level type is reported (SST1402).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecondTypeReportedAsync()
        => await VerifyFile.VerifyAnalyzerAsync(
            """
            internal class A { }
            internal class {|SST1402:B|} { }
            """);

    /// <summary>Verifies a partial type split across declarations is counted once.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PartialTypeCountedOnceAsync()
        => await VerifyFile.VerifyAnalyzerAsync(
            """
            internal partial class A { }
            internal partial class A { }
            """);

    /// <summary>Verifies a second namespace is reported (SST1403).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecondNamespaceReportedAsync()
        => await VerifyFile.VerifyAnalyzerAsync(
            """
            namespace A { }
            namespace {|SST1403:B|} { }
            """);

    /// <summary>Verifies a suppression without justification is reported (SST1404).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SuppressionWithoutJustificationReportedAsync()
        => await VerifySuppress.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.CodeAnalysis;
            internal class C
            {
                [{|SST1404:SuppressMessage("Cat", "Rule")|}]
                public void M() { }
            }
            """);

    /// <summary>Verifies a justified suppression is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JustifiedSuppressionAllowedAsync()
        => await VerifySuppress.VerifyAnalyzerAsync(
            """
            using System.Diagnostics.CodeAnalysis;
            internal class C
            {
                [SuppressMessage("Cat", "Rule", Justification = "Tested.")]
                public void M() { }
            }
            """);

    /// <summary>Verifies a Debug.Assert without a message is reported (SST1405).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssertWithoutMessageReportedAsync()
        => await VerifyDebug.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;
            internal class C
            {
                public void M() => Debug.{|SST1405:Assert|}(true);
            }
            """);

    /// <summary>Verifies a Debug.Fail with an empty message is reported (SST1406).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FailWithoutMessageReportedAsync()
        => await VerifyDebug.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;
            internal class C
            {
                public void M() => Debug.{|SST1406:Fail|}("");
            }
            """);

    /// <summary>Verifies mixed arithmetic precedence is reported (SST1407) and parenthesized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArithmeticPrecedenceParenthesizedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public int M(int a, int b, int c) => a + {|SST1407:b * c|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public int M(int a, int b, int c) => a + (b * c);
                                   }
                                   """;
        await VerifyPrecedence.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies mixed conditional precedence is reported (SST1408) and parenthesized.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalPrecedenceParenthesizedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool a, bool b, bool c) => a || {|SST1408:b && c|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool a, bool b, bool c) => a || (b && c);
                                   }
                                   """;
        await VerifyPrecedence.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty anonymous-method parameter list is reported (SST1410) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyDelegateParenthesesRemovedAsync()
    {
        const string Source = """
                              using System;
                              internal class C
                              {
                                  public Action A() => delegate{|SST1410:()|} { };
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   internal class C
                                   {
                                       public Action A() => delegate { };
                                   }
                                   """;
        await VerifyParens.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an empty attribute argument list is reported (SST1411) and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyAttributeParenthesesRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  [System.Obsolete{|SST1411:()|}]
                                  public void M() { }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       [System.Obsolete]
                                       public void M() { }
                                   }
                                   """;
        await VerifyParens.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites every top-level type missing an access modifier (SST1400) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AccessModifierFixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              class {|SST1400:A|} { }

                              class {|SST1400:B|} { }

                              class {|SST1400:C|} { }
                              """;
        const string FixedSource = """
                                   internal class A { }

                                   internal class B { }

                                   internal class C { }
                                   """;
        await VerifyAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All rewrites a type and the members nested inside it in one pass (parent-then-child edits must compose).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AccessModifierFixAllRewritesTypeAndNestedMembersAsync()
    {
        const string Source = """
                              class {|SST1400:Outer|}
                              {
                                  int {|SST1400:_x|};

                                  void {|SST1400:M|}() { }
                              }
                              """;
        const string FixedSource = """
                                   internal class Outer
                                   {
                                       private int _x;

                                       private void M() { }
                                   }
                                   """;
        await VerifyAccess.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every empty attribute argument list (SST1411) in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantParenthesesFixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  [System.Obsolete{|SST1411:()|}]
                                  public void M() { }

                                  [System.Obsolete{|SST1411:()|}]
                                  public void N() { }

                                  [System.Obsolete{|SST1411:()|}]
                                  public void O() { }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       [System.Obsolete]
                                       public void M() { }

                                       [System.Obsolete]
                                       public void N() { }

                                       [System.Obsolete]
                                       public void O() { }
                                   }
                                   """;
        await VerifyParens.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an unused private method is removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnusedPrivateMethodIsRemovedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void {|SST1440:Unused|}() { }

                                  public void M() { }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {

                                       public void M() { }
                                   }
                                   """;

        await VerifyPrivateUsage.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a private field with only dead writes is reported separately from fully unused members.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnreadPrivateFieldIsReportedAsync()
        => await VerifyPrivateUsageAnalyzer.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int {|SST1441:_value|};

                public void M() => _value = 1;
            }
            """);

    /// <summary>Verifies private members with real reads are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsedPrivateMembersAreAllowedAsync()
        => await VerifyPrivateUsageAnalyzer.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private int _value;

                public int M()
                {
                    _value = 1;
                    return _value;
                }
            }
            """);

    /// <summary>Verifies a private method used from another partial declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateMethodUsedFromAnotherPartialPartIsAllowedAsync()
    {
        var test = new VerifyPrivateUsageAnalyzer.Test();
        test.TestState.Sources.Add(
            ("C.Helpers.cs", """
                             internal static partial class C
                             {
                                 private static int Helper() => 1;
                             }
                             """));
        test.TestState.Sources.Add(
            ("C.cs", """
                     internal static partial class C
                     {
                         public static int M() => Helper();
                     }
                     """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a private field used from another partial declaration is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateFieldUsedFromAnotherPartialPartIsAllowedAsync()
    {
        var test = new VerifyPrivateUsageAnalyzer.Test();
        test.TestState.Sources.Add(
            ("C.State.cs", """
                           internal static partial class C
                           {
                               private static int _value = 1;
                           }
                           """));
        test.TestState.Sources.Add(
            ("C.cs", """
                     internal static partial class C
                     {
                         public static int M() => _value;
                     }
                     """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a private field written in one partial declaration and read in another is not reported as unread.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateFieldWrittenAndReadAcrossPartialPartsIsAllowedAsync()
    {
        var test = new VerifyPrivateUsageAnalyzer.Test();
        test.TestState.Sources.Add(
            ("C.State.cs", """
                           internal static partial class C
                           {
                               private static int _value;

                               public static void Set() => _value = 1;
                           }
                           """));
        test.TestState.Sources.Add(
            ("C.cs", """
                     internal static partial class C
                     {
                         public static int M() => _value;
                     }
                     """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies private helpers called from extension blocks are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateHelperCalledFromExtensionBlockIsAllowedAsync()
        => await VerifyPrivateUsageAnalyzer.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public static partial class SignalAsyncExtensions
            {
                extension<T>(IObservableAsync<T> @this)
                {
                    public ValueTask<Dictionary<TKey, T>> ToDictionaryAsync<TKey>(
                        Func<T, TKey> keySelector,
                        IEqualityComparer<TKey> comparer,
                        CancellationToken cancellationToken)
                        where TKey : notnull =>
                        ToDictionaryCore(@this, keySelector, DictionaryIdentity<T>.Instance, comparer, cancellationToken);
                }

                private static ValueTask<Dictionary<TKey, TValue>> ToDictionaryCore<TSource, TKey, TValue>(
                    IObservableAsync<TSource> source,
                    Func<TSource, TKey> keySelector,
                    Func<TSource, TValue> elementSelector,
                    IEqualityComparer<TKey> comparer,
                    CancellationToken cancellationToken)
                    where TKey : notnull =>
                    default;

                private static class DictionaryIdentity<T>
                {
                    internal static readonly Func<T, T> Instance = static value => value;
                }
            }

            public interface IObservableAsync<T>
            {
            }
            """);

    /// <summary>Verifies private nullable generic helpers called through type inference are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PrivateNullableGenericHelperCalledThroughTypeInferenceIsAllowedAsync()
        => await VerifyPrivateUsageAnalyzer.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;
            using System.Threading;

            #if REACTIVE_SHIM
            namespace ReactiveUI.Primitives.Reactive.Signals;
            #else
            namespace ReactiveUI.Primitives.Signals;
            #endif

            public static partial class Signal
            {
                public static IObservable<int> Sequence(int start, int count)
                    => count == 0 ? ImmutableEmptySignal<int>.Instance : new RangeSignal(start, count);

                public static IObservable<int> Sequence(int start, int count, ISequencer scheduler)
                    => scheduler == Sequencer.Immediate || scheduler == Sequencer.CurrentThread ? new RangeSignal(start, count) : new SequenceSignal(start, count, scheduler);

                public static IObservable<long> Every(TimeSpan period, ISequencer scheduler)
                    => new EverySignal(period, scheduler);

                public static IObservable<long> Pulse(TimeSpan period, ISequencer scheduler)
                    => new EverySignal(period, scheduler);

                public static IObservable<T> Chain<T>(params System.IObservable<T>[] sources)
                {
                    var validated = ValidateSources(sources);
                    var rangeConcat = TryCreateRangeConcat(validated);
                    return rangeConcat is null ? new ChainSignal<T>(validated) : (IObservable<T>)(object)rangeConcat;
                }

                public static IObservable<T> Blend<T>(params System.IObservable<T>[] sources)
                {
                    var validated = ValidateSources(sources);
                    var rangeConcat = TryCreateRangeConcat(validated);
                    return rangeConcat is null ? new EnumerableBlendSignal<T>(validated) : (IObservable<T>)(object)rangeConcat;
                }

                public static IObservable<T> Race<T>(params System.IObservable<T>[] sources)
                {
                    var validated = ValidateSources(sources);
                    return validated.Length > 0 && validated[0] is RangeSignal ? validated[0] : new RaceSignal<T>(validated);
                }

                private static System.IObservable<T>[] ValidateSources<T>(System.IObservable<T>[] sources)
                {
                    ArgumentExceptionHelper.ThrowIfNull(sources);

                    for (var i = 0; i < sources.Length; i++)
                    {
                        ArgumentExceptionHelper.ThrowIfNull(sources[i]);
                    }

                    return sources;
                }

                private static RangeConcatSignal? TryCreateRangeConcat<T>(System.IObservable<T>[] sources)
                {
                    if (typeof(T) != typeof(int) || sources.Length == 0)
                    {
                        return null;
                    }

                    var ranges = new RangeSignal[sources.Length];
                    for (var i = 0; i < sources.Length; i++)
                    {
                        if (sources[i] is not RangeSignal range)
                        {
                            return null;
                        }

                        ranges[i] = range;
                    }

                    return new(ranges);
                }

                private sealed class SequenceSignal(int start, int count, ISequencer scheduler) : IObservable<int>
                {
                    public IDisposable Subscribe(IObserver<int> observer) => throw new NotImplementedException();
                }

                private sealed class EverySignal(TimeSpan period, ISequencer scheduler) : IObservable<long>
                {
                    public IDisposable Subscribe(IObserver<long> observer) => throw new NotImplementedException();
                }

                private sealed class ChainSignal<T>(System.IObservable<T>[] sources) : IObservable<T>
                {
                    public IDisposable Subscribe(IObserver<T> observer) => throw new NotImplementedException();
                }

                private sealed class EnumerableBlendSignal<T>(System.IObservable<T>[] sources) : IObservable<T>
                {
                    public IDisposable Subscribe(IObserver<T> observer) => throw new NotImplementedException();
                }

                private sealed class RaceSignal<T>(System.IObservable<T>[] sources) : IObservable<T>
                {
                    public IDisposable Subscribe(IObserver<T> observer) => throw new NotImplementedException();
                }

                private sealed class RangeConcatSignal(RangeSignal[] ranges) : IObservable<int>
                {
                    public IDisposable Subscribe(IObserver<int> observer) => throw new NotImplementedException();
                }
            }

            public interface ISequencer
            {
            }

            public static class Sequencer
            {
                public static ISequencer Immediate { get; } = new SequencerImpl();

                public static ISequencer CurrentThread { get; } = new SequencerImpl();

                private sealed class SequencerImpl : ISequencer
                {
                }
            }

            public static class ArgumentExceptionHelper
            {
                public static void ThrowIfNull<T>(T value)
                    where T : class
                {
                    if (value is null)
                    {
                        throw new ArgumentNullException(nameof(value));
                    }
                }
            }

            public sealed class RangeSignal(int start, int count) : IObservable<int>
            {
                public IDisposable Subscribe(IObserver<int> observer) => throw new NotImplementedException();
            }

            public sealed class ImmutableEmptySignal<T> : IObservable<T>
            {
                public static ImmutableEmptySignal<T> Instance { get; } = new();

                public IDisposable Subscribe(IObserver<T> observer) => throw new NotImplementedException();
            }
            """);
}
