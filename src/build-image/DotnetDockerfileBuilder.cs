using System.Text;

public class DotnetDockerfileBuilderOptions
{
    public string? SdkImage { get; set; }
    public string? RuntimeImage { get; set; }
    public string? ProjectPath { get; set; }
    public string? AssemblyName { get; set; }
    public bool SupportsCacheMount { get; set; }
    public bool SupportsCacheMountSELinuxRelabling { get; set; }
}

class DotnetDockerfileBuilder
{
    public static string BuildDockerFile(DotnetDockerfileBuilderOptions options)
    {
        string fromImage = options.RuntimeImage ?? throw new ArgumentNullException(nameof(options.RuntimeImage));
        string buildImage = options.SdkImage ?? throw new ArgumentNullException(nameof(options.SdkImage));
        string projectPath = options.ProjectPath ?? throw new ArgumentNullException(nameof(options.ProjectPath));
        string assemblyName = options.AssemblyName ?? throw new ArgumentNullException(nameof(options.AssemblyName));

        var sb = new StringBuilder();

        sb.AppendLine($"# Publish application");
        sb.AppendLine($"FROM {buildImage} AS build-env");
        sb.AppendLine("USER 0");
        sb.AppendLine($"WORKDIR /src");
        sb.AppendLine($"COPY . ./");
        string relabel = options.SupportsCacheMountSELinuxRelabling ? ",Z" : "";
        string cacheMount = options.SupportsCacheMount ? $"--mount=type=cache,id=nuget,target=${{HOME}}/.nuget/packages{relabel} " : "";
        sb.AppendLine($"RUN {cacheMount}dotnet restore {projectPath}");
        sb.AppendLine($"RUN {cacheMount}dotnet publish --no-restore -c Release -o /out {projectPath}");
        sb.AppendLine($"");

        sb.AppendLine($"# Build application image");
        sb.AppendLine($"FROM {fromImage}");
        sb.AppendLine($"COPY --from=build-env /out /app");
        sb.AppendLine("ENV ASPNETCORE_URLS=http://*:8080");
        sb.AppendLine("WORKDIR /app");
        sb.AppendLine($"ENTRYPOINT [\"dotnet\", \"/app/{assemblyName}\"]");
        return sb.ToString();
    }
}