using System.CommandLine;
using System.Diagnostics;

class BuildCommand : RootCommand
{
    public BuildCommand() :
        base("Build an image from a .NET project.")
    {
        var osOption = new Option<string>("--os");
        var tagOption = new Option<string>("--tag", getDefaultValue: () => "dotnet-app");
        var asDockerfileOption = new Option<string>("--as-dockerfile");
        var projectOption = new Option<string>("--project", getDefaultValue: () => ".");

        Add(osOption);
        Add(tagOption);
        Add(asDockerfileOption);
        Add(projectOption);

        this.SetHandler((string? os, string tag, string? asDockerfile, string project) => Handle(os, tag, asDockerfile, project),
                        osOption, tagOption, asDockerfileOption, projectOption);
    }

    public static int Handle(string? os, string tag, string? asDockerfile, string project)
    {
        // The working directory will be used as the image build context,
        // verify the project we're build is under it.
        string projectFullPath = Path.GetFullPath(project);
        if (!projectFullPath.StartsWith(Directory.GetCurrentDirectory()))
        {
            Console.Error.WriteLine($"Project must be a subdirectory of the current working directory.");
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
            Console.Error.WriteLine($"Project {projectFullPath} not found.");
            return 1;
        }

        Console.WriteLine($"Building image '{tag}' from project {projectFullPath}.");

        // Find out .NET version and assembly name.
        ProjectInformation projectInformation = ProjectReader.ReadProjectInfo(projectFile);
        if (projectInformation.DotnetVersion is null || projectInformation.AssemblyName is null)
        {
            if (projectInformation.DotnetVersion is null)
            {
                Console.Error.WriteLine($"Cannot determine project target framework version.");
            }
            if (projectInformation.AssemblyName is null)
            {
                Console.Error.WriteLine($"Cannot determine application assembly name.");
            }
            return 1;
        }
        string dotnetVersion = projectInformation.DotnetVersion;
        DotnetDockerfileBuilderOptions options = new()
        {
            ProjectPath = project,
            AssemblyName = projectInformation.AssemblyName
        };

        // Build the image.
        os ??= "";
        if (os.StartsWith("ubi"))
        {
            string versionNoDot = dotnetVersion.Replace(".", "");
            string baseOs = os;
            if (baseOs == "ubi")
            {
                baseOs = "ubi8"; // TODO: switch based on dotnetVersion
            }
            options.FromImage = $"registry.access.redhat.com/{baseOs}/dotnet-{versionNoDot}-runtime";
            options.BuildImage = $"registry.access.redhat.com/{baseOs}/dotnet-{versionNoDot}";
            options.WorkDir = "${DOTNET_APP_PATH}/../src";
            options.OutputDir = "${DOTNET_APP_PATH}";
        }
        else
        {
            string imageTag = dotnetVersion;
            if (!string.IsNullOrEmpty(os))
            {
                imageTag += $"-{os}";
            }
            options.FromImage = $"mcr.microsoft.com/dotnet/aspnet:{imageTag}"; // TODO: detect is ASP.NET.
            options.BuildImage = $"mcr.microsoft.com/dotnet/sdk:{imageTag}";
            options.WorkDir = "/app";
            options.OutputDir = "/app";
        }
        var dockerfileContent = DotnetDockerfileBuilder.BuildDockerFile(options);
        string dockerFileName = asDockerfile ?? "Dockerfile." + Path.GetRandomFileName();
        File.WriteAllText(dockerFileName, dockerfileContent);
        if (asDockerfile is not null)
        {
            return 0;
        }
        var process = Process.Start("podman", new[] { "build", "-f", dockerFileName, "-t", tag, "." });
        process.WaitForExit();
        File.Delete(dockerFileName);
        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"Failed to build image.");
            return 1;
        }

        return 0;
    }
}