class FlavorInfo
{
    public string Flavor { get; init; } = null!;
    public string BaseImage { get; init; } = null!;
    public string SdkImage { get; init; } = null!;
}

class ImageFlavorDatabase
{
    public static FlavorInfo GetFlavorInfo(string baseFlavor, string? sdkFlavor, string runtimeVersion, string sdkVersion)
    {
        return new FlavorInfo()
        {
            Flavor = baseFlavor,
            BaseImage = ResolveImage(baseFlavor, runtimeVersion, isSdk: false, out string resolvedFlavor),
            SdkImage = ResolveImage(sdkFlavor ?? resolvedFlavor, sdkVersion, isSdk: true, out _)
        };
    }

    private static bool IsRepositoryName(string flavor) => flavor.Contains("/");
    private static bool IsShortName(string flavor) => !IsRepositoryName(flavor);

    private static string ResolveImage(string flavor, string version, bool isSdk, out string resolvedFlavor)
    {
        if (IsRepositoryName(flavor))
        {
            if (flavor.Contains("redhat.com"))
            {
                // Fall through to short name.
                flavor = "ubi";
            }
            else
            {
                int colonPos = flavor.IndexOf(':');

                if (colonPos == -1) // example: flavor = mcr.microsoft.com/dotnet/runtime
                {
                    resolvedFlavor = "";
                    return $"{flavor}:{version}";
                }
                else // example: flavor = mcr.microsoft.com/dotnet/runtime:6.0-alpine
                {
                    string registryName = flavor.Substring(0, colonPos);
                    string tag = flavor.Substring(colonPos + 1);

                    // strip version from the tag.
                    while (tag.Length > 0 && (char.IsDigit(tag[0]) || tag[0] == '.'))
                    {
                        tag = tag.Substring(1);
                    }

                    resolvedFlavor = tag.StartsWith('-') ? tag.Substring(1) : tag;

                    return $"{registryName}:{version}{tag}";
                }
            }
        }

        // flavor is a short name
        resolvedFlavor = flavor;

        if (flavor.StartsWith("ubi")) // Red Hat image.
        {
            string versionNoDot = version.Replace(".", "");

            return isSdk ? $"registry.access.redhat.com/{DotNetVersionToRedHatBaseImage(version)}/dotnet-{versionNoDot}"
                            : $"registry.access.redhat.com/{DotNetVersionToRedHatBaseImage(version)}/dotnet-{versionNoDot}-runtime";

            static string DotNetVersionToRedHatBaseImage(string version) => version switch
            {
                _ => "ubi8"
            };
        }
        else // Microsoft image.
        {
            const string ChiseledSuffix = "-chiseled";
            if (flavor.EndsWith(ChiseledSuffix) && isSdk) // There are no chiseled SDK images.
            {
                flavor = flavor.Substring(0, flavor.Length - ChiseledSuffix.Length);
            }

            string registryName = isSdk ? "mcr.microsoft.com/dotnet/sdk" : "mcr.microsoft.com/dotnet/aspnet";

            // jammy-chiseled is preview for .NET 6.0.
            if (flavor == "jammy-chiseled" && version == "6.0")
            {
                registryName = "mcr.microsoft.com/dotnet/nightly/aspnet";
            }

            string tag = version;
            if (flavor.Length > 0)
            {
                tag += $"-{flavor}";
            }

            return $"{registryName}:{tag}";
        }
    }
}