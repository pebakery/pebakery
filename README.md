# PEBakery

![Logo](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/Logo.png)

PEBakery is a builder specialized in customizing Windows Preinstalled Envrionment.

| Branch    | Build Status   |
|-----------|----------------|
| Master    | [![CI Master Branch Build Status](https://ci.appveyor.com/api/projects/status/j3p0v26j7nky0bvu/branch/master?svg=true)](https://ci.appveyor.com/project/ied206/pebakery/branch/master)    |
| Develop   | [![CI Develop Branch Build Status](https://ci.appveyor.com/api/projects/status/j3p0v26j7nky0bvu/branch/develop?svg=true)](https://ci.appveyor.com/project/ied206/pebakery/branch/develop) |

PEBakery is compatible with WinBuilder 082.

## Main Goal

PEBakery's main goal is being able to build [Win10PESE](http://win10se.cwcodes.net/) and [MistyPE](http://mistyprojects.co.uk/documents/MistyPE/index.html) perfectly.

## Disclaimer

- All implementation is only backed by documentation and blackbox testing, without violating WinBuilder 082's EULA.
- Even though I did not experienced data corruption while developing, it is highly recommended to backup your files.
- I do not provide any warranty, use at your own risk. 

## License

PEBakery is mainly licensed under GPL Version 3.

Part of PEBakery is licensed under MIT License and others.

## Current Status

PEBakery is currently in beta stage.

### Command Status

|   Class   | All | Implemented | Tested |
|-----------|-----|-------------|--------|
| File      | 12  | 12          | 12     |
| Registry  | 8   | 8           | 4      |
| Text      | 5   | 5           | 5      |
| Ini       | 8   | 8           | 8      |
| Archive   | 4   | 4           | 4      |
| Network   | 2   | 2           | 2      |
| Plugin    | 4   | 4           | 0      |
| Interface | 9   | 9           | 2      |
| Hash      | 5   | 5           | 5      |
| String    | 33  | 33          | 31     |
| Math      | 22  | 22          | 22     |
| System    | 19  | 19          | 0      |
| Branch    | 26  | 26          | 15     |
| Control   | 9   | 9           | 7      |
| WIM       | 8   | 0           | 0      |
| All       | 172 | 166         | 117    |

## Help Needed

### Testing

PEBakery is in alpha stage, it needs a lot of testing.

- Try to build [Win10PESE](http://win10se.cwcodes.net/), [MistyPE](http://mistyprojects.co.uk/documents/MistyPE/index.html) with PEBakery and report bugs.
- If you are developer, you can contribute by writing unit tests.

### Documents

PEBakery needs to be documented, especially for plugin developers.

- Design of PEBakery
- Command Syntax and Usage

### UI

PEBakery needs more well-designed UI.

### Optimization

It would be better to have more faster, robust PEBakery.

### Proposal

Suggestions about which direction PEBakery should go.

- Add Useful Commands
- New Plugin Format
- New Language and Grammar

## Build

To compile PEBakery from source, Visual Studio and .Net Framework is required.

### Recommended

- Visual Studio 2017
- .Net Framework 4.7.1 Developer Pack

### Requirement

- C# 7 Compiler
- .Net Framework 4.7.1

## Documentation

See [PEBakery Manual](https://github.com/ied206/PEBakery/tree/docs/Manual).

## Screenshots

### Build 20171114

![Win10PESE by PEBakery 20171114](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery.png)
![Korean IME by PEBakery 20171114](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery-Korean_IME.png)

In WB082, same plugins are rendered like this:

![Win10PESE by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082.png)
![Korean IME by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082-Korean_IME.png)
