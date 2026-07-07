// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUnusedParameter = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1461UnusedParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst1461UnusedParameterAnalyzer"/>.</summary>
public class UnusedParameterAnalyzerUnitTest
{
    /// <summary>Verifies an unused private method parameter is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PrivateMethodParameterIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int M(int {|SST1461:value|}) => 1;
            }
            """);

    /// <summary>Verifies public method parameters are not reported because they are API surface.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicMethodParameterIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M(int value) => 1;
            }
            """);

    /// <summary>Verifies an (object, EventArgs) event handler is exempt even when both parameters are unread.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EventHandlerSignatureIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private void OnEvent(object sender, EventArgs e)
                {
                }
            }
            """);

    /// <summary>Verifies a PropertyChanged handler with a nullable sender and a derived EventArgs is exempt.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PropertyChangedHandlerIsCleanAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            #nullable enable
            using System.ComponentModel;

            public sealed class C
            {
                private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
                    => System.Console.WriteLine(e.PropertyName);
            }
            """);

    /// <summary>Verifies the exemption is narrow: a two-parameter method whose second parameter is not EventArgs still reports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ObjectFirstParameterWithNonEventArgsSecondIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private void M(object {|SST1461:sender|}, string name)
                    => System.Console.WriteLine(name);
            }
            """);

    /// <summary>Verifies the exemption is narrow: an EventArgs second parameter with a non-object first parameter still reports.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonObjectFirstParameterIsReportedAsync()
        => await VerifyUnusedParameter.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private void M(int {|SST1461:code|}, EventArgs e)
                    => System.Console.WriteLine(e);
            }
            """);
}
