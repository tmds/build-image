using System.CommandLine;
using System.CommandLine.IO;

class BuildCommand : RootCommand
{
    public BuildCommand() :
        base("Build a container image from a .NET project.")
    {
        var baseOption = new Option<string>(new[] { "--base", "-b" }, "Flavor of the base image");
        var tagOption = new Option<string>(new[] { "--tag", "-t" }, getDefaultValue: () => "dotnet-app", "Name for the built image");
        var asDockerfileOption = new Option<string>("--as-dockerfile", "Generates a Dockerfile with the specified name");
        var projectArg = new Argument<string>("PROJECT", getDefaultValue: () => ".", ".NET project to build");

        Add(baseOption);
        Add(tagOption);
        Add(asDockerfileOption);
        Add(projectArg);

        this.SetHandler((IConsole console,
                         string? os, string tag, string? asDockerfile, string project) => Handle(console, os, tag, asDockerfile, project),
                        baseOption, tagOption, asDockerfileOption, projectArg);
    }

    public static int Handle(IConsole console, string? baseFlavor, string tag, string? asDockerfile, string project)
    {
        ContainerEngine? containerEngine = ContainerEngine.TryCreate();
        if (containerEngine is null && asDockerfile is null)
        {
            console.Error.WriteLine("Install podman or docker to build images.");
            return 1;
        }

        // The working directory will be used as the image build context,
        // verify the project we're build is under it.
        string projectFullPath = Path.GetFullPath(project);
        if (!projectFullPath.StartsWith(Directory.GetCurrentDirectory()))
        {
            console.Error.WriteLine($"Project must be a subdirectory of the current working directory.");
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
                projectFile = projFiles[0];
            }
        }
        if (projectFile is null)
        {
            console.Error.WriteLine($"Project {projectFullPath} not found.");
            return 1;
        }

        Console.WriteLine($"Building image '{tag}' from project {projectFullPath}.");

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
        string dotnetVersion = projectInformation.DotnetVersion;
        DotnetDockerfileBuilderOptions buildOptions = new()
        {
            ProjectPath = project,
            AssemblyName = projectInformation.AssemblyName
        };

        // Build the image.
        baseFlavor ??= "";
        if (baseFlavor.StartsWith("ubi"))
        {
            string versionNoDot = dotnetVersion.Replace(".", "");
            string baseOs = baseFlavor;
            if (baseOs == "ubi")
            {
                baseOs = "ubi8"; // TODO: switch based on dotnetVersion
            }
            buildOptions.FromImage = $"registry.access.redhat.com/{baseOs}/dotnet-{versionNoDot}-runtime";
            buildOptions.BuildImage = $"registry.access.redhat.com/{baseOs}/dotnet-{versionNoDot}";
            buildOptions.WorkDir = "${DOTNET_APP_PATH}/../src";
            buildOptions.OutputDir = "${DOTNET_APP_PATH}";
        }
        else
        {
            string imageTag = dotnetVersion;
            if (!string.IsNullOrEmpty(baseFlavor))
            {
                imageTag += $"-{baseFlavor}";
            }
            buildOptions.FromImage = $"mcr.microsoft.com/dotnet/aspnet:{imageTag}"; // TODO: detect is ASP.NET.
            buildOptions.BuildImage = $"mcr.microsoft.com/dotnet/sdk:{imageTag}";
            buildOptions.WorkDir = "/app";
            buildOptions.OutputDir = "/app";
        }
        buildOptions.SupportsCacheMount = containerEngine is null ? false : containerEngine.SupportsCacheMount;
        var dockerfileContent = DotnetDockerfileBuilder.BuildDockerFile(buildOptions);
        string dockerFileName = asDockerfile ?? "Dockerfile." + Path.GetRandomFileName();
        File.WriteAllText(dockerFileName, dockerfileContent);

        if (asDockerfile is not null)
        {
            return 0;
        }

        bool buildSuccessful = containerEngine!.TryBuild(console, dockerFileName, tag, contextDir: ".");
        File.Delete(dockerFileName);
        if (!buildSuccessful)
        {
            console.Error.WriteLine($"Failed to build image.");
            return 1;
        }

        return 0;
    }
}