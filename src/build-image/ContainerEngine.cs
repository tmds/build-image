using System.CommandLine;
using System.CommandLine.IO;
using System.Diagnostics;
using System.Text;

[Flags]
enum ContainerEngineFeature
{
    None = 0,
    CacheMounts,
    All = CacheMounts
}

class ContainerEngine
{
    public const string Docker = "docker";
    public const string Podman = "podman";

    public string Command { get; }
    public Version Version { get; }
    private ContainerEngineFeature _disabledFeatures;

    private bool IsDisabled(ContainerEngineFeature feature) => (_disabledFeatures & ContainerEngineFeature.CacheMounts) != 0;

    public bool SupportsCacheMount
    {
        get
        {
            if (IsDisabled(ContainerEngineFeature.CacheMounts))
            {
                return false;
            }
            if (Command == Podman)
            {
                return Version.Major >= 4;
            }
            return true;
        }
    }

    public bool SupportsCacheMountSELinuxRelabling
    {
        get
        {
            return SupportsCacheMount && Command == Podman;
        }
    }

    private ContainerEngine(string command, Version version, ContainerEngineFeature disabledFeatures)
    {
        Command = command;
        Version = version;
        _disabledFeatures = disabledFeatures;
    }

    public static ContainerEngine? TryCreate(ContainerEngineFeature disableFeatures = ContainerEngineFeature.None)
    {
        string? command = null;
        Version? version = null;
        foreach (var cmd in new[] { Podman, Docker })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = cmd,
                    ArgumentList = { "version", "-f", "{{ .Client.Version }}" },
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                })!;
                process.WaitForExit();
                string stdout = process.StandardOutput.ReadToEnd().Trim();
                Version.TryParse(stdout, out version);
                command = cmd;
                break;
            }
            catch
            { }
        }

        if (command is null)
        {
            return null;
        }

        return new ContainerEngine(command, version ?? new Version(), disableFeatures);
    }

    public bool TryBuild(IConsole console, string dockerFileName, string tag, string contextDir)
    {
        ProcessCommand command = GetBuildCommand(dockerFileName, tag, contextDir);
        int exitCode = command.Run(console);
        return exitCode == 0;
    }

    internal string GetBuildCommandLine(string dockerFileName, string tag, string contextDir)
    {
        return GetBuildCommand(dockerFileName, tag, contextDir).GetCommandLine();
    }

    private ProcessCommand GetBuildCommand(string dockerFileName, string tag, string contextDir)
    {
        ProcessCommand command = new()
        {
            FileName = Command,
            Arguments = { "build", "-f", dockerFileName, "-t", tag, contextDir }
        };

        if (Command == Docker && !IsDisabled(ContainerEngineFeature.CacheMounts))
        {
            // Enable cache mount support.
            command.EnvironmentVariables.Add(("DOCKER_BUILDKIT", "1"));
        }

        return command;
    }

    public bool TryPush(IConsole console, string tag)
    {
        ProcessCommand command = new()
        {
            FileName = Command,
            Arguments = { "push", tag }
        };

        return command.Run(console) == 0;
    }

    public bool TryTag(IConsole console, string src, string target)
    {
        ProcessCommand command = new()
        {
            FileName = Command,
            Arguments = { "tag", src, target }
        };

        return command.Run(console) == 0;
    }

    class ProcessCommand
    {
        public string FileName { get; set; } = null!;
        public List<string> Arguments { get; } = new();
        public List<(string, string)> EnvironmentVariables { get; } = new();

        public string GetCommandLine()
        {
            StringBuilder sb = new();

            foreach (var env in EnvironmentVariables)
            {
                sb.Append($"{env.Item1}={env.Item2} ");
            }

            sb.Append(FileName);

            foreach (var arg in Arguments)
            {
                sb.Append($" {arg}");
            }

            return sb.ToString();
        }

        public int Run(IConsole console)
        {
            var psi = new ProcessStartInfo
            {
                FileName = FileName,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            foreach (var arg in Arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            foreach (var env in EnvironmentVariables)
            {
                psi.Environment[env.Item1] = env.Item2;
            }

            using var process = Process.Start(psi)!;

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    console.Error.WriteLine(e.Data);
                }
            };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    console.Out.WriteLine(e.Data);
                }
            };

            process.StandardInput.Close();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit();

            return process.ExitCode;
        }
    }
}