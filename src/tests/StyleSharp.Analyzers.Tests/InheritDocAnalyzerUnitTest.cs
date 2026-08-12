// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyInheritDoc = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1648InheritDocAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the inheritdoc validity rule (SST1648).</summary>
public class InheritDocAnalyzerUnitTest
{
    /// <summary>Verifies inheritdoc on an element with no base is reported (SST1648).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocWithoutBaseReportedAsync()
        => await VerifyInheritDoc.VerifyAnalyzerAsync(
            """
            internal class C
            {
                /// <inheritdoc/>
                public void {|SST1648:M|}()
                {
                }
            }
            """);

    /// <summary>Verifies inheritdoc on an interface implementation is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocOnImplementationIsCleanAsync()
        => await VerifyInheritDoc.VerifyAnalyzerAsync(
            """
            internal interface I
            {
                void M();
            }

            internal class C : I
            {
                /// <inheritdoc/>
                public void M()
                {
                }
            }
            """);

    /// <summary>Verifies inheritdoc on an explicit interface implementation is not flagged.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocOnExplicitImplementationIsCleanAsync()
        => await VerifyInheritDoc.VerifyAnalyzerAsync(
            """
            internal interface IViewFor
            {
                object ViewModel { get; set; }
            }

            internal interface IViewFor<T> : IViewFor
                where T : class
            {
                new T ViewModel { get; set; }
            }

            internal abstract class C<T> : IViewFor<T>
                where T : class
            {
                public T ViewModel { get; set; }

                /// <inheritdoc/>
                object IViewFor.ViewModel
                {
                    get => ViewModel;
                    set => ViewModel = (T)value;
                }
            }
            """);

    /// <summary>Verifies inheritdoc naming its source with a cref is not flagged, whatever the member derives from.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocWithCrefIsCleanAsync()
        => await VerifyInheritDoc.VerifyAnalyzerAsync(
            """
            internal interface ISource
            {
                /// <summary>Gets a value.</summary>
                int Value { get; }
            }

            internal readonly struct S
            {
                /// <inheritdoc cref="ISource.Value"/>
                public static int Value { get; }
            }
            """);

    /// <summary>Verifies a cref on the closing form of the element is recognised too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocWithCrefOnAPairedElementIsCleanAsync()
        => await VerifyInheritDoc.VerifyAnalyzerAsync(
            """
            internal interface ISource
            {
                /// <summary>Gets a value.</summary>
                int Value { get; }
            }

            internal readonly struct S
            {
                /// <inheritdoc cref="ISource.Value"></inheritdoc>
                public static int Value { get; }
            }
            """);

    /// <summary>
    /// Verifies an explicit interface implementation is not flagged even when the interface type
    /// is unresolved (incomplete semantics, e.g. an interface in a momentarily unreferenced assembly).
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritDocOnExplicitImplWithUnresolvedInterfaceIsCleanAsync()
    {
        var test = new VerifyInheritDoc.Test
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
        };

        test.TestState.Sources.Add(("App.cs", """
            namespace App
            {
                public abstract class C<T> : IViewFor<T>
                    where T : class
                {
                    /// <inheritdoc/>
                    object IViewFor.ViewModel
                    {
                        get => null;
                        set { }
                    }
                }
            }
            """));

        await test.RunAsync();
    }
}
