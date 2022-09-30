using Microsoft.Build.Evaluation;

class ProjectInformation
{
    public string? DotnetVersion { get; set; }
    public string? AssemblyName { get; set; }
    public string? ImageTag { get; set; }
    public string? ImageBase { get; set; }
    public string? ImageArchitecture { get; set; }
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
            ImageTag = GetProperty(project, "ImageTag"),
            ImageBase = GetProperty(project, "ImageBase"),
            ImageArchitecture = GetProperty(project, "ImageArchitecture"),
        };

        project.ProjectCollection.UnloadProject(project);

        return info;

        static string? GetProperty(Project project, string name)
        {
            return project.AllEvaluatedProperties.FirstOrDefault(prop => prop.Name == name)?.EvaluatedValue;
        }
    }
}