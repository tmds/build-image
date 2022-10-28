using Microsoft.Build.Evaluation;

class ProjectInformation
{
    public string? DotnetVersion { get; set; }
    public string? AssemblyName { get; set; }

    public string? Version { get; set; }

    // Match properties of built-in sdk support
    // See https://github.com/dotnet/sdk-container-builds/blob/main/docs/ContainerCustomization.md.
    public string? ContainerImageTag { get; set; }
    public string? ContainerImageName { get; set; }
    public string? ContainerRegistry { get; set; }
    public string? ContainerBaseImage { get; set; }
    public string? ContainerWorkingDirectory { get; set; }
    public string? ContainerImageTags { get; set; }

    // Additional properties
    public string? ContainerImageArchitecture { get; set; }
    public string? ContainerSdkImage { get; set; }
}

class ProjectReader
{
    public static ProjectInformation ReadProjectInfo(string path)
    {
        var project = new Project(path);

        string? tfm = GetProperty(project, "TargetFramework");
        string? dotnetVersion = null;
        if (tfm is not null && tfm.StartsWith("net"))
        {
            dotnetVersion = tfm.Substring(3);
        }

        string? assemblyName = GetProperty(project, "AssemblyName");
        if (assemblyName is not null)
        {
            assemblyName += ".dll";
        }

        var info = new ProjectInformation()
        {
            DotnetVersion = dotnetVersion,
            AssemblyName = assemblyName,
            ContainerImageTag = GetProperty(project, "ContainerImageTag"),
            ContainerImageTags = GetProperty(project, "ContainerImageTags"),
            ContainerImageName = GetProperty(project, "ContainerImageName"),
            ContainerRegistry = GetProperty(project, "ContainerRegistry"),
            ContainerBaseImage = GetProperty(project, "ContainerBaseImage"),
            ContainerWorkingDirectory = GetProperty(project, "ContainerWorkingDirectory"),
            Version = GetProperty(project, "Version"),

            ContainerImageArchitecture = GetProperty(project, "ContainerImageArchitecture"),
            ContainerSdkImage = GetProperty(project, "ContainerSdkImage"),
        };

        project.ProjectCollection.UnloadProject(project);

        return info;

        static string? GetProperty(Project project, string name)
        {
            return project.AllEvaluatedProperties.FirstOrDefault(prop => prop.Name == name)?.EvaluatedValue;
        }
    }
}