using System.Text;

public class DotnetContainerfileBuilderOptions
{
    public string? SdkImage { get; set; }
    public string? RuntimeImage { get; set; }
    public string? ProjectPath { get; set; }
    public string? AssemblyName { get; set; }
    public bool SupportsCacheMount { get; set; }
    public bool SupportsCacheMountSELinuxRelabling { get; set; }
    public string? TargetPlatform { get; set; }
    public string? WorkingDirectory { get; set; }
}

class DotnetContainerfileBuilder
{
    public static string BuildFile(DotnetContainerfileBuilderOptions options)
    {
        const int ContainerUid = 1001;
        const int ContainerGid = 0;
        const string BuildHomeDir = "/home/build";
        const string HomeDir = "/home/app";
        const string TargetRoot = "/rootfs";

        string fromImage = options.RuntimeImage ?? throw new ArgumentNullException(nameof(options.RuntimeImage));
        string buildImage = options.SdkImage ?? throw new ArgumentNullException(nameof(options.SdkImage));
        string projectPath = options.ProjectPath ?? throw new ArgumentNullException(nameof(options.ProjectPath));
        string assemblyName = options.AssemblyName ?? throw new ArgumentNullException(nameof(options.AssemblyName));
        string workingDirectory = options.WorkingDirectory ?? "/app";
        string appDir = workingDirectory;

        var sb = new StringBuilder();
        sb.AppendLine($"ARG UID={ContainerUid}");
        sb.AppendLine($"ARG GID={ContainerGid}");
        sb.AppendLine($"");

        sb.AppendLine($"# Publish application");
        sb.AppendLine($"FROM {buildImage} AS build-env");
        sb.AppendLine($"ARG UID");
        sb.AppendLine($"ARG GID");
        sb.AppendLine("USER 0");

        // Ensure uid and gid are known by the target image.
        sb.AppendLine($"COPY --from={fromImage} /etc/passwd /etc/group /scratch/etc");
        sb.AppendLine($"RUN grep \":$GID:\" /scratch/etc/group || echo \"app:x:$GID:\" >>/scratch/etc/group && mkdir -p {TargetRoot}/etc && cp /scratch/etc/group {TargetRoot}/etc && \\");
        sb.AppendLine($"    grep \":x:$UID:\" /scratch/etc/passwd || echo \"app:x:$UID:$GID::{HomeDir}:/usr/sbin/nologin\" >>/scratch/etc/passwd && mkdir -p {TargetRoot}/etc && cp /scratch/etc/passwd {TargetRoot}/etc");
        // Create a home directory.
        sb.AppendLine($"RUN mkdir -p {TargetRoot}{HomeDir}");

        // Publish the application
        sb.AppendLine($"ENV HOME={BuildHomeDir}");
        sb.AppendLine($"WORKDIR /src");
        sb.AppendLine($"COPY . ./");
        string relabel = options.SupportsCacheMountSELinuxRelabling ? ",Z" : "";
        string cacheMount = options.SupportsCacheMount ? $"--mount=type=cache,id=nuget,target={BuildHomeDir}/.nuget/packages{relabel} " : "";
        sb.AppendLine($"RUN {cacheMount}dotnet restore {projectPath}");
        sb.AppendLine($"RUN {cacheMount}dotnet publish --no-restore -c Release -o {TargetRoot}{appDir} {projectPath}");
 
        // Ensure the application and home directory are owned by uid:gid.
        sb.AppendLine($"RUN chgrp -R $GID {TargetRoot}{appDir} {TargetRoot}{HomeDir} && chmod -R g=u {TargetRoot}{appDir} {TargetRoot}{HomeDir} && chown -R $UID:$GID {TargetRoot}{appDir} {TargetRoot}{HomeDir}");
        sb.AppendLine($"");

        sb.AppendLine($"# Build application image");
        string platformArch = options.TargetPlatform is null ? "" : $"--platform={options.TargetPlatform} ";
        sb.AppendLine($"FROM {platformArch}{fromImage}");
        sb.AppendLine($"ARG UID");
        sb.AppendLine($"ARG GID");
        sb.AppendLine($"COPY --from=build-env {TargetRoot} /");
        sb.AppendLine($"USER $UID:$GID");
        sb.AppendLine("ENV ASPNETCORE_URLS=http://*:8080");
        sb.AppendLine($"ENV HOME={HomeDir}");
        sb.AppendLine($"WORKDIR {workingDirectory}");
        sb.AppendLine($"ENTRYPOINT [\"dotnet\", \"{appDir}/{assemblyName}\"]");
        return sb.ToString();
    }
}