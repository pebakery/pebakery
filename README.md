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
- Lastest Nightly (develop)
    - [Standalone Nightly (x64)](https://ci.appveyor.com/api/projects/ied206/PEBakery/artifacts/Publish/PEBakery-nightly-sc.7z?branch=develop)
      - No dependency
      - Sizes about 110MB
      - Built for x64 architecture
    - **[Runtime Dependent Nightly (x64, x86)](https://ci.appveyor.com/api/projects/ied206/PEBakery/artifacts/Publish/PEBakery-nightly-fxdep.7z?branch=develop)**
      - Requires [.NET Core 3.1 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1)
      - Sizes about 32MB
      - Supports both x86, x64 architecture

**CAUTION**: Do not forget to set the proper compatibility options for your projects. We have prepared a special [Migrating from Winbuilder](https://github.com/pebakery/pebakery-docs/blob/master/CodingGuide/Migrating.md) guide, so you know what script breaking changes to expect and when compatibility options need to be enabled.

## Prerequisites

### Beta 6 Release

PEBakery beta 6 runs on .NET Framework 4.7.2.

If you are using Windows 10 v1803 or later, no action is necessary. If not, please install **[.NET Framework 4.7.2](http://go.microsoft.com/fwlink/?LinkId=863262)**.

### Nightly Builds

Starting from beta 7, PEBakery runs on .NET Core 3.1. 

- *Standalone Nightly* do not require any runtime installed, but runs only on **x64** Windows.
- *Runtime Dependent Nightly* runs on both **x64** and **x86** Windows, but requires **[.NET Core 3.1 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1)** to be installed.

## License

PEBakery is primarily licensed under GPLv3 or any later version with additional permission.

Some parts of PEBakery are licensed under the MIT License and other licenses.

Please read [LICENSE](./LICENSE) for details.

## Documentation

Please read the [Official PEBakery Manual](https://github.com/pebakery/pebakery-docs).

Testers using nightly builds should refer to the developer branch [Official PEBakery Manual (develop)](https://github.com/pebakery/pebakery-docs/tree/develop).

## Progress and TODO

See our [Roadmap](https://github.com/pebakery/pebakery/projects/2).

## Build

.NET Core SDK, Windows SDK, and MSVC are required to compile PEBakery from the source.

### Requirement

- [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) to build and test `PEBakery.exe`.
- [Windows 10 SDK](https://developer.microsoft.com/ko-kr/windows/downloads/windows-10-sdk) to build the `PEBakeryLauncher.exe`
    - Requires [Microsoft C++ Build Tools 2019](https://visualstudio.microsoft.com/visual-cpp-build-tools/)

We recommend using full-featured Visual Studio for your best development experience.

- [Visual Studio 2019](https://visualstudio.microsoft.com/vs/)

### Compiling

To compile PEBakery, run `BinaryPublish.ps1` with PowerShell or Powershell Core. 

- Publish\PEBakery-release-fxdep : Runtime-depedent binary
- Publish\PEBakery-release-sc : Standalone binary

### Testing

To run a PEBakery with a project ([Win10XPE](https://github.com/ChrisRfr/Win10XPE), [ChrisPE](https://github.com/pebakery/chrispe), etc.), follow one of the given instructions.

1. (Simple) Copy `PEBakeryLauncher.exe` and `Binary` alongside `Projects` directory.
2. (Advanced) Launch `PEBakeryLauncher.exe` or `PEBakery.exe` with `/baseDir` parameter.
    ```powershell
    # <ProjectPath> is a path of the root directory that contains "Projects".
    $ .\PEBakeryLauncher.exe /baseDir <ProjectPath>
    # or
    $ .\Binary\PEBakery.exe /baseDir <ProjectPath>
    ```

## Screenshots

### PEBakery Beta 6

![Win10XPE with PEBakery Beta 6](./Image/PEBakery-Win10XPE.png)

![ChrisPE with PEBakery Beta 6](./Image/PEBakery-ChrisPE.png)
