# PEBakery
WinBuilder drop-in replacement. Stil in development.

Master Build Status  
[![CI Master Branch Build Status](https://ci.appveyor.com/api/projects/status/j3p0v26j7nky0bvu/branch/master?svg=true)](https://ci.appveyor.com/project/ied206/pebakery/branch/master)

Develop Build Status  
[![CI Develop Branch Build Status](https://ci.appveyor.com/api/projects/status/j3p0v26j7nky0bvu/branch/develop?svg=true)](https://ci.appveyor.com/project/ied206/pebakery/branch/develop)
 
## What is PEBakery?
PEBakery is new, improved implementation of WinBuilder 082.

PEBakery aims to resolve WinBuilder 082's abandoned bugs, more plugin developer friendly builder.

## Disclaimer
All implementation is only backed by documentation and blackbox testing, without violating WinBuilder 082's EULA.

## License
PEBakery is mainly licensed under GPL Version 3.
Part of PEBakery is licensed under MIT License and others.

# Current State
## Implemented
- Plugin Interface Parser/Renderer
- Variables System
- Logger System
- Macro System

## Working
- GUI
- Code Parser
- PEBakery Engine

## Need Help
If you have any ideas, please let me know at [Issue Tracker](https://github.com/ied206/PEBakery/issues).
- New Plugin Format
- New Language and Grammar

## Command Status
Command 78% Implemented

|   Class   | All | Implemented |
|-----------|-----|-------------|
| File      | 11  | 6   |
| Registry  | 10  | 0   |
| Text      | 5   | 5   |
| INI       | 7   | 6   |
| Compress  | 4   | 0   |
| Network   | 2   | 2   |
| Plugin    | 4   | 4   |
| Interface | 5   | 5   |
| Hash      | 5   | 5   |
| String    | 27  | 27  |
| Math      | 9   | 0   |
| System    | 15  | 15  |
| Branch    | 31  | 29  |
| Control   | 8   | 8   |
| All       | 144 | 83  |

# Screenshots
## 20170530 Build
![Win10PESE by PEBakery 20170530](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery.png)
![Korean IME by PEBakery 20170530](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery-Korean_IME.png)

In WB082, same plugins are rendered like this:
![Win10PESE by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082.png)
![Korean IME by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082-Korean_IME.png)

