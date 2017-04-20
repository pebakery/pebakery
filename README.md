# PEBakery
WinBuilder drop-in replacement. Stil in development.

## What is PEBakery?
PEBakery is new, improved implementation of WinBuilder 082.

PEBakery aims to resolve WinBuilder 082's abandoned bugs, provide improved support for Unicode, more plugin developer friendly builder.

## Disclaimer
All implementation is only backed by documentation and blackbox testing, without violating WinBuilder 082's EULA.

# Current State
## Implemented
- Plugin Interface Parser/Renderer
- Logger
- Variables
- Macro

## Working
- GUI
- WB082 Syntax Parser
- PEBakery Engine

## TODO
- New Plugin Format
- New Language Grammar

## Command Status
|   Class  | Implemented | All |
|----------|-------------|-----|
| File     | 0  | 13  |
| Registry | 0  | 10  |
| Text     | 10 | 12  |
| Plugin   | 0  | 6   |
| UI       | 2  | 11  |
| String   | 0  | 24  |
| System   | 0  | 16  |
| Branch   | 30 | 31  |
| Control  | 3  | 8   |
| All      | 45 | 131 |

# Screenshots
## 20170421 Build
![Win10PESE by PEBakery 20170326](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery.png)
![Korean IME by PEBakery 20170326](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/PEBakery-Korean_IME.png)

In WB082, same plugins are rendered like this:
![Win10PESE by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082.png)
![Korean IME by WinBuilder 082](https://raw.githubusercontent.com/ied206/PEBakery/master/Image/WB082-Korean_IME.png)

