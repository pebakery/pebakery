# PEBakery
WinBuilder drop-in replacement. Stil in development.

## What is PEBakery?
PEBakery is new, improved implementation of WinBuilder 082's script engine.

PEBakery aims to resolve WinBuilder 082's abandoned bugs, provide improved support for Unicode, more plugin developer friendly builder.

## Disclaimer
All implementation is only backed by documentation and blackbox testing, without violating WinBuilder 082's EULA.

# Current State
## Implemented
- GUI in WPF
- Parsing of Plugins
- Plugin Interface Rendering

## Roadmap
- Interface Command Grammar Analysis
- Refactor of Logger System
- Refactor of Variables System
- Full Refactor of PEBakery Engine
- Macro System
- Refine of GUI

# Screenshots
## 20170326 Build
![Win10PESE by PEBakery 20170326](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery.png)
![Korean IME by PEBakery 20170326](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery-Korean_IME.png)

In WB082, same plugins are rendered like this:
![Win10PESE by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082.png)
![Korean IME by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082-Korean_IME.png)


# License
Core of PEBakery is licensed under GPL.
Portions of PEBakery is licensed under MIT License and Apache License 2.0.

## State of PEBakery-Legacy
### Implemented
- Plugin Code Parser
- Project Recognition
- Logger
- Variables

### Working
- Commands
- TestSuite
- Macro (known as API in WinBuilder 082)

### Command Status
|   Class  | Implemented | All |
|----------|-------------|-----|
| File     | 13 | 14  |
| Registry | 0  | 10  |
| Text     | 3  | 12  |
| Plugin   | 0  | 6   |
| UI       | 0  | 11  |
| String   | 0  | 24  |
| System   | 14 | 16  |
| Branch   | 27 | 32  |
| Control  | 2  | 8   |
| All      | 60 | 132 |
