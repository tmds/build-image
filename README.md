# dotnet build-image

A .NET global tool to create container images from .NET projects, because _life is too short to write Dockerfiles_.

- The application image is built using a multi-stage build: first the application is published in one image, and then the result is copied into a runtime image.
- Images are chosen that support the .NET version targetted in the project (`TargetFramework`).
- The image base OS can be chosen.
- Both `podman` and `docker` are supported to build the image.
- Caches NuGet packages across builds.

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
$ dotnet build-image
[1/2] STEP 1/5: FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
[1/2] STEP 2/5: WORKDIR /app
--> Using cache 19c7be895c6c22f77f807ed3bb15f5f5b914899e9bbc76de8041bf68e391d4db
--> 19c7be895c6
[1/2] STEP 3/5: COPY . ./
--> 619d48ea256
[1/2] STEP 4/5: RUN dotnet restore .
  Determining projects to restore...
  Restored /app/web.csproj (in 158 ms).
--> 92b7017e3f7
[1/2] STEP 5/5: RUN dotnet publish --no-restore -c Release -o /app/out .
Microsoft (R) Build Engine version 17.1.0+ae57d105c for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  web -> /app/bin/Release/net6.0/web.dll
  web -> /app/out/
--> 08310738cee
[2/2] STEP 1/5: FROM mcr.microsoft.com/dotnet/aspnet:6.0
[2/2] STEP 2/5: WORKDIR /app
--> Using cache 9cb5e9baedc4fea3023f8f3d9874dfaac8580ab898fdcf5e21848291b640d85e
--> 9cb5e9baedc
[2/2] STEP 3/5: ENV ASPNETCORE_URLS=http://*:8080
--> 4fbf1b4d0be
[2/2] STEP 4/5: COPY --from=build-env /app/out .
--> bfaa4823a77
[2/2] STEP 5/5: CMD ["dotnet", "web.dll"]
[2/2] COMMIT dotnet-app
--> 7d160587c6b
Successfully tagged localhost/dotnet-app:latest
7d160587c6b0489b94eed5091c4561d77d214c01b114562a0f903d5294481732
```

Run the image:
```
$ podman run -p 8080:8080 dotnet-app
```

# Options

```
$ dotnet build-image --help
Description:
  Build a container image from a .NET project.

Usage:
  build-image [<PROJECT>] [options]

Arguments:
  <PROJECT>  .NET project to build [default: .]

Options:
  -b, --base <base>                Flavor of the base image
  -t, --tag <tag>                  Name for the built image [default: dotnet-app]
  --push                           After the build, push the image to the repository
  --as-dockerfile <as-dockerfile>  Generates a Dockerfile with the specified name
  --version                        Show version information
  -?, -h, --help                   Show help and usage information
```

The `--base` option can be used to select the .NET base image.
When not specified, Microsoft images from `mcr.microsoft.com` are used.
When set to `ubi`, Red Hat images from `registry.access.redhat.com` are used.
When set to another value, like `alpine`, the corresponding Microsoft images are used.

The options can also be specified in the .NET project file.
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    ...
    <ImageTag>myapp</ImageTag>
    <ImageBase>ubi</ImageBase>
  </PropertyGroup>

</Project>
```
