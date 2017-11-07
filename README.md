# PEBakery

WinBuilder drop-in replacement.

Master Build Status  
[![CI Master Branch Build Status](https://ci.appveyor.com/api/projects/status/j3p0v26j7nky0bvu/branch/master?svg=true)](https://ci.appveyor.com/project/ied206/pebakery/branch/master)

Develop Build Status  
[![CI Develop Branch Build Status](https://ci.appveyor.com/api/projects/status/j3p0v26j7nky0bvu/branch/develop?svg=true)](https://ci.appveyor.com/project/ied206/pebakery/branch/develop)
 
PEBakery is new, improved implementation of WinBuilder 082.

PEBakery aims to resolve WinBuilder 082's abandoned bugs, and to be more plugin developer friendly builder.

## Main Goal

PEBakery's main goal is being able to build [Win10PESE](http://win10se.cwcodes.net/) without error.

## Disclaimer

- All implementation is only backed by documentation and blackbox testing, without violating WinBuilder 082's EULA.
- PEBakery is not mature software, it can corrupt your system. I do not provide any warranty, use at your own risk.
- Please backup your files before running PEBakery, or consider isolating with VM.

## License

PEBakery is mainly licensed under GPL Version 3.

Part of PEBakery is licensed under MIT License and others.

## Build

To compile PEBakery from source, Visual Studio and .Net Framework is required.

## Recommended

- Visual Studio 2017
- .Net Framework 4.7 Developer Pack

## Requirement

- C# 7 Compiler
- .Net Framework 4.7

## Current State

PEBakery can load Projects, and able to run simple plugin correctly.

However, building whole project is quite buggy and needs to be improved.

### Implemented

- Plugin Interface Parser/Renderer
- Code Parser
- Variables System
- Logger System
- Macro System
- Commands System

### Working

- GUI
- Unit Tests
- Fixing bugs, bugs and bugs

### Command Status

|   Class   | All | Implemented | Tested |
|-----------|-----|-------------|--------|
| File      | 11  | 11          | 2      |
| Registry  | 8   | 8           | 0      |
| Text      | 5   | 5           | 0      |
| INI       | 7   | 7           | 0      |
| Archive   | 4   | 4           | 4      |
| Network   | 2   | 2           | 2      |
| Plugin    | 4   | 4           | 0      |
| Interface | 6   | 6           | 0      |
| Hash      | 5   | 5           | 5      |
| String    | 32  | 32          | 30     |
| Math      | 22  | 22          | 22     |
| System    | 15  | 15          | 0      |
| Branch    | 31  | 31          | 0      |
| Control   | 6   | 6           | 6      |
| All       | 158 | 158         | 71     |

## Help Needed

PEBakery is waiting your contributions!

### Testing

PEBakery now have its skeleton finished, but has lots of bugs.

- Try to build [Win10PESE](http://win10se.cwcodes.net/), with PEBakery and report bugs.
- If you are developer, you can contribute by writing unit tests.

### Undocumented WB082 behaviors

WB082 has lots of undocumented behaviors, and it takes time to inspect all of them. As a result, many commands behave different from WB082.

### UI

PEBakery needs more well-designed UI.

### Documentation

PEBakery needs to be documented, especially for plugin developers.

- Design of PEBakery
- Command Grammar and Usage

### Optimization

It would be better to have more faster, robust PEBakery.

### Proposal

Plan PEBakery's future with your own hands!

- Add Useful Commands
- New Plugin Format
- New Language and Grammar

## Screenshots

### 20170530 Build

![Win10PESE by PEBakery 20170530](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery.png)
![Korean IME by PEBakery 20170530](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery-Korean_IME.png)

In WB082, same plugins are rendered like this:  
![Win10PESE by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082.png)
![Korean IME by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082-Korean_IME.png)
