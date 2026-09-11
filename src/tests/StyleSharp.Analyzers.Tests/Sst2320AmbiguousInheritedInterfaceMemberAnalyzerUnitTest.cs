// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2320AmbiguousInheritedInterfaceMemberAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2320 (an interface inheriting the same member from two interfaces).</summary>
public class Sst2320AmbiguousInheritedInterfaceMemberAnalyzerUnitTest
{
    /// <summary>Verifies an interface inheriting one member from two unrelated interfaces is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MemberInheritedFromTwoInterfacesReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public interface ILeft
            {
                string Name { get; }
            }

            public interface IRight
            {
                string Name { get; }
            }

            public interface {|SST2320:IBoth|} : ILeft, IRight
            {
            }
            """);

    /// <summary>Verifies a method inherited from two unrelated interfaces is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MethodInheritedFromTwoInterfacesReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public interface ILeft
            {
                int Compute(int value);
            }

            public interface IRight
            {
                int Compute(int value);
            }

            public interface {|SST2320:IBoth|} : ILeft, IRight
            {
            }
            """);

    /// <summary>Verifies re-declaring the member with 'new' resolves the ambiguity and is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReDeclaredMemberIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public interface ILeft
            {
                string Name { get; }
            }

            public interface IRight
            {
                string Name { get; }
            }

            public interface IBoth : ILeft, IRight
            {
                new string Name { get; }
            }
            """);

    /// <summary>Verifies a diamond where both members trace to one common base interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DiamondFromCommonBaseIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public interface IBase
            {
                string Name { get; }
            }

            public interface ILeft : IBase
            {
            }

            public interface IRight : IBase
            {
            }

            public interface IBoth : ILeft, IRight
            {
            }
            """);

    /// <summary>Verifies overloads with different signatures across the two interfaces are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DifferentSignaturesAreCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public interface ILeft
            {
                int Compute(int value);
            }

            public interface IRight
            {
                int Compute(string value);
            }

            public interface IBoth : ILeft, IRight
            {
            }
            """);
}
