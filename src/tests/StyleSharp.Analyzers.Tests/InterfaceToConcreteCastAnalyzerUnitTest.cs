// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2326InterfaceToConcreteCastAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2326 (narrowing an interface reference to a concrete implementation type).</summary>
public class InterfaceToConcreteCastAnalyzerUnitTest
{
    /// <summary>Verifies an explicit cast from an interface to a concrete class is reported on the target type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplicitCastFromInterfaceToConcreteIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            sealed class Circle : IShape
            {
            }

            class Consumer
            {
                void Use(IShape shape)
                {
                    var circle = ({|SST2326:Circle|})shape;
                }
            }
            """);

    /// <summary>Verifies an <c>as</c> conversion from an interface to a concrete class is reported on the target type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsExpressionFromInterfaceToConcreteIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            sealed class Circle : IShape
            {
            }

            class Consumer
            {
                void Use(IShape shape)
                {
                    var circle = shape as {|SST2326:Circle|};
                }
            }
            """);

    /// <summary>Verifies an <c>is</c> type test from an interface to a concrete class is reported on the target type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsTypeTestFromInterfaceToConcreteIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            sealed class Circle : IShape
            {
            }

            class Consumer
            {
                bool Use(IShape shape) => shape is {|SST2326:Circle|};
            }
            """);

    /// <summary>Verifies an <c>is</c> declaration pattern from an interface to a concrete class is reported on the target type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsDeclarationPatternFromInterfaceToConcreteIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            sealed class Circle : IShape
            {
            }

            class Consumer
            {
                void Use(IShape shape)
                {
                    if (shape is {|SST2326:Circle|} circle)
                    {
                        _ = circle;
                    }
                }
            }
            """);

    /// <summary>Verifies a cast from a constructed generic interface to its concrete implementation is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericInterfaceToConcreteListIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            class Consumer
            {
                void Use(IEnumerable<int> items)
                {
                    var list = ({|SST2326:List<int>|})items;
                }
            }
            """);

    /// <summary>Verifies a cast from one interface to another interface is not reported: the target is not a concrete class.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceToInterfaceCastIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            class Consumer
            {
                void Use(IEnumerable<int> items)
                {
                    var list = (IList<int>)items;
                }
            }
            """);

    /// <summary>Verifies a cast to an abstract class that implements the interface is not reported: an abstract base is not a concrete implementation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastToAbstractImplementingClassIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            abstract class ShapeBase : IShape
            {
            }

            class Consumer
            {
                void Use(IShape shape)
                {
                    var baseShape = (ShapeBase)shape;
                }
            }
            """);

    /// <summary>Verifies a cast to a struct that implements the interface is not reported: the target must be a class.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastToStructImplementingInterfaceIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            struct Point : IShape
            {
            }

            class Consumer
            {
                void Use(IShape shape)
                {
                    var point = (Point)shape;
                }
            }
            """);

    /// <summary>Verifies a cast to an unrelated non-sealed class that does not implement the interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastToUnrelatedNonSealedClassIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            class Unrelated
            {
            }

            class Consumer
            {
                void Use(IShape shape)
                {
                    var unrelated = (Unrelated)shape;
                }
            }
            """);

    /// <summary>Verifies a downcast from <c>object</c> to a concrete class is not reported: the operand is not an interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastFromObjectIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            sealed class Circle
            {
            }

            class Consumer
            {
                void Use(object value)
                {
                    var circle = (Circle)value;
                }
            }
            """);

    /// <summary>Verifies an <c>is</c> test where the operand is a type parameter constrained to an interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsTypeTestFromTypeParameterIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            sealed class Circle : IShape
            {
            }

            class Consumer<T>
                where T : IShape
            {
                bool Use(T shape) => shape is Circle;
            }
            """);

    /// <summary>Verifies an <c>is not</c> pattern, which is not a declaration pattern, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IsNotPatternIsNotReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            interface IShape
            {
            }

            sealed class Circle : IShape
            {
            }

            class Consumer
            {
                bool Use(IShape shape) => shape is not Circle;
            }
            """);
}
