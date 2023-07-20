class Flavor
{
    public string BaseName { get; }
    public string? Tag { get; }

    public Flavor(string flavor)
    {
        int colonIndex = flavor.IndexOf(':');
        BaseName = colonIndex == -1 ? flavor : flavor.Substring(0, colonIndex);
        Tag = colonIndex == -1 ? null : flavor.Substring(colonIndex + 1);
    }
}

class ResolvedImages
{
    public string BaseImage { get; init; } = null!;
    public string SdkImage { get; init; } = null!;
    public int? BaseImageAppUser { get; init; }
    public int? BaseImageAppGroup { get; init; }
    public bool BaseImageRunsAsAppUser { get; init; }
}

class ImageDatabase
{
    public static ResolvedImages Resolve(Flavor baseFlavor, Flavor? sdkFlavor, string runtimeVersion, string sdkVersion)
    {
        string baseImage = ResolveImage(baseFlavor, runtimeVersion, isSdk: false, out string resolvedFlavor);
        string sdkImage = ResolveImage(sdkFlavor ?? new Flavor(resolvedFlavor), sdkVersion, isSdk: true, out _);
        bool isRedHatImage = baseImage.Contains("redhat.com");
        return new ResolvedImages()
        {
            BaseImage = baseImage,
            SdkImage = sdkImage,
            BaseImageRunsAsAppUser = isRedHatImage,
            BaseImageAppUser  = isRedHatImage ? 1001 : null,
            BaseImageAppGroup  = isRedHatImage ? 0 : null
        };
    }

    private static bool IsRepositoryName(Flavor flavor) => flavor.BaseName.Contains("/");
    private static bool IsShortName(Flavor flavor) => !IsRepositoryName(flavor);

    private static string ResolveImage(Flavor flavor, string version, bool isSdk, out string resolvedBaseName)
    {
        string baseName = flavor.BaseName;
        if (IsRepositoryName(flavor))
        {
            if (baseName.Contains("redhat.com"))
            {
                // Fall through to short name.
                baseName = "ubi";
            }
            else
            {
                string tag = flavor.Tag ?? version;

                resolvedBaseName = "";

                return $"{baseName}:{tag}";
            }
        }

        // flavor is a short name
        resolvedBaseName = baseName;

        if (baseName.StartsWith("ubi")) // Red Hat image.
        {
            string versionNoDot = version.Replace(".", "");

            return isSdk ? $"registry.access.redhat.com/{DotNetVersionToRedHatBaseImage(version)}/dotnet-{versionNoDot}:latest"
                            : $"registry.access.redhat.com/{DotNetVersionToRedHatBaseImage(version)}/dotnet-{versionNoDot}-runtime:latest";

            static string DotNetVersionToRedHatBaseImage(string version) => version switch
            {
                _ => "ubi8"
            };
        }
        else // Microsoft image.
        {
            string shortName = baseName;

            const string ChiseledSuffix = "-chiseled";
            if (shortName.EndsWith(ChiseledSuffix) && isSdk) // There are no chiseled SDK images.
            {
                shortName = shortName.Substring(0, baseName.Length - ChiseledSuffix.Length);
            }

            string registryName = isSdk ? "mcr.microsoft.com/dotnet/sdk" : "mcr.microsoft.com/dotnet/aspnet";

            // jammy-chiseled is preview for .NET 6.0.
            if (shortName == "jammy-chiseled" && version == "6.0")
            {
                registryName = "mcr.microsoft.com/dotnet/nightly/aspnet";
            }

            string tag = baseName.Length > 0 ? $"{version}-{shortName}" : version;

            return $"{registryName}:{tag}";
        }
    }
}