using System.CommandLine;
using System.CommandLine.IO;
using System.Diagnostics;

class ContainerEngine
{
    private const string NotFound = "";
    private static string? s_command;

    private readonly string _command;

    private ContainerEngine(string command)
    {
        _command = command;
    }

    public static ContainerEngine? TryCreate()
    {
        string? command = GetContainerCommand();
        if (command is null)
        {
            return null;
        }

        return new ContainerEngine(command);
    }

    public bool TryBuild(IConsole console, string dockerFileName, string tag, string contextDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _command,
            ArgumentList = {  "build", "-f", dockerFileName, "-t", tag, "." },
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        };

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

        return process.ExitCode != 0;
    }

    public static string? GetContainerCommand()
    {
        if (s_command is null)
        {
            foreach (var command in new[] { "podman", "docker" })
            {
                if (HasContainerCommand(command))
                {
                    s_command = command;
                    break;
                }
            }
            if (s_command is null)
            {
                s_command = NotFound;
            }
        }
        if (s_command == NotFound)
        {
            return null;
        }
        return s_command;
    }

    private static bool HasContainerCommand(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                ArgumentList = { "version" },
                RedirectStandardError = true,
                RedirectStandardOutput = true
            })!;
            process.WaitForExit();
            return true;
        }
        catch
        {
            return false;
        }
    }
}