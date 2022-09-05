class FlavorInfo
{
    public string Flavor { get; init; } = null!;
    public string RuntimeImage { get; init; } = null!;
    public string SdkImage { get; init; } = null!;
}

class ImageFlavorDatabase
{
    public static FlavorInfo GetFlavorInfo(string flavor, string runtimeVersion, string sdkVersion)
    {
        string fromImage, buildImage;
        if (flavor.StartsWith("ubi"))
        {
            string runtimeVersionNoDot = runtimeVersion.Replace(".", "");
            string sdkVersionNoDot = sdkVersion.Replace(".", "");
            string baseOs = flavor;
            if (baseOs == "ubi")
            {
                baseOs = "ubi8"; // TODO: switch based on dotnetVersion
            }
            fromImage = $"registry.access.redhat.com/{baseOs}/dotnet-{runtimeVersionNoDot}-runtime";
            buildImage = $"registry.access.redhat.com/{baseOs}/dotnet-{sdkVersionNoDot}";
        }
        else
        {
            string imageTag = runtimeVersion;
            string sdkImageTag = sdkVersion;
            if (!string.IsNullOrEmpty(flavor))
            {
                imageTag += $"-{flavor}";
                sdkImageTag += $"-{flavor}";

                const string ChiseledSuffix = "-chiseled";
                if (sdkImageTag.EndsWith(ChiseledSuffix))
                {
                    // There are no chiseled SDK images.
                    sdkImageTag = sdkImageTag.Substring(0, sdkImageTag.Length - ChiseledSuffix.Length);
                }
            }
            bool isPreview = flavor == "jammy-chiseled" && runtimeVersion == "6.0";
            string fromRegistry = isPreview ? "mcr.microsoft.com/dotnet/nightly/aspnet" : "mcr.microsoft.com/dotnet/aspnet";
            fromImage = $"{fromRegistry}:{imageTag}"; 
            buildImage = $"mcr.microsoft.com/dotnet/sdk:{sdkImageTag}";
        }

        return new FlavorInfo()
        {
            Flavor = flavor,
            RuntimeImage = fromImage,
            SdkImage = buildImage
        };
    }
}