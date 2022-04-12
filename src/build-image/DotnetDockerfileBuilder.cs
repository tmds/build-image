using System.Text;

public class DotnetDockerfileBuilderOptions
{
    public string? BuildImage { get; set; }
    public string? FromImage { get; set; }
    public string? ProjectPath { get; set; }
    public string? WorkDir { get; set; }
    public string? OutputDir { get; set; }
    public string? AssemblyName { get; set; }
    public bool SupportsCacheMount { get; set; }
}

class DotnetDockerfileBuilder
{
    public static string BuildDockerFile(DotnetDockerfileBuilderOptions options)
    {
        string fromImage = options.FromImage ?? throw new ArgumentNullException(nameof(options.FromImage));
        string buildImage = options.BuildImage ?? throw new ArgumentNullException(nameof(options.BuildImage));
        string projectPath = options.ProjectPath ?? throw new ArgumentNullException(nameof(options.ProjectPath));
        string workDir = options.WorkDir ?? throw new ArgumentNullException(nameof(options.WorkDir));
        string assemblyName = options.AssemblyName ?? throw new ArgumentNullException(nameof(options.AssemblyName));
        string? outputDir = options.OutputDir;

        var sb = new StringBuilder();

        sb.AppendLine($"FROM {buildImage} AS build-env");
        sb.AppendLine($"WORKDIR {workDir}");
        sb.AppendLine($"");
        sb.AppendLine($"# Copy everything");
        sb.AppendLine($"COPY . ./");
        sb.AppendLine($"# Restore");
        string cacheMount = options.SupportsCacheMount ? "--mount=type=cache,id=nuget,target=${HOME}/.nuget/packages,Z " : "";
        sb.AppendLine($"RUN {cacheMount}dotnet restore {projectPath}");
        sb.AppendLine($"# Build and publish a release");
        sb.AppendLine($"RUN {cacheMount}dotnet publish --no-restore -c Release -o {workDir}/out {projectPath}");
        sb.AppendLine($"");

        sb.AppendLine($"# Build runtime image");
        sb.AppendLine($"FROM {fromImage}");
        if (outputDir is not null)
        {
            sb.AppendLine($"WORKDIR {outputDir}");
        }
        sb.AppendLine($"COPY --from=build-env {workDir}/out .");
        sb.AppendLine($"CMD [\"dotnet\", \"{assemblyName}\"]");
        return sb.ToString();
    }
}