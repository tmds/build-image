using System.Diagnostics;

class ContainerEngine
{
    private const string NotFound = "";
    private static string? s_command;

    public static string? Command
    {
        get
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