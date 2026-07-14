// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyInferableTypeParameter = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2307InferableTypeParameterAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2307 (generic method type parameters should be inferable from the parameters).</summary>
public class InferableTypeParameterAnalyzerUnitTest
{
    /// <summary>Verifies a type parameter used by no parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterInNoParameterIsReportedAsync()
        => await VerifyInferableTypeParameter.VerifyAnalyzerAsync(
            """
            public static class Registry
            {
                public static void Register<{|SST2307:TService|}>()
                {
                }
            }
            """);

    /// <summary>Verifies a type parameter that only appears in the return type is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>C# does not infer a method's type arguments from its return type, so the caller still names it.</remarks>
    [Test]
    public async Task TypeParameterOnlyInReturnTypeIsReportedAsync()
        => await VerifyInferableTypeParameter.VerifyAnalyzerAsync(
            """
            public static class Factory
            {
                public static T Create<{|SST2307:T|}>() => default!;
            }
            """);

    /// <summary>Verifies a type parameter reachable only through another's constraint is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Inference does not walk constraints, so naming TItem there does not let a caller omit it.</remarks>
    [Test]
    public async Task TypeParameterOnlyInConstraintIsReportedAsync()
        => await VerifyInferableTypeParameter.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public static class Copier
            {
                public static void CopyInto<TSource, {|SST2307:TItem|}>(TSource source)
                    where TSource : IEnumerable<TItem>
                {
                }
            }
            """);

    /// <summary>Verifies a type parameter a parameter pins down is not reported, however deeply it is nested.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeParameterNestedInAParameterIsNotReportedAsync()
        => await VerifyInferableTypeParameter.VerifyAnalyzerAsync(
            """
            using System;
            using System.Collections.Generic;

            public static class Inferable
            {
                public static void Direct<T>(T value)
                {
                }

                public static void Array<T>(T[] values)
                {
                }

                public static void Constructed<T>(List<T> values)
                {
                }

                public static void Delegate<T>(Func<int, T> factory)
                {
                }

                public static void Tuple<T>((T Item, string Name) pair)
                {
                }

                public static void ByRef<T>(ref T value)
                {
                }

                public static void Receiver<T>(this T value, int count)
                {
                }
            }
            """);

    /// <summary>Verifies a method whose shape belongs to a base or an interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The override and both interface implementations inherit their signature. Reporting them would demand
    /// a change that cannot be made without breaking the contract; the declarations that set the shape carry
    /// the diagnostic instead.
    /// </remarks>
    [Test]
    public async Task MethodThatCannotChangeItsSignatureIsNotReportedAsync()
        => await VerifyInferableTypeParameter.VerifyAnalyzerAsync(
            """
            public interface IRegistry
            {
                void Register<{|SST2307:TService|}>();
            }

            public abstract class RegistryBase
            {
                public abstract void Register<{|SST2307:TService|}>();
            }

            public sealed class Registry : RegistryBase, IRegistry
            {
                public override void Register<TService>()
                {
                }
            }

            public sealed class ExplicitRegistry : IRegistry
            {
                void IRegistry.Register<TService>()
                {
                }
            }
            """);

    /// <summary>Verifies a method that is not externally visible is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>Its callers all live in the assembly that can change the signature freely.</remarks>
    [Test]
    public async Task MethodThatIsNotExternallyVisibleIsNotReportedAsync()
        => await VerifyInferableTypeParameter.VerifyAnalyzerAsync(
            """
            internal static class InternalRegistry
            {
                public static void Register<TService>()
                {
                }
            }

            public static class PublicRegistry
            {
                private static void Register<TService>()
                {
                }

                public static void Run()
                {
                    static void Local<TService>()
                    {
                    }

                    Local<int>();
                    Register<int>();
                }
            }
            """);

    /// <summary>Verifies a type's own type parameter is not reported against its methods.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>A caller names the type's argument once, when they construct it.</remarks>
    [Test]
    public async Task TypeParameterOfTheContainingTypeIsNotReportedAsync()
        => await VerifyInferableTypeParameter.VerifyAnalyzerAsync(
            """
            public sealed class Cache<T>
            {
                private T _value = default!;

                public void Set(T value)
                {
                    _value = value;
                }

                public T Get() => _value;
            }
            """);
}
