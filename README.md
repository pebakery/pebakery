# PEBakery

<div style="text-align: left">
    <img src="./Image/Banner.svg" height="140">
</div>

PEBakery is a script engine that specializes in customizing the Windows Preinstalled Environment (WinPE/WinRE).

| Branch    | Build Status   |
|-----------|----------------|
| Master    | [![CI Master Branch Build Status](https://ci.appveyor.com/api/projects/status/j3p0v26j7nky0bvu/branch/master?svg=true)](https://ci.appveyor.com/project/ied206/pebakery/branch/master) |
| Develop   | [![CI Develop Branch Build Status](https://ci.appveyor.com/api/projects/status/j3p0v26j7nky0bvu/branch/develop?svg=true)](https://ci.appveyor.com/project/ied206/pebakery/branch/develop) |

PEBakery is backward compatible with WinBuilder 082 and makes significant improvements upon it.

## Disclaimer

- All implementation is only backed by documentation and black box testing, without violating WinBuilder 082's EULA.
- The developers do not provide any warranty, use at your own risk. Backup is highly recommended.
- Windows Preinstalled Environment is a registered trademark of Microsoft.

## Download

The official release version is recommended for general use.

A nightly build is provided for testing purposes. 

- [Official Release](https://github.com/pebakery/pebakery/releases)
- [Lastest Nightly (develop)](https://ci.appveyor.com/project/ied206/pebakery/build/artifacts?branch=develop)
    - [Standalone Nightly (x64)](https://ci.appveyor.com/api/projects/ied206/PEBakery/artifacts/Publish/PEBakery-nightly-sc.7z?branch=develop)
      - No dependency
      - Sizes about 110MB
      - Built for x64 architecture
    - **[Runtime-dependent Nightly (x64, x86)](https://ci.appveyor.com/api/projects/ied206/PEBakery/artifacts/Publish/PEBakery-nightly-rt.7z?branch=develop)**
      - Requires [.NET Core 3.1 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1)
      - Sizes about 32MB
      - Supports both x86, x64 architecture

**CAUTION**: Do not forget to set the proper compatibility options for your projects. We have prepared a special [Migrating from Winbuilder](https://github.com/pebakery/pebakery-docs/blob/master/CodingGuide/Migrating.md) guide, so you know what script breaking changes to expect and when compatibility options need to be enabled.

## Prerequisites

### Beta 6 Release

PEBakery beta 6 runs on .NET Framework 4.7.2.

If you are using Windows 10 v1803 or later, no action is necessary. If not, please install **[.NET Framework 4.7.2](http://go.microsoft.com/fwlink/?LinkId=863262)**.

### Nightly Builds

Starting from beta 7, PEBakery runs on .NET 6. 

- *Standalone Nightly* do not require any runtime installed, but runs only on **x64** Windows.
- *Runtime Dependent Nightly* runs on both **x64**, **x86** and **ARm64** Windows, but requires **[.NET Core 6 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0/runtime)** to be installed.

## License

PEBakery is primarily licensed under GPLv3 or any later version with additional permission.

Some parts of PEBakery are licensed under the MIT License and other licenses.

Please read [LICENSE](./LICENSE) for details.

## Documentation

Please read the [Official PEBakery Manual](https://github.com/pebakery/pebakery-docs).

Testers using nightly builds should refer to the developer branch [Official PEBakery Manual (develop)](https://github.com/pebakery/pebakery-docs/tree/develop).

## Progress and TODO

See our [Roadmap](https://github.com/pebakery/pebakery/projects/2).

## Build Instructions

.NET Core SDK, Windows SDK, and MSVC are required to compile PEBakery from the source.

### Requirement

- [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) to build and test `PEBakery.exe`.
- [Windows 10 SDK](https://developer.microsoft.com/ko-kr/windows/downloads/windows-10-sdk) to build `PEBakeryLauncher.exe`
    - Requires [Microsoft C++ Build Tools 2019](https://visualstudio.microsoft.com/visual-cpp-build-tools/) or later

If you are a contributor, we recommend using a full-featured Visual Studio for the best development experience.

- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)

### Compiling

Nightly binaries can be compiled by running `BinaryPublish.ps1 -nightly` on PowerShell.

- `Publish\PEBakery-nightly-rt` : Runtime-dependent binary
- `Publish\PEBakery-nightly-sc` : Standalone binary

### Testing

To run a PEBakery with a project ([Win10XPE](https://github.com/ChrisRfr/Win10XPE), [ChrisPE](https://github.com/pebakery/chrispe), etc.), follow one of the given instructions.

**NOTE**: `<ProjectPath>` is the directory that contains `Projects` directory from the PE building project.

1. (Simple) Copy `PEBakeryLauncher.exe` and `Binary` inside `<ProjectPath>`, and run `PEBakeryLauncher.exe`.
2. (Advanced) Launch `PEBakeryLauncher.exe` or `PEBakery.exe` with `/baseDir` parameter.
    ```powershell
    $ .\PEBakeryLauncher.exe /baseDir <ProjectPath>
    # or
    $ .\Binary\PEBakery.exe /baseDir <ProjectPath>
    ```

## Screenshots

### PEBakery Beta 6

![Win10XPE with PEBakery Beta 6](./Image/PEBakery-Win10XPE.png)

![ChrisPE with PEBakery Beta 6](./Image/PEBakery-ChrisPE.png)
