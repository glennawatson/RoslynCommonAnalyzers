// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>A file extracted from a document, ready to add beside it.</summary>
/// <param name="FileName">The file name.</param>
/// <param name="Text">The file's text.</param>
internal readonly record struct ExtractedFile(string FileName, SourceText Text);
