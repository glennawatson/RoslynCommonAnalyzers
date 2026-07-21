// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInferableTypeArguments = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2251InferableTypeArgumentsAnalyzer,
    StyleSharp.Analyzers.Sst2251InferableTypeArgumentsCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2251 (omit type arguments that inference supplies).</summary>
public class InferableTypeArgumentsAnalyzerUnitTest
{
    /// <summary>Verifies explicit type arguments an unqualified call infers are reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantTypeArgumentsAreRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public T Echo<T>(T value) => value;

                                  public int Use() => Echo{|SST2251:<int>|}(42);
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public T Echo<T>(T value) => value;

                                       public int Use() => Echo(42);
                                   }
                                   """;
        await VerifyInferableTypeArguments.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies redundant type arguments on a qualified call keep the receiver and drop only the list.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantTypeArgumentsOnAMemberAccessAreRemovedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public T Echo<T>(T value) => value;

                                  public int Use() => this.Echo{|SST2251:<int>|}(42);
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public T Echo<T>(T value) => value;

                                       public int Use() => this.Echo(42);
                                   }
                                   """;
        await VerifyInferableTypeArguments.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the rule stays silent whenever dropping the type arguments would change what the call means.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CallsWhoseMeaningWouldChangeAreCleanAsync()
        => await VerifyInferableTypeArguments.VerifyAnalyzerAsync(
            """
            public static class Box<T>
            {
                public static T Make(T value) => value;
            }

            public sealed class C
            {
                // A non-generic overload wins once the type arguments are gone, so the call moves.
                public string Pick<T>(T value) => "generic";

                public string Pick(int value) => "specific";

                // The type parameter appears in no parameter, so inference cannot supply it.
                public void Log<T>(object value)
                {
                }

                // A parameterless generic method has nothing to infer the type argument from.
                public T Default<T>() => default;

                // Inference would pick string here; the call deliberately widens to object.
                public T Identity<T>(T value) => value;

                public T Echo<T>(T value) => value;

                public string UsePick() => Pick<int>(42);

                public void UseLog() => Log<int>("text");

                public int UseDefault() => Default<int>();

                public object UseIdentity() => Identity<object>("text");

                // The type arguments belong to the receiver type, not the invoked method.
                public int UseBox() => Box<int>.Make(42);

                // A conditional-access call cannot be re-bound without its receiver, so it is left alone.
                public int? UseConditional(C other) => other?.Echo<int>(42);
            }
            """);

    /// <summary>Verifies a type argument that only differs from inference in nullability is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullabilityChangingTypeArgumentIsCleanAsync()
        => await VerifyInferableTypeArguments.VerifyAnalyzerAsync(
            """
            #nullable enable

            public sealed class C
            {
                public T Id<T>(T value) => value;

                // Dropping <object?> would infer <object> from the non-null argument, changing the result type.
                public object? Use(object value) => Id<object?>(value);
            }
            """);

    /// <summary>Verifies a redundant reference-type argument whose nullability matches inference is still removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RedundantReferenceTypeArgumentWithMatchingNullabilityIsRemovedAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class C
                              {
                                  public T Id<T>(T value) => value;

                                  public object? Use(object? value) => Id{|SST2251:<object?>|}(value);
                              }
                              """;
        const string FixedSource = """
                                   #nullable enable

                                   public sealed class C
                                   {
                                       public T Id<T>(T value) => value;

                                       public object? Use(object? value) => Id(value);
                                   }
                                   """;
        await VerifyInferableTypeArguments.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>
    /// Verifies a generic call reached through a conditional-access chain does not crash the analyzer and is
    /// left alone. Regression: dropping the type arguments and speculatively binding the detached call made
    /// Roslyn look for an enclosing conditional access that no longer existed and throw a NullReferenceException.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericCallThroughConditionalAccessChainIsNotReportedAsync()
    {
        const string Source = """
                              #nullable enable

                              public sealed class Inner
                              {
                                  public int Do<T>(int value) => value;
                              }

                              public sealed class Box
                              {
                                  public Inner Prop = new();

                                  public Inner this[int index] => Prop;
                              }

                              public sealed class C
                              {
                                  private Box? _box;

                                  public void M()
                                  {
                                      var a = _box?.Prop.Do<C>(1);
                                      var b = _box?[0].Do<C>(1);
                                      var c = _box?.Prop!.Do<C>(1);
                                  }
                              }
                              """;
        await VerifyInferableTypeArguments.VerifyAnalyzerAsync(Source);
    }
}
