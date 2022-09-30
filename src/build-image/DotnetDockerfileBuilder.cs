using System.Text;

public class DotnetDockerfileBuilderOptions
{
    public string? SdkImage { get; set; }
    public string? RuntimeImage { get; set; }
    public string? ProjectPath { get; set; }
    public string? AssemblyName { get; set; }
    public bool SupportsCacheMount { get; set; }
    public bool SupportsCacheMountSELinuxRelabling { get; set; }
    public string? TargetPlatform { get; set; }
}

class DotnetDockerfileBuilder
{
    public static string BuildDockerFile(DotnetDockerfileBuilderOptions options)
    {
        const int ContainerUid = 1001;
        const int ContainerGid = 0;
        const string HomeDir = "/home/app";
        const string AppDir = "/app";

        string fromImage = options.RuntimeImage ?? throw new ArgumentNullException(nameof(options.RuntimeImage));
        string buildImage = options.SdkImage ?? throw new ArgumentNullException(nameof(options.SdkImage));
        string projectPath = options.ProjectPath ?? throw new ArgumentNullException(nameof(options.ProjectPath));
        string assemblyName = options.AssemblyName ?? throw new ArgumentNullException(nameof(options.AssemblyName));

        var sb = new StringBuilder();
        sb.AppendLine($"ARG UID={ContainerUid} GID={ContainerGid}");
        sb.AppendLine($"");
        sb.AppendLine($"# Publish application");
        sb.AppendLine($"FROM {buildImage} AS build-env");
        sb.AppendLine($"ARG UID GID");
        sb.AppendLine("USER 0");
        // Ensure uid and gid are known by the target image.
        sb.AppendLine($"COPY --from={fromImage} /etc/passwd /etc/group /scratch/etc");
        sb.AppendLine($"RUN grep \":$GID:\" /scratch/etc/group || echo \"app:x:$GID:\" >>/scratch/etc/group && mkdir -p /rootfs/etc && cp /scratch/etc/group /rootfs/etc");
        sb.AppendLine($"RUN grep \":x:$UID:\" /scratch/etc/passwd || echo \"app:x:$UID:$GID::{HomeDir}:/usr/sbin/nologin\" >>/scratch/etc/passwd && mkdir -p /rootfs/etc && cp /scratch/etc/passwd /rootfs/etc");
        // Create a home directory.
        sb.AppendLine($"RUN mkdir -p /rootfs{HomeDir}");
        // Publish the application
        sb.AppendLine($"WORKDIR /src");
        sb.AppendLine($"COPY . ./");
        string relabel = options.SupportsCacheMountSELinuxRelabling ? ",Z" : "";
        string cacheMount = options.SupportsCacheMount ? $"--mount=type=cache,id=nuget,target=${{HOME}}/.nuget/packages{relabel} " : "";
        sb.AppendLine($"RUN {cacheMount}dotnet restore {projectPath}");
        sb.AppendLine($"RUN {cacheMount}dotnet publish --no-restore -c Release -o /rootfs/app {projectPath}");
        // Ensure the application and home directory are owned by uid:gid.
        sb.AppendLine($"RUN chgrp -R $GID /rootfs{AppDir} /rootfs{HomeDir} && chmod -R g=u /rootfs{AppDir} /rootfs{HomeDir} && chown -R $UID:$GID /rootfs{AppDir} /rootfs{HomeDir}");
        sb.AppendLine($"");

        sb.AppendLine($"# Build application image");
        string platformArch = options.TargetPlatform is null ? "" : $"--platform={options.TargetPlatform} ";
        sb.AppendLine($"FROM {platformArch}{fromImage}");
        sb.AppendLine($"ARG UID GID");
        sb.AppendLine($"COPY --from=build-env /rootfs /");
        sb.AppendLine($"USER $UID:$GID");
        sb.AppendLine("ENV ASPNETCORE_URLS=http://*:8080");
        sb.AppendLine($"ENV HOME={HomeDir}");
        sb.AppendLine($"WORKDIR {AppDir}");
        sb.AppendLine($"ENTRYPOINT [\"dotnet\", \"{AppDir}/{assemblyName}\"]");
        return sb.ToString();
    }
}