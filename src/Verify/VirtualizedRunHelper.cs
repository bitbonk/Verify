class VirtualizedRunHelper
{
    internal static IEnvironment Env { private get; set; } = PhysicalEnvironment.Instance;

    // e.g. WSL or docker run (https://github.com/VerifyTests/Verify#unit-testing-inside-virtualized-environment)
    internal bool AppearsToBeLocalVirtualizedRun { get; private set; }
    internal bool Initialized { get; private set; }

    string? originalCodeBaseRootAbsolute;
    string? mappedCodeBaseRootAbsolute;

    static readonly char[] separators =
    [
        '\\',
        '/'
    ];

    public VirtualizedRunHelper(Assembly userAssembly)
        : this(
            AttributeReader.TryGetSolutionDirectory(userAssembly, false, out var sln) ? sln : null,
            AttributeReader.TryGetProjectDirectory(userAssembly, false, out var proj) ? proj : null)
    {
    }

    internal VirtualizedRunHelper(string? solutionDir, string? projectDir)
    {
        if (solutionDir != null)
        {
            Initialized = TryInitializeFromBuildTimePath(solutionDir, solutionDir);
            if (!Initialized)
            {
                if (projectDir != null)
                {
                    Initialized = TryInitializeFromBuildTimePath(solutionDir, projectDir);
                }
            }

            return;
        }

        if (projectDir != null)
        {
            Initialized = TryInitializeFromBuildTimePath(projectDir, projectDir);
        }
    }

    public string GetMappedBuildPath(string path)
    {
        if (!Initialized &&
            !path.Equals(originalCodeBaseRootAbsolute, StringComparison.OrdinalIgnoreCase))
        {
            // If not initialized (the solution or project dir path might not have been set or they are
            //   not sufficient due to insufficient nesting and hence appearing to have no cross-section
            //   with the current dir)
            TryInitializeFromBuildTimePath(originalCodeBaseRootAbsolute, path);
            Initialized = true;
        }

        if (!AppearsToBeLocalVirtualizedRun)
        {
            return path;
        }

        if (originalCodeBaseRootAbsolute == null)
        {
            return path;
        }

        if (!path.StartsWith(originalCodeBaseRootAbsolute, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var mappedPathRelative = path[originalCodeBaseRootAbsolute.Length..]
            .Replace('\\', '/');

        var mappedPath = Env.CombinePaths(mappedCodeBaseRootAbsolute!, mappedPathRelative);

        if (Env.PathExists(mappedPath))
        {
            return mappedPath;
        }

        return path;
    }

    bool TryInitializeFromBuildTimePath(string? originalCodeBaseRoot, string buildTimePath)
    {
        if (AppearsBuiltOnCurrentPlatform(buildTimePath.AsSpan()))
        {
            AppearsToBeLocalVirtualizedRun = false;
            return true;
        }

        if (InnerTryInitializeFromBuildTimePath(originalCodeBaseRoot, buildTimePath, out mappedCodeBaseRootAbsolute, out var originalAbsolute))
        {
            originalCodeBaseRootAbsolute = originalAbsolute;
            AppearsToBeLocalVirtualizedRun = true;
            return true;
        }

        return false;
    }

    static bool InnerTryInitializeFromBuildTimePath(
        string? originalCodeBaseRoot,
        string buildTimePath,
        [NotNullWhen(true)] out string? mappedCodeBaseRootAbsolute,
        [NotNullWhen(true)] out string? originalCodeBaseRootAbsolute) =>
        // First attempt - by the cross-section of the build-time path and run-time path
        TryFindByCrossSectionOfBuildRunPath(originalCodeBaseRoot, out mappedCodeBaseRootAbsolute, out originalCodeBaseRootAbsolute) ||

        // Fallback attempt - via the existence of mapped build-time path within the run-time FS
        // 1) try to get relative if we can (if not, at least cut the first part - or not)
        TryGetRelative(originalCodeBaseRoot, buildTimePath, out mappedCodeBaseRootAbsolute, out originalCodeBaseRootAbsolute);

    static bool TryGetRelative(
        string? originalCodeBaseRoot,
        string buildTimePath,
        [NotNullWhen(true)] out string? codeBaseRootAbsolute,
        [NotNullWhen(true)] out string? baseRootAbsolute)
    {
        var buildTimePathRelative = GetBuildTimePathRelative(originalCodeBaseRoot, buildTimePath);

        // iteratively decrease build-time path from start and try to append to root and check existence
        do
        {
            var currentDir = Env.CurrentDirectory;
            do
            {
                var testMappedPath = Env.CombinePaths(currentDir, buildTimePathRelative.ToString());
                if (Env.PathExists(testMappedPath))
                {
                    baseRootAbsolute = buildTimePath[..^buildTimePathRelative.Length];
                    codeBaseRootAbsolute = currentDir;
                    return true;
                }

                currentDir = TryRemoveDirFromEndOfPath(currentDir);
            } while (currentDir.Length > 0);

            buildTimePathRelative = TryRemoveDirFromStartOfPath(buildTimePathRelative);
        } while (buildTimePathRelative.Length > 0);

        codeBaseRootAbsolute = null;
        baseRootAbsolute = null;
        return false;
    }

    static bool TryFindByCrossSectionOfBuildRunPath(
        string? originalCodeBaseRoot,
        [NotNullWhen(true)] out string? mappedCodeBaseRootAbsolute,
        [NotNullWhen(true)] out string? codeBaseRootAbsolute)
    {
        if (originalCodeBaseRoot == null)
        {
            mappedCodeBaseRootAbsolute = null;
            codeBaseRootAbsolute = null;
            return false;
        }

        var currentDirectory = Env.CurrentDirectory;
        var currentDirRelativeToAppRoot = currentDirectory.TrimStart(separators);
        //remove the drive info from the code root
        var mappedCodeBaseRootRelative = originalCodeBaseRoot.Replace('\\', '/');
        while (true)
        {
            currentDirRelativeToAppRoot = TryRemoveDirFromStartOfPath(currentDirRelativeToAppRoot.AsSpan()).ToString();
            if (currentDirRelativeToAppRoot.Length == 0)
            {
                break;
            }
            while (true)
            {
                mappedCodeBaseRootRelative = TryRemoveDirFromStartOfPath(mappedCodeBaseRootRelative.AsSpan()).ToString();
                if (mappedCodeBaseRootRelative.Length == 0)
                {
                    break;
                }
                if (!currentDirRelativeToAppRoot.StartsWith(mappedCodeBaseRootRelative, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                mappedCodeBaseRootAbsolute = currentDirectory[..^currentDirRelativeToAppRoot.Length];

                // the start of paths to be mapped

                codeBaseRootAbsolute = originalCodeBaseRoot[..^mappedCodeBaseRootRelative.Length];

                var testMappedPath = Env.CombinePaths(
                    mappedCodeBaseRootAbsolute,
                    originalCodeBaseRoot[codeBaseRootAbsolute.Length..]
                        .Replace('\\', '/'));

                if (Env.PathExists(testMappedPath))
                {
                    return true;
                }
            }
        }

        mappedCodeBaseRootAbsolute = null;
        codeBaseRootAbsolute = null;
        return false;
    }

    static CharSpan GetBuildTimePathRelative(string? originalCodeBaseRoot, string buildTimePath)
    {
        var buildTimePathRelative = buildTimePath.AsSpan();
        if (originalCodeBaseRoot != null &&
            buildTimePath.StartsWith(originalCodeBaseRoot, StringComparison.OrdinalIgnoreCase))
        {
            buildTimePathRelative = buildTimePathRelative[originalCodeBaseRoot.Length..];
            buildTimePathRelative = buildTimePathRelative.TrimStart(separators);
            if (buildTimePathRelative.Length == 0)
            {
                buildTimePathRelative = buildTimePath.AsSpan();
            }
        }

        // path is actually absolute - let's remove the root
        if (buildTimePathRelative == buildTimePath.AsSpan())
        {
            buildTimePathRelative = TryRemoveDirFromStartOfPath(buildTimePathRelative);
        }

        return buildTimePathRelative;
    }

    static bool AppearsBuiltOnCurrentPlatform(CharSpan buildTimePath) =>
        buildTimePath.Contains(Env.DirectorySeparatorChar) &&
        !buildTimePath.Contains(Env.AltDirectorySeparatorChar);

    static CharSpan TryRemoveDirFromStartOfPath(CharSpan path)
    {
        path = path.TrimStart(separators);

        var nextSeparatorIdx = path.IndexOfAny(separators);
        if (nextSeparatorIdx <= 0 || nextSeparatorIdx == path.Length - 1)
        {
            return CharSpan.Empty;
        }

        path = path[(nextSeparatorIdx + 1)..];

        return path;
    }

    static string TryRemoveDirFromEndOfPath(string path)
    {
        if (path == string.Empty)
        {
            return string.Empty;
        }

        path = path.TrimEnd(separators);

        var nextSeparatorIdx = path.LastIndexOfAny(separators);
        if (nextSeparatorIdx <= 0 ||
            nextSeparatorIdx == path.Length - 1)
        {
            return string.Empty;
        }

        path = path[..nextSeparatorIdx];

        return path;
    }
}