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
$ dotnet tool install -g --add-source https://www.myget.org/F/tmds --version '*-*' Tmds.BuildImage
```

Create an app:
```
$ dotnet new console -o console
$ cd console
```

Build an image:
```
$ dotnet build-image
Building image 'dotnet-app' from project /tmp/repos/console.
[1/2] STEP 1/5: FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
[1/2] STEP 2/5: WORKDIR /app
--> Using cache 19c7be895c6c22f77f807ed3bb15f5f5b914899e9bbc76de8041bf68e391d4db
--> 19c7be895c6
[1/2] STEP 3/5: COPY . ./
--> baf4041d900
[1/2] STEP 4/5: RUN dotnet restore .
  Determining projects to restore...
  Restored /app/console.csproj (in 136 ms).
--> e3c2f3e15c8
[1/2] STEP 5/5: RUN dotnet publish -c Release -o /app/out .
Microsoft (R) Build Engine version 17.1.0+ae57d105c for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  All projects are up-to-date for restore.
  console -> /app/bin/Release/net6.0/console.dll
  console -> /app/out/
--> 5a95d5eb0dd
[2/2] STEP 1/4: FROM mcr.microsoft.com/dotnet/aspnet:6.0
[2/2] STEP 2/4: WORKDIR /app
--> Using cache 9cb5e9baedc4fea3023f8f3d9874dfaac8580ab898fdcf5e21848291b640d85e
--> 9cb5e9baedc
[2/2] STEP 3/4: COPY --from=build-env /app/out .
--> f56daa1108f
[2/2] STEP 4/4: CMD ["dotnet", "console.dll"]
[2/2] COMMIT dotnet-app
--> 925c61e439e
Successfully tagged localhost/dotnet-app:latest
925c61e439e6269b92d6abec23956085f3d4fe170bfd70aa98597ee922c2a29d
```

Run the image:
```
$ podman run dotnet-app
Hello, World!
```

# Options

```
$ dotnet build-image --help
Description:
  Build an image from a .NET project.

Usage:
  build-image [options]

Options:
  --os <os>
  --tag <tag>                      [default: dotnet-app]
  --as-dockerfile <as-dockerfile>
  --project <project>              [default: .]
  --version                        Show version information
  -?, -h, --help                   Show help and usage information
```

The `--os` option can be used to select the .NET base image.
When not specified, Microsoft images from `mcr.microsoft.com` are used.
When set to `ubi`, Red Hat images from `registry.access.redhat.com` are used.
When set to another value, like `alpine`, the corresponding Microsoft images are used.
