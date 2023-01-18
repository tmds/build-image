# dotnet build-image

A .NET global tool to create container images from .NET projects, because _life is too short to write Dockerfiles_.

- The application image is built using a multi-stage build: first the application is published in an SDK image, and then the result is copied into a runtime image.
- Images are chosen that support the .NET version targetted in the project (`TargetFramework`).
- If a `global.json` file is present, it is used to determine the SDK image version.
- The image base OS can be chosen.
- Both `podman` and `docker` are supported to build the image.
- Caches NuGet packages across builds.
- The target architecture can be chosen.
- The application image runs as a non-root user.
- Supports the same properties as the SDK's built-in support to build images (https://github.com/dotnet/sdk-container-builds).

# Usage

Install the tool:

```
$ dotnet tool install -g dotnet-build-image
```

Create an app:
```
$ dotnet new web -o web
$ cd web
```

Build an image:
```
$ dotnet build-image -t web:latest
Building image 'web:latest' from project '/tmp/web/web.csproj'.
[1/2] STEP 1/13: FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
...
[2/2] STEP 9/9: ENTRYPOINT ["dotnet", "/app/web.dll"]
[2/2] COMMIT web:latest
--> 97a42d41790
Successfully tagged localhost/web:latest
97a42d41790c7d926f0152f5a86dbdbd0b5c4b6592423f6af97b7cd72c57324b
```

Run the image:
```
$ podman run -p 8080:8080 web
```

# Options

```
Description:
  Build a container image from a .NET project.

Usage:
  build-image [<PROJECT>] [options]

Arguments:
  <PROJECT>  .NET project to build [default: .]

Options:
  -b, --base <base>    Flavor of the base image
  -t, --tag <tag>      Name for the built image [default: dotnet-app]
  -a, --arch <arch>    Target architecture ('x64'/'arm64'/'s390x'/'ppc64le')
                       The base image needs to support the selected architecture
  --push               After the build, push the image to the repository
  --portable           Avoid using features that make the Containerfile not portable
  --context <context>  Context directory for the build [default: .]
  --as-file <as-file>  Generates a Containerfile with the specified name
  --print              Print the Containerfile
  --version            Show version information
  -?, -h, --help       Show help and usage information
```

The `--base` option can be used to select the .NET base image.
When not specified, Microsoft images from `mcr.microsoft.com` are used.
When set to `ubi`, Red Hat images from `registry.access.redhat.com` are used.
When set to another value, like `alpine`, the corresponding Microsoft images are used.
You can also use the full image repository names, like `mcr.microsoft.com/dotnet/runtime:6.0-alpine`.

The options can also be specified in the .NET project file.
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    ...
    <ContainerImageName>myapp</ContainerImageName>
    <ContainerImageTag>latest</ContainerImageTag>
    <ContainerBaseImage>ubi</ContainerBaseImage>
  </PropertyGroup>

</Project>
```

The following properties are supported: `ContainerImageTag`, `ContainerImageTags`, `ContainerImageName`, `ContainerRegistry`, `ContainerBaseImage`, `ContainerWorkingDirectory`, `ContainerImageArchitecture`, `ContainerSdkImage`, `ContainerEnvironmentVariable`, `ContainerLabel`, `ContainerPort`.

# Using the CI build

```
dotnet tool install -g dotnet-build-image --prerelease --add-source https://www.myget.org/F/tmds/api/v3/index.json
```