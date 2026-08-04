// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNameofTypeParameter = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2498NameofTypeParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2498NameofTypeParameterAnalyzer"/> (SST2498).</summary>
public class NameofTypeParameterAnalyzerUnitTest
{
    /// <summary>Verifies a type parameter used to name a file is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameofOnTypeParameterIsReportedAsync()
    {
        const string Source = """
                              public sealed class SettingsStore<T>
                              {
                                  public string DatabaseName(string overrideName) => overrideName ?? {|SST2498:nameof(T)|};
                              }
                              """;
        await VerifyNameofTypeParameter.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a method's own type parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameofOnMethodTypeParameterIsReportedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string Describe<TValue>() => "of type " + {|SST2498:nameof(TValue)|};
                              }
                              """;
        await VerifyNameofTypeParameter.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a type parameter used as a constant is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameofOnTypeParameterInConstantIsReportedAsync()
    {
        const string Source = """
                              public sealed class KeyMetadata<T>
                              {
                                  internal const string Name = {|SST2498:nameof(T)|};
                              }
                              """;
        await VerifyNameofTypeParameter.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies <c>nameof</c> on anything that is not a type parameter is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NameofOnOtherSymbolsIsCleanAsync()
    {
        const string Source = """
                              public sealed class Widget
                              {
                                  public int Count { get; }
                              }

                              public sealed class C<T>
                              {
                                  public string TypeName() => typeof(T).Name;

                                  public string MemberName() => nameof(Widget.Count);

                                  public string ClassName() => nameof(Widget);

                                  public string ParameterName(int value) => nameof(value);
                              }
                              """;
        await VerifyNameofTypeParameter.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies a method someone declared as <c>nameof</c> is an ordinary call, not the operator.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DeclaredNameofMethodIsCleanAsync()
    {
        const string Source = """
                              public sealed class C<T>
                              {
                                  public string Describe() => nameof(default(T));

                                  private static string nameof(T value) => "";
                              }
                              """;
        await VerifyNameofTypeParameter.VerifyAnalyzerAsync(Source);
    }
}
