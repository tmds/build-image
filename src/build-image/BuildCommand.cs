using System.CommandLine;
using System.CommandLine.IO;

class BuildCommand : RootCommand
{
    private const string DefaultTag = "dotnet-app";

    public BuildCommand() :
        base("Build a container image from a .NET project.")
    {
        var baseOption = new Option<string>(new[] { "--base", "-b" }, "Flavor of the base image");
        var tagOption = new Option<string>(new[] { "--tag", "-t" }, $"Name for the built image [default: {DefaultTag}]");
        var asfileOption = new Option<string>( "--as-file", "Generates a Containerfile with the specified name");
        var printOption = new Option<bool>("--print", "Print the Containerfile") { Arity = ArgumentArity.Zero };
        var pushOption = new Option<bool>("--push", "After the build, push the image to the repository") { Arity = ArgumentArity.Zero };
        var archOption = new Option<string>(new[] { "--arch", "-a" }, $"Target architecture ('x64'/'arm64'/'s390x')\nThe base image needs to support the selected architecture");
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

        arch ??= projectInformation.ImageArchitecture;
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

        tag ??= projectInformation.ImageTag ?? DefaultTag;
        if (asfile is null)
        {
            console.WriteLine($"Building image '{tag}' from project '{projectFile}'.");
        }
        else
        {
            console.WriteLine($"Creating Containerfile '{asfile}' for project '{projectFile}'.");
        }
        string dotnetVersion = projectInformation.DotnetVersion;
        sdkVersion ??= dotnetVersion;
        DotnetContainerfileBuilderOptions buildOptions = new()
        {
            ProjectPath = project,
            AssemblyName = projectInformation.AssemblyName,
            TargetPlatform = targetPlatform
        };

        // Build the image.
        baseFlavor ??= projectInformation.ImageBase ?? "";
        // TODO: detect is ASP.NET.
        FlavorInfo flavorInfo = ImageFlavorDatabase.GetFlavorInfo(baseFlavor, dotnetVersion, sdkVersion);
        buildOptions.RuntimeImage = flavorInfo.RuntimeImage;
        buildOptions.SdkImage = flavorInfo.SdkImage;
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

        if (asfile is not null)
        {
            if (containerEngine is not null)
            {
                console.WriteLine("To build the image, run:");
                console.WriteLine(containerEngine.GetBuildCommandLine(containerfileName, tag, contextDir));
            }
            return 0;
        }

        bool buildSuccessful = containerEngine!.TryBuild(console, containerfileName, tag, contextDir);
        File.Delete(containerfileName);
        if (!buildSuccessful)
        {
            console.Error.WriteLine($"Failed to build image.");
            return 1;
        }

        if (push)
        {
            console.WriteLine($"Pushing image '{tag}' to repository.");
            bool pushSuccesful = containerEngine!.TryPush(console, tag);
            if (!pushSuccesful)
            {
                console.Error.WriteLine($"Failed to push image.");
                return 1;
            }
        }

        return 0;
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
            _ => null,
        };
        return platform is not null;
    }
}