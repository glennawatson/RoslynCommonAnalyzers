// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifySeal = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2301EquatableTypeShouldBeSealedAnalyzer,
    StyleSharp.Analyzers.Sst2301SealEquatableTypeCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2301SealEquatableTypeCodeFixProvider"/> (SST2301 seal the equatable type).</summary>
public class Sst2301SealEquatableTypeCodeFixUnitTest
{
    /// <summary>An open class that decides equality against itself.</summary>
    private const string PublicClassSource = """
        using System;

        public class {|SST2301:Money|} : IEquatable<Money>
        {
            public bool Equals(Money other) => true;
        }
        """;

    /// <summary>The open class after the fix.</summary>
    private const string PublicClassFixed = """
        using System;

        public sealed class Money : IEquatable<Money>
        {
            public bool Equals(Money other) => true;
        }
        """;

    /// <summary>A class with no modifiers at all, whose leading trivia must survive the fix.</summary>
    private const string NoModifierSource = """
        using System;

        /// <summary>A value.</summary>
        class {|SST2301:Money|} : IEquatable<Money>
        {
            /// <summary>Compares two values.</summary>
            /// <param name="other">The other value.</param>
            /// <returns>Whether they are equal.</returns>
            public bool Equals(Money other) => true;
        }
        """;

    /// <summary>The no-modifier class after the fix.</summary>
    private const string NoModifierFixed = """
        using System;

        /// <summary>A value.</summary>
        sealed class Money : IEquatable<Money>
        {
            /// <summary>Compares two values.</summary>
            /// <param name="other">The other value.</param>
            /// <returns>Whether they are equal.</returns>
            public bool Equals(Money other) => true;
        }
        """;

    /// <summary>A partial class, where <c>partial</c> has to stay last.</summary>
    private const string PartialClassSource = """
        using System;

        public partial class {|SST2301:Money|} : IEquatable<Money>
        {
            public bool Equals(Money other) => true;
        }
        """;

    /// <summary>The partial class after the fix.</summary>
    private const string PartialClassFixed = """
        using System;

        public sealed partial class Money : IEquatable<Money>
        {
            public bool Equals(Money other) => true;
        }
        """;

    /// <summary>Two open equatable classes in one document.</summary>
    private const string FixAllSource = """
        using System;

        public class {|SST2301:Money|} : IEquatable<Money>
        {
            public bool Equals(Money other) => true;
        }

        public class {|SST2301:Weight|} : IEquatable<Weight>
        {
            public bool Equals(Weight other) => true;
        }
        """;

    /// <summary>Both classes after the fix.</summary>
    private const string FixAllFixed = """
        using System;

        public sealed class Money : IEquatable<Money>
        {
            public bool Equals(Money other) => true;
        }

        public sealed class Weight : IEquatable<Weight>
        {
            public bool Equals(Weight other) => true;
        }
        """;

    /// <summary>Verifies the fix appends <c>sealed</c> to the access modifiers.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealsAPublicClassAsync()
        => await VerifySeal.VerifyCodeFixAsync(PublicClassSource, PublicClassFixed);

    /// <summary>Verifies a class with no modifiers keeps its leading trivia when <c>sealed</c> arrives.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealsAClassWithNoModifiersAsync()
        => await VerifySeal.VerifyCodeFixAsync(NoModifierSource, NoModifierFixed);

    /// <summary>Verifies <c>sealed</c> is inserted in front of <c>partial</c>, which stays last.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task SealsAPartialClassAsync()
        => await VerifySeal.VerifyCodeFixAsync(PartialClassSource, PartialClassFixed);

    /// <summary>Verifies Fix All seals every reported class in the document.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FixAllSealsEveryReportedClassAsync()
        => await VerifySeal.VerifyCodeFixAsync(FixAllSource, FixAllFixed);
}
