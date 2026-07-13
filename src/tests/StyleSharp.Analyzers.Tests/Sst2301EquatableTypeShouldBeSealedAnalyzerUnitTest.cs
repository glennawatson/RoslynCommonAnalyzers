// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEquatable = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2301EquatableTypeShouldBeSealedAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2301 (types implementing IEquatable&lt;T&gt; should be sealed).</summary>
public class Sst2301EquatableTypeShouldBeSealedAnalyzerUnitTest
{
    /// <summary>Verifies an unsealed class that decides equality against itself is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnsealedEquatableClassIsReportedAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            using System;

            public class {|SST2301:Money|} : IEquatable<Money>
            {
                public bool Equals(Money other) => true;
            }
            """);

    /// <summary>Verifies a sealed class keeps the contract it signs.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealedEquatableClassIsCleanAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Money : IEquatable<Money>
            {
                public bool Equals(Money other) => true;
            }
            """);

    /// <summary>Verifies an abstract class is left to its leaves, which are reported on their own.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AbstractClassIsCleanAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            using System;

            public abstract class Shape : IEquatable<Shape>
            {
                public abstract bool Equals(Shape other);
            }
            """);

    /// <summary>Verifies a struct is not reported: nothing can derive from it, so the asymmetry cannot arise.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StructIsCleanAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            using System;

            public struct Point : IEquatable<Point>
            {
                public bool Equals(Point other) => true;
            }
            """);

    /// <summary>Verifies a record is not reported: its generated equality already carries the type check.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task RecordIsCleanAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            using System;

            public record Person : IEquatable<Person>
            {
                public string Name { get; set; }
            }
            """);

    /// <summary>Verifies a class that decides equality against some other type makes no claim about itself.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EquatableOfAnotherTypeIsCleanAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            using System;

            public class Amount
            {
            }

            public class Money : IEquatable<Amount>
            {
                public bool Equals(Amount other) => true;
            }
            """);

    /// <summary>Verifies only the class that names itself is reported, not the ones below it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The derived type inherits <c>IEquatable&lt;Money&gt;</c>, which says nothing about a <c>Coin</c>.</remarks>
    [Test]
    public async Task DerivedTypeIsReportedAtItsSourceAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            using System;

            public class {|SST2301:Money|} : IEquatable<Money>
            {
                public bool Equals(Money other) => true;
            }

            public class Coin : Money
            {
            }
            """);

    /// <summary>Verifies a generic class that decides equality against its own constructed self is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenericEquatableClassIsReportedAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            using System;

            public class {|SST2301:Box|}<T> : IEquatable<Box<T>>
            {
                public bool Equals(Box<T> other) => true;
            }
            """);

    /// <summary>Verifies a class that signs no equality contract is never looked at.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NonEquatableClassIsCleanAsync()
        => await VerifyEquatable.VerifyAnalyzerAsync(
            """
            public class Money
            {
                public override bool Equals(object obj) => true;

                public override int GetHashCode() => 0;
            }
            """);
}
