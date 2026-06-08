// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using VerifyBasePrefix = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.DoNotPrefixWithBaseAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the redundant-base-prefix rule (SST1100).</summary>
public class DoNotPrefixWithBaseAnalyzerUnitTest
{
    /// <summary>Verifies a base call to a non-overridden member is reported (SST1100).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantBasePrefixReportedAsync()
        => await VerifyBasePrefix.VerifyAnalyzerAsync(
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
    [Test]
    public async Task BaseCallToOverriddenMemberIsCleanAsync()
        => await VerifyBasePrefix.VerifyAnalyzerAsync(
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

    /// <summary>Verifies delegate arguments followed by a parameterless lambda are not misclassified as SST1100.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubscribeLambdaSequenceIsCleanAsync()
        => await VerifyBasePrefix.VerifyAnalyzerAsync(
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

        await Assert.That(DoNotPrefixWithBaseAnalyzer.HasOverrideNamed(type, "Run")).IsTrue();
    }

    /// <summary>Verifies the syntax fast path ignores non-override members with the requested name.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SyntaxFastPathRejectsNonOverrideByNameAsync()
    {
        var type = ParseType(
            "internal class Base { public void Help() { } } internal class Derived : Base { public void Help() { } }");

        await Assert.That(DoNotPrefixWithBaseAnalyzer.HasOverrideNamed(type, "Help")).IsFalse();
    }

    /// <summary>Parses the last type declaration from the supplied source.</summary>
    /// <param name="source">The source containing the type declarations.</param>
    /// <returns>The parsed containing type.</returns>
    private static TypeDeclarationSyntax ParseType(string source)
        => (TypeDeclarationSyntax)SyntaxFactory.ParseCompilationUnit(source).Members[^1];
}
