// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests the shared trim step of the fill-then-trim buffer pattern.</summary>
public sealed class ArrayBuffersUnitTest
{
    /// <summary>How many leading entries the partly filled buffer holds.</summary>
    private const int WrittenCount = 2;

    /// <summary>Verifies a full buffer is handed back without copying.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RightSizeReturnsAFullBufferItselfAsync()
    {
        int[] buffer = [1, 2, 3];

        var result = ArrayBuffers.RightSize(buffer, buffer.Length);

        await Assert.That(result).IsSameReferenceAs(buffer);
    }

    /// <summary>Verifies a partly filled buffer is copied down to the written entries.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RightSizeCopiesThePartThatWasWrittenAsync()
    {
        int[] buffer = [1, 2, 0, 0];

        var result = ArrayBuffers.RightSize(buffer, WrittenCount);

        await Assert.That(result).IsNotSameReferenceAs(buffer);
        await Assert.That(result).IsEquivalentTo([buffer[0], buffer[1]]);
    }

    /// <summary>Verifies nothing written yields an empty array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RightSizeOfNothingIsEmptyAsync() =>
        await Assert.That(ArrayBuffers.RightSize(new string[4], 0).Length).IsEqualTo(0);
}
