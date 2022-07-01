using System.Text.Json;

class GlobalJson
{
    public Version? SdkVersion { get; set; }
}

class GlobalJsonReader
{
    public static GlobalJson? ReadGlobalJson(string path)
    {
        path = Path.GetFullPath(path);
        string root = Path.GetPathRoot(path)!;

        do
        {
            string globalJsonFileName = Path.Combine(path, "global.json");
            if (File.Exists(globalJsonFileName))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                GlobalJsonContent? content;
                try
                {
                    content = JsonSerializer.Deserialize<GlobalJsonContent>(File.ReadAllText(globalJsonFileName), options);
                }
                catch
                {
                    return null;
                }
                GlobalJson globalJson = new();
                if (content?.Sdk?.Version is string v && Version.TryParse(v, out Version? result))
                {
                    globalJson.SdkVersion = result;
                }
                return globalJson;
            }
            path = Path.GetDirectoryName(path)!;
        } while (root != path);

        return null;
    }

    record GlobalJsonSdk
    {
        public string? Version { get; set; }
    }

    record GlobalJsonContent
    {
        public GlobalJsonSdk? Sdk { get; set; }
    }
}