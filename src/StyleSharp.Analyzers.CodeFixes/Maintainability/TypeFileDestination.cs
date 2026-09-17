// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO;

namespace StyleSharp.Analyzers;

/// <summary>Checks destinations before a move or rename removes the original document's contents.</summary>
internal static class TypeFileDestination
{
    /// <summary>Declines hosts that cannot safely add or remove SDK-globbed documents.</summary>
    /// <param name="document">The document being moved or renamed.</param>
    /// <returns>Whether creating a document is supported safely by this host.</returns>
    internal static bool IsSupported(Document document) => document.Project.Solution.Workspace.Kind != WorkspaceKind.MSBuild;

    /// <summary>Checks both physical paths and logical document names, including linked projects.</summary>
    /// <param name="document">The original document.</param>
    /// <param name="fileName">The extracted file's name.</param>
    /// <returns>Whether the destination is unoccupied in every affected project.</returns>
    internal static bool IsAvailable(Document document, string fileName)
    {
        var path = GetPath(document, fileName);
        if (path is not null && HasPhysicalCollision(document.Project.Solution, path))
        {
            return false;
        }

        if (HasDocument(document, fileName))
        {
            return false;
        }

        foreach (var linkedId in document.GetLinkedDocumentIds())
        {
            if (document.Project.Solution.GetDocument(linkedId) is { } linked && HasDocument(linked, fileName))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Resolves a physical sibling path when the workspace supplies one.</summary>
    /// <param name="document">The original document.</param>
    /// <param name="fileName">The extracted file's name.</param>
    /// <returns>The destination path, or null for a workspace without physical paths.</returns>
    internal static string? GetPath(Document document, string fileName)
    {
        if (document.FilePath is { } sourcePath && Path.GetDirectoryName(sourcePath) is { Length: > 0 } sourceDirectory)
        {
            return Path.GetFullPath(Path.Combine(sourceDirectory, fileName));
        }

        if (document.Project.FilePath is not { } projectPath || Path.GetDirectoryName(projectPath) is not { Length: > 0 } directory)
        {
            return null;
        }

        foreach (var folder in document.Folders)
        {
            directory = Path.Combine(directory, folder);
        }

        return Path.GetFullPath(Path.Combine(directory, fileName));
    }

    /// <summary>Checks workspace paths and files that the project excludes from compilation.</summary>
    /// <param name="solution">The solution containing the original document.</param>
    /// <param name="path">The proposed physical path.</param>
    /// <returns>Whether the path is already occupied.</returns>
    private static bool HasPhysicalCollision(Solution solution, string path)
    {
        if (ExistsOnDisk(path))
        {
            return true;
        }

        foreach (var project in solution.Projects)
        {
            foreach (var existing in project.Documents)
            {
                if (existing.FilePath is { } existingPath && string.Equals(Path.GetFullPath(existingPath), path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Protects excluded files that cannot be discovered through the workspace.</summary>
    /// <param name="path">The proposed destination.</param>
    /// <returns>Whether a file or directory already occupies the destination.</returns>
    [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035", Justification = "Only the code fix checks disk for excluded files before creating a destination; analyzers never call this helper.")]
    private static bool ExistsOnDisk(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>Checks logical names even when a document has no physical path yet.</summary>
    /// <param name="document">The source or one of its linked copies.</param>
    /// <param name="fileName">The proposed document name.</param>
    /// <returns>Whether the logical folder already contains the name.</returns>
    private static bool HasDocument(Document document, string fileName)
    {
        // Reject names differing only by case too, since the host may use a case-insensitive filesystem.
        foreach (var existing in document.Project.Documents)
        {
            if (!string.Equals(existing.Name, fileName, StringComparison.OrdinalIgnoreCase) || existing.Folders.Count != document.Folders.Count)
            {
                continue;
            }

            if (SameFolders(existing.Folders, document.Folders))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Compares logical folder paths without allocating a joined path.</summary>
    /// <param name="first">The existing document's folders.</param>
    /// <param name="second">The source document's folders.</param>
    /// <returns>Whether both documents occupy the same logical folder.</returns>
    private static bool SameFolders(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        for (var index = 0; index < first.Count; index++)
        {
            if (!string.Equals(first[index], second[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
