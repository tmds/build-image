using System.CommandLine;
using System.CommandLine.IO;

class BuildCommand : RootCommand
{
    public BuildCommand() :
        base("Build a container image from a .NET project.")
    {
        var baseOption = new Option<string>(new[] { "--base", "-b" }, "Flavor of the base image");
        var tagOption = new Option<string>(new[] { "--tag", "-t" }, $"Name for the built image");
        var asfileOption = new Option<string>( "--as-file", "Generates a Containerfile with the specified name");
        var printOption = new Option<bool>("--print", "Print the Containerfile") { Arity = ArgumentArity.Zero };
        var pushOption = new Option<bool>("--push", "After the build, push the image to the repository") { Arity = ArgumentArity.Zero };
        var archOption = new Option<string>(new[] { "--arch", "-a" }, $"Target architecture ('x64'/'arm64'/'s390x'/'ppc64le')\nThe base image needs to support the selected architecture");
        var contextOption = new Option<string>(new[] { "--context" }, getDefaultValue: () => ".", "Context directory for the build");
        var portableOption = new Option<bool>("--portable", "Avoid using features that make the Containerfile not portable") { Arity = ArgumentArity.Zero };

        var projectArg = new Argument<string>("PROJECT", getDefaultValue: () => ".", ".NET project to build");
        Add(projectArg);

        // note: order determines the help order
        Add(baseOption);
        Add(tagOption);
        Add(archOption);

        Add(pushOption);

        Add(portableOption);
        Add(contextOption);

        Add(asfileOption);
        Add(printOption);

        this.SetHandler((IConsole console,
                         string? os, string tag, string? asfile, string project, bool push, bool print, string? arch, string? context, bool portable) => Handle(console, os, tag, asfile, project, push, print, arch, context, portable),
                        baseOption, tagOption, asfileOption, projectArg, pushOption, printOption, archOption, contextOption, portableOption);
    }

    public static int Handle(IConsole console, string? baseFlavor, string? tag, string? asfile, string project, bool push, bool print, string? arch, string? contextDir, bool portable)
    {
        ContainerEngineFeature disabledFeatures = ContainerEngineFeature.None;
        if (portable)
        {
            disabledFeatures = ContainerEngineFeature.All;
        }

        ContainerEngine? containerEngine = ContainerEngine.TryCreate(disabledFeatures);
        if (containerEngine is null && asfile is null)
        {
            console.Error.WriteLine("Install podman or docker to build images.");
            return 1;
        }

        contextDir ??= Directory.GetCurrentDirectory();

        if (!Directory.Exists(contextDir))
        {
            console.Error.WriteLine($"The build context directory does not exist.");
            return 1;
        }

        // The working directory will be used as the image build context,
        // verify the project we're build is under it.
        string projectFullPath = Path.Combine(contextDir, project);
        if (!projectFullPath.StartsWith(contextDir))
        {
            console.Error.WriteLine($"Project must be a subdirectory of the context directory.");
            return 1;
        }

        // Find the .NET project file.
        string? projectFile = null;
        if (File.Exists(projectFullPath))
        {
            projectFile = Path.GetFullPath(projectFullPath);
        }
        else if (Directory.Exists(projectFullPath))
        {
            var projFiles = Directory.GetFiles(projectFullPath, "*.??proj");
            if (projFiles.Length > 0)
            {
                projectFile = Path.GetFullPath(projFiles[0]);
            }
        }
        if (projectFile is null)
        {
            console.Error.WriteLine($"Project {projectFullPath} not found.");
            return 1;
        }

        // Find out .NET version and assembly name.
        ProjectInformation projectInformation = ProjectReader.ReadProjectInfo(projectFile);
        if (projectInformation.DotnetVersion is null || projectInformation.AssemblyName is null)
        {
            if (projectInformation.DotnetVersion is null)
            {
                console.Error.WriteLine($"Cannot determine project target framework version.");
            }
            if (projectInformation.AssemblyName is null)
            {
                console.Error.WriteLine($"Cannot determine application assembly name.");
            }
            return 1;
        }

        arch ??= projectInformation.ContainerImageArchitecture;
        if (!TryGetTargetPlatform(arch, out string? targetPlatform))
        {
            console.Error.WriteLine($"Unknown target architecture: {arch}.");
            return 1;
        }

        string projectDirectory = Path.GetDirectoryName(projectFile)!;
        GlobalJson? globalJson = GlobalJsonReader.ReadGlobalJson(projectDirectory);
        string? sdkVersion = null;
        if (globalJson?.SdkVersion != null)
        {
            sdkVersion = $"{globalJson.SdkVersion.Major}.{globalJson.SdkVersion.Minor}";
        }

        List<string> tags = new();
        if (tag is not null)
        {
            tags.Add(tag);
        }
        else
        {
            string? name = projectInformation.ContainerImageName;
            if (name is null)
            {
                name = projectInformation.AssemblyName.ToLowerInvariant();
                if (name.EndsWith(".dll"))
                {
                    name = name.Substring(0, name.Length - 4);
                }
            }

            if (projectInformation.ContainerRegistry is not null)
            {
                name = $"{projectInformation.ContainerRegistry}/{name}";
            }

            if (projectInformation.ContainerImageTag is not null && projectInformation.ContainerImageTags is not null)
            {
                console.Error.WriteLine($"Both ContainerImageTag and ContainerImageTags are specified.");
                return 1;
            }
            if (projectInformation.ContainerImageTags is not null)
            {
                foreach (var t in projectInformation.ContainerImageTags.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    tags.Add($"{name}:{t}");
                }
            }
            else
            {
                string t = projectInformation.ContainerImageTag ?? projectInformation.Version ?? "latest";
                tags.Add($"{name}:{t}");
            }
        }

        if (asfile is null)
        {
            console.WriteLine($"Building image {string.Join(", ", tags.Select(t => $"'{t}'"))} from project '{projectFile}'.");
        }
        else
        {
            console.WriteLine($"Creating Containerfile '{asfile}' for project '{projectFile}'.");
        }
        string dotnetVersion = projectInformation.DotnetVersion;
        sdkVersion ??= dotnetVersion;
        (string name, string value)[] envvars = projectInformation.ContainerEnvironmentVariables;
        envvars = EnsureAspNetUrls(envvars);
        DotnetContainerfileBuilderOptions buildOptions = new()
        {
            ProjectPath = project,
            AssemblyName = projectInformation.AssemblyName,
            TargetPlatform = targetPlatform,
            WorkingDirectory = projectInformation.ContainerWorkingDirectory,
            EnvironmentVariables = envvars,
            Ports = projectInformation.ContainerPorts,
            Labels = projectInformation.ContainerLabels
        };

        // Build the image.
        baseFlavor ??= projectInformation.ContainerBaseImage ?? "";
        // TODO: detect is ASP.NET.
        Flavor? sdkFlavor = projectInformation.ContainerSdkImage is null ? null : new Flavor(projectInformation.ContainerSdkImage);
        ResolvedImages resolvedImages = ImageDatabase.Resolve(new Flavor(baseFlavor), sdkFlavor, dotnetVersion, sdkVersion);
        buildOptions.RuntimeImage = resolvedImages.BaseImage;
        buildOptions.SdkImage = resolvedImages.SdkImage;
        if (containerEngine is not null)
        {
            buildOptions.SupportsCacheMount = containerEngine.SupportsCacheMount;
            buildOptions.SupportsCacheMountSELinuxRelabling = containerEngine.SupportsCacheMountSELinuxRelabling;
        }
        var containerfileContent = DotnetContainerfileBuilder.BuildFile(buildOptions);
        if (print)
        {
            Console.WriteLine("--");
            Console.WriteLine(containerfileContent);
            Console.WriteLine("--");
        }

        string containerfileName = asfile ?? "Containerfile." + Path.GetRandomFileName();
        File.WriteAllText(containerfileName, containerfileContent);

        string firstTag = tags.First();
        if (asfile is not null)
        {
            if (containerEngine is not null)
            {
                console.WriteLine("To build the image, run:");
                console.WriteLine(containerEngine.GetBuildCommandLine(containerfileName, firstTag, contextDir));
            }
            return 0;
        }

        bool buildSuccessful = containerEngine!.TryBuild(console, containerfileName, firstTag, contextDir);
        File.Delete(containerfileName);
        if (!buildSuccessful)
        {
            console.Error.WriteLine($"Failed to build image.");
            return 1;
        }

        for (int i = 1; i < tags.Count; i++)
        {
            string t = tags[i];
            bool tagSuccesful = containerEngine!.TryTag(console, firstTag, t);
            if (!tagSuccesful)
            {
                console.Error.WriteLine($"Failed to tag image.");
                return 1;
            }
        }

        if (push)
        {
            foreach (var t in tags)
            {
                console.WriteLine($"Pushing image '{t}' to repository.");
                bool pushSuccesful = containerEngine!.TryPush(console, t);
                if (!pushSuccesful)
                {
                    console.Error.WriteLine($"Failed to push image.");
                    return 1;
                }
            }
        }

        return 0;
    }

    private static (string name, string value)[] EnsureAspNetUrls((string name, string value)[] envvars)
    {
        if (envvars.Any(e => e.name == "ASPNETCORE_URLS"))
        {
            return envvars;
        }
        List<(string, string)> list = new(envvars);
        list.Add(("ASPNETCORE_URLS", "http://*:8080"));
        return list.ToArray();
    }

    private static bool TryGetTargetPlatform(string? arch, out string? platform)
    {
        platform = null;
        if (arch is null)
        {
            return true;
        }
        platform = arch switch
        {
            "x64" => "linux/amd64",
            "arm64" => "linux/arm64",
            "s390x" => "linux/s390x",
            "ppc64le" => "linux/ppc64le",
            _ => null,
        };
        return platform is not null;
    }
}