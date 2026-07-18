// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2467BypassedParamsOverloadAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2467 (a params overload bypassed by a more specific same-type sibling).</summary>
public class Sst2467BypassedParamsOverloadAnalyzerUnitTest
{
    /// <summary>Verifies a more specific reference-typed sibling bypasses the params overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReferenceTypeSiblingIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Log
            {
                public void {|SST2467:Write|}(string format, params object[] args)
                {
                }

                public void Write(string format, Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies a value-typed sibling that boxes to the element type bypasses the params overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BoxingValueTypeSiblingIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void {|SST2467:Write|}(string format, params object[] args)
                {
                }

                public void Write(string format, int value)
                {
                }
            }
            """);

    /// <summary>Verifies a <c>string</c> sibling bypasses a <c>params object[]</c> overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StringSiblingIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void {|SST2467:Write|}(string format, params object[] args)
                {
                }

                public void Write(string format, string message)
                {
                }
            }
            """);

    /// <summary>Verifies an interface-typed sibling bypasses a <c>params object[]</c> overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceSiblingIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Log
            {
                public void {|SST2467:Write|}(string format, params object[] args)
                {
                }

                public void Write(string format, IFormattable value)
                {
                }
            }
            """);

    /// <summary>Verifies a sibling implementing the params element interface bypasses the params overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceElementImplementingSiblingIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public interface IAnimal
            {
            }

            public class Dog : IAnimal
            {
            }

            public class Shelter
            {
                public void {|SST2467:Admit|}(string name, params IAnimal[] animals)
                {
                }

                public void Admit(string name, Dog dog)
                {
                }
            }
            """);

    /// <summary>Verifies a subclass sibling bypasses a params overload whose element type is a base class.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SubclassSiblingIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Animal
            {
            }

            public class Dog : Animal
            {
            }

            public class Shelter
            {
                public void {|SST2467:Admit|}(string name, params Animal[] animals)
                {
                }

                public void Admit(string name, Dog dog)
                {
                }
            }
            """);

    /// <summary>Verifies the shape is reported when the params array is the only parameter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleParamsParameterIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Log
            {
                public void {|SST2467:Write|}(params object[] args)
                {
                }

                public void Write(Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies the shape is reported for static overloads.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticOverloadsAreReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public static class Log
            {
                public static void {|SST2467:Write|}(string format, params object[] args)
                {
                }

                public static void Write(string format, Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies the deliberate element-typed overload (allocation-avoiding) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ElementTypedSiblingIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }

                public void Write(string format, object value)
                {
                }
            }
            """);

    /// <summary>Verifies a sibling whose parameter is a base type of the element type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BaseTypedSiblingIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void Write(string format, params string[] values)
                {
                }

                public void Write(string format, object value)
                {
                }
            }
            """);

    /// <summary>Verifies a sibling of a different arity (no trailing element) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ShorterAritySiblingIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }

                public void Write(string format)
                {
                }
            }
            """);

    /// <summary>Verifies a sibling whose leading parameter differs in type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchedLeadingParameterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }

                public void Write(int code, Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies a lone params overload with no sibling is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoneParamsOverloadIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }
            }
            """);

    /// <summary>Verifies two params overloads are not treated as a bypassing pair.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParamsSiblingIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }

                public void Write(string format, params string[] values)
                {
                }
            }
            """);

    /// <summary>Verifies a static params overload and an instance sibling are not treated as a pair.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StaticInstanceMismatchIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Log
            {
                public static void Write(string format, params object[] args)
                {
                }

                public void Write(string format, Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies a sibling whose parameter is unrelated to the element type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedSiblingTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void Write(string format, params int[] values)
                {
                }

                public void Write(string format, string message)
                {
                }
            }
            """);

    /// <summary>Verifies a differently named method is not treated as a sibling.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DifferentNameIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }

                public void Report(string format, Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies a sibling whose parameter does not implement the params element interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterfaceElementUnrelatedSiblingIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public interface IAnimal
            {
            }

            public class Cat
            {
            }

            public class Shelter
            {
                public void Admit(string name, params IAnimal[] animals)
                {
                }

                public void Admit(string name, Cat cat)
                {
                }
            }
            """);

    /// <summary>Verifies a generic sibling is not treated as a bypassing overload.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericSiblingIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }

                public void Write<T>(string format, T value)
                {
                }
            }
            """);

    /// <summary>Verifies a sibling whose capturing parameter is by-reference is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ByReferenceSiblingIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }

                public void Write(string format, ref Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies a sibling whose leading parameter differs only by ref kind is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchedLeadingRefKindIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class Log
            {
                public void Write(string format, params object[] args)
                {
                }

                public void Write(in string format, Exception error)
                {
                }
            }
            """);

    /// <summary>Verifies a parameterless method alongside a params overload is handled without a report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Log
            {
                public void Flush()
                {
                }

                public void Write(string format, params object[] args)
                {
                }
            }
            """);

    /// <summary>Verifies a delegate type with a params signature is skipped by the type-kind guard.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync("public delegate void Callback(string format, params object[] args);");
}
