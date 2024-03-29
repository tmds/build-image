using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

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

    public bool IsAvailable([NotNullWhen(false)]out string? errorMessage)
    {
        errorMessage = null;

        // Don't check for podman as it doesn't hava a daemon that should be running.
        if (Command == Podman)
        {
            return true;
        }

        StringBuilder stdout = new();
        ProcessRunner.Run(Command, new[] { "info", "-f", "{{ json . }}" }, stdout);
        JsonDocument doc = JsonDocument.Parse(stdout.ToString());
        if (!doc.RootElement.TryGetProperty("ServerErrors", out JsonElement serverErrors))
        {
            return true;
        }
        else if (serverErrors.ValueKind == JsonValueKind.Array && serverErrors.GetArrayLength() == 0)
        {
            return true;
        }
        else
        {
            errorMessage = string.Join(Environment.NewLine, serverErrors.EnumerateArray());
            return false;
        }
    }

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
                StringBuilder stdout = new();
                ProcessRunner.Run(cmd, new[] { "version", "-f", "{{ .Client.Version }}" });
                Version.TryParse(stdout.ToString(), out version);
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
            => ProcessRunner.Run(FileName, Arguments, ProcessRunner.WriteToConsoleOut, ProcessRunner.WriteToConsoleOut, EnvironmentVariables.Select(envvar => new KeyValuePair<string, string>(envvar.Item1, envvar.Item2)));
    }
}