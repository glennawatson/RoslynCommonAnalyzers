// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUncheckedAsDereference = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2454UncheckedAsDereferenceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2454UncheckedAsDereferenceAnalyzer"/> (SST2454).</summary>
public class UncheckedAsDereferenceAnalyzerUnitTest
{
    /// <summary>Verifies a member read through an 'as' result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessOnAsResultIsFlaggedAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(object value) => ({|SST2454:value as string|}).Length;
            }
            """);

    /// <summary>Verifies a call through an 'as' result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallOnAsResultIsFlaggedAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public string M(object value) => ({|SST2454:value as string|}).Trim();
            }
            """);

    /// <summary>Verifies an index through an 'as' result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IndexOnAsResultIsFlaggedAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public char M(object value) => ({|SST2454:value as string|})[0];
            }
            """);

    /// <summary>Verifies extra parentheses around the conversion do not hide the dereference.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DoublyParenthesizedAsResultIsFlaggedAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(object value) => (({|SST2454:value as string|})).Length;
            }
            """);

    /// <summary>Verifies unwrapping a nullable value type from an 'as' conversion is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableValueUnwrapOnAsResultIsFlaggedAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(object value) => ({|SST2454:value as int?|}).Value;
            }
            """);

    /// <summary>Verifies a conditional access is the null check the rule asks for and is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAccessOnAsResultIsCleanAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int? M(object value) => (value as string)?.Length;
            }
            """);

    /// <summary>Verifies an 'as' result that is only tested for null is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullTestedAsResultIsCleanAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(object value) => (value as string) != null;
            }
            """);

    /// <summary>Verifies an 'as' result stored in a local is left alone; the flow question is not this rule's.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsResultStoredInLocalIsCleanAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(object value)
                {
                    var text = value as string;
                    return text == null ? 0 : text.Length;
                }
            }
            """);

    /// <summary>Verifies a cast, which throws with the right exception, is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastDereferenceIsCleanAsync()
        => await VerifyUncheckedAsDereference.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public int M(object value) => ((string)value).Length;
            }
            """);
}
