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
        var asDockerfileOption = new Option<string>( "--as-dockerfile", "Generates a Containerfile with the specified name");
        var printOption = new Option<bool>("--print", "Print the dockerfile.") { Arity = ArgumentArity.Zero };
        var pushOption = new Option<bool>("--push", "After the build, push the image to the repository") { Arity = ArgumentArity.Zero };
        var archOption = new Option<string>(new[] { "--arch" }, $"Target architecture ('x64'/'arm64'/'s390x')\nThe base image needs to support the selected architecture");
        var contextOption = new Option<string>(new[] { "--context" }, getDefaultValue: () => ".", "Context directory for the build");

        Add(baseOption);
        Add(tagOption);
        Add(pushOption);
        Add(asDockerfileOption);
        Add(printOption);
        Add(archOption);
        Add(contextOption);

        var projectArg = new Argument<string>("PROJECT", getDefaultValue: () => ".", ".NET project to build");
        Add(projectArg);

        this.SetHandler((IConsole console,
                         string? os, string tag, string? asDockerfile, string project, bool push, bool print, string? arch, string? context) => Handle(console, os, tag, asDockerfile, project, push, print, arch, context),
                        baseOption, tagOption, asDockerfileOption, projectArg, pushOption, printOption, archOption, contextOption);
    }

    public static int Handle(IConsole console, string? baseFlavor, string? tag, string? asDockerfile, string project, bool push, bool print, string? arch, string? contextDir)
    {
        ContainerEngine? containerEngine = ContainerEngine.TryCreate();
        if (containerEngine is null && asDockerfile is null)
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
        if (asDockerfile is null)
        {
            console.WriteLine($"Building image '{tag}' from project '{projectFile}'.");
        }
        else
        {
            console.WriteLine($"Creating Containerfile '{asDockerfile}' for project '{projectFile}'.");
        }
        string dotnetVersion = projectInformation.DotnetVersion;
        sdkVersion ??= dotnetVersion;
        DotnetDockerfileBuilderOptions buildOptions = new()
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
        var dockerfileContent = DotnetDockerfileBuilder.BuildDockerFile(buildOptions);
        if (print)
        {
            Console.WriteLine("--");
            Console.WriteLine(dockerfileContent);
            Console.WriteLine("--");
        }

        string dockerFileName = asDockerfile ?? "Containerfile." + Path.GetRandomFileName();
        File.WriteAllText(dockerFileName, dockerfileContent);

        if (asDockerfile is not null)
        {
            if (containerEngine is not null)
            {
                console.WriteLine("To build the image, run:");
                console.WriteLine(containerEngine.GetBuildCommandLine(dockerFileName, tag, contextDir));
            }
            return 0;
        }

        bool buildSuccessful = containerEngine!.TryBuild(console, dockerFileName, tag, contextDir);
        File.Delete(dockerFileName);
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