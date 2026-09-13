// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using VerifyBasePrefix = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1100DoNotPrefixWithBaseAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the redundant-base-prefix rule (SST1100).</summary>
public class DoNotPrefixWithBaseAnalyzerUnitTest
{
    /// <summary>Verifies unresolved base members do not report when a local member has the same name.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnresolvedHiddenBaseMemberIsSilentAsync()
    {
        const string Source = "class C { void Missing(int value) { } void M() { base.Missing(); } }";
        var test = new VerifyBasePrefix.Test { TestCode = Source, CompilerDiagnostics = CompilerDiagnostics.None };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies field and event declarations participate in syntactic hiding checks.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="expected">Whether the declaration introduces Value.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("int Other, Value;", true)]
    [Arguments("int Other;", false)]
    [Arguments("event System.Action Other, Value;", true)]
    [Arguments("event System.Action Other;", false)]
    [Arguments("event System.Action Value { add { } remove { } }", true)]
    [Arguments("event System.Action Other { add { } remove { } }", false)]
    [Arguments("class Value { }", false)]
    public async Task HidingCheckRecognizesFieldAndEventNamesAsync(string member, bool expected)
    {
        var declaration = (TypeDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration($"class C {{ {member} }}")!;
        await Assert.That(Sst1100DoNotPrefixWithBaseAnalyzer.HasOwnMemberNamed(declaration, "Value")).IsEqualTo(expected);
    }

    /// <summary>Verifies an override of a different overload does not make a base prefix necessary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DifferentOverloadOverrideLeavesBasePrefixRedundantAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync("class B { public virtual void M() { } public virtual void M(int x) { } } class C : B { public override void M() { {|SST1100:base|}.M(1); } }");

    /// <summary>Verifies property and event overrides retain meaningful base accesses.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task PropertyAndEventOverridesRetainBaseAccessAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            class B
            {
                public virtual int Value => 1;
                public virtual event System.Action Changed { add { } remove { } }
            }
            class C : B
            {
                public override int Value => base.Value;
                public override event System.Action Changed
                {
                    add { base.Changed += value; }
                    remove { base.Changed -= value; }
                }
            }
            """);

    /// <summary>Verifies verbatim member names are compared using their decoded identifier.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task VerbatimBaseNameIsReportedAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync("class B { public int @event; } class C : B { int M() => {|SST1100:base|}.@event; }");

    /// <summary>Verifies a base call to a non-overridden member is reported (SST1100).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RedundantBasePrefixReportedAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                public virtual void Run()
                {
                }

                public void Help()
                {
                }
            }

            internal class Derived : Base
            {
                public override void Run()
                {
                }

                public void Call() => {|SST1100:base|}.Help();
            }
            """);

    /// <summary>Verifies a base call to an overridden member is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseCallToOverriddenMemberIsCleanAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                public virtual void Run()
                {
                }
            }

            internal class Derived : Base
            {
                public override void Run() => base.Run();
            }
            """);

    /// <summary>Verifies a base access to a member hidden by a <c>new</c> declaration is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseCallToHiddenMemberIsCleanAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                public int Value { get; set; }
            }

            internal class Derived : Base
            {
                public new int Value
                {
                    get => base.Value;
                    set => base.Value = value;
                }
            }
            """);

    /// <summary>Verifies a generic type hiding a base property with <c>new</c> is not flagged (the StateSignal case).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseCallToHiddenMemberInGenericTypeIsCleanAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            internal class Behavior<T>
            {
                public Behavior(T value) => Value = value;

                public T Value { get; protected set; }
            }

            internal class State<T> : Behavior<T>
            {
                public State(T value)
                    : base(value)
                {
                }

                public new T Value
                {
                    get => base.Value;
                    set => base.Value = value;
                }
            }
            """);

    /// <summary>Verifies a base call to a method hidden by a <c>new</c> declaration is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BaseCallToHiddenMethodIsCleanAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            internal class Base
            {
                public int Compute() => 0;
            }

            internal class Derived : Base
            {
                public new int Compute() => base.Compute() + 1;
            }
            """);

    /// <summary>Verifies delegate arguments followed by a parameterless lambda are not misclassified as SST1100.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SubscribeLambdaSequenceIsCleanAsync() =>
        VerifyBasePrefix.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            internal static class ObservableExtensions
            {
                private static IDisposable Subscribe<T>(
                    this IObservable<T> source,
                    Action<T> onNext,
                    Action<Exception> onError,
                    Action onCompleted)
                    => throw new NotSupportedException();

                private static Task<T> FirstOrDefaultCoreAsync<T>(this IObservable<T> source, bool hasDefault, T defaultValue)
                {
                    var completion = new TaskCompletionSource<T>();
                    var seen = false;
                    source.Subscribe(
                        value =>
                        {
                            if (seen)
                            {
                                return;
                            }

                            seen = true;
                            completion.TrySetResult(value);
                        },
                        error => completion.TrySetException(error),
                        () =>
                        {
                            if (seen)
                            {
                                return;
                            }

                            if (hasDefault)
                            {
                                completion.TrySetResult(defaultValue);
                            }
                            else
                            {
                                completion.TrySetException(new InvalidOperationException("The source completed without producing a value."));
                            }
                        });
                    return completion.Task;
                }
            }
            """);

    /// <summary>Verifies the syntax fast path recognizes an override with the requested member name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxFastPathRecognizesOverrideByNameAsync()
    {
        var type = ParseType(
            "internal class Base { public virtual void Run() { } } internal class Derived : Base { public override void Run() { } }");

        await Assert.That(Sst1100DoNotPrefixWithBaseAnalyzer.HasOwnMemberNamed(type, "Run")).IsTrue();
    }

    /// <summary>Verifies the syntax fast path recognizes a hiding member with the requested member name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxFastPathRecognizesHidingMemberByNameAsync()
    {
        var type = ParseType(
            "internal class Base { public int Value { get; set; } } internal class Derived : Base { public new int Value { get; set; } }");

        await Assert.That(Sst1100DoNotPrefixWithBaseAnalyzer.HasOwnMemberNamed(type, "Value")).IsTrue();
    }

    /// <summary>Verifies the syntax fast path rejects names the type does not declare.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxFastPathRejectsAbsentMemberByNameAsync()
    {
        var type = ParseType(
            "internal class Base { public void Help() { } } internal class Derived : Base { public void Call() { } }");

        await Assert.That(Sst1100DoNotPrefixWithBaseAnalyzer.HasOwnMemberNamed(type, "Help")).IsFalse();
    }

    /// <summary>Parses the last type declaration from the supplied source.</summary>
    /// <param name="source">The source containing the type declarations.</param>
    /// <returns>The parsed containing type.</returns>
    private static TypeDeclarationSyntax ParseType(string source) =>
        (TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[^1];
}
