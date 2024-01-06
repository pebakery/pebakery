# ChangeLog

## v1.x

### v1.1.0

Released on 2022-12-25

- \[ADD\] Implement the `Math,ToChar` and `Math,FromChar` command
- \[ADD\] Implement modern loop commands (`While`, `ForEach`, `ForRange`, `Continue`, `Break`)
- \[ADD\] Implement modern loop helper commands (`List,Range`)
- \[ADD\] Position and size of MainWindow are now persisted
- \[ADD\] Enhanced `PathBox`, `ComboBox` checking in SyntaxChecker 
- \[ADD\] SyntaxChecker now identifies types of sections better
- \[FIX\] CodeOptimizer now performs data flow analysis to avoid broken optimization
- \[FIX\] Most MessageBoxes are now displayed over the PEBakery window
- \[FIX\] Section parameters are no longer contaminated at the end of the code block
- \[FIX\] Fixed invalid `WebGet` parameter parsing 
- \[FIX\] Launcher now checks the installed .NET Runtime is compatible even on the minor version level
- \[FIX\] Enhanced script progress tracking
- \[FIX\] Fix race condition happens in `SectionToRun` build triggered from UIControls

### v1.0.0

Released on 2022-05-19

First stable PEBakery release.

- \[ADD\] New compat option to automatically compact ini files after `IniWrite`
- \[ADD\] Colored row in LogViewer
- \[ADD\] Better text encoding detection
- \[ADD\] Supporting UTF8 without BOM encoding on text files
- \[ADD\] Suppor `Encoding=` optional parameter on `FileCreateBlank` command
- \[ADD\] Support `DefaultValue=` optional parameter on `IniRead` command
- \[ADD\] Support `UserAgent=` optional parameter on `WebGet` command
- \[ADD\] Support `Filter=` optional parameter on `FileBox` control
- \[ADD\] Add `PathBox` control, a better `FileBox`
- \[ADD\] Official support for Windows ARM64
- \[CHANGE\] Migrated to .NET 6
- \[CHANGE\] Better EncodedFile footer handling
- \[CHANGE\] Migrated HTML template engine to [Scriban](https://github.com/scriban/scriban) from Razor
- \[CHANGE\] ShellExecute console output appears only there is something to disaply
- \[CHANGE\] Improved `WimMount`/`WimUnmount` command progress report
- \[CHANGE\] User preferences on LogViewer and LogExportWindow are persistently remebered
- \[FIX\] More stable and improved Interface Editor
- \[FIX\] Fix section searching on cached script to work properly
- \[FIX\] `FileCopy` no longer truncates filename on the path ends with `\`
- \[FIX\] Fix broken `Exit` command
- \[FIX\] Expand variables on macro arguments
- \[FIX\] Fix rare bug on `ShellExecute` command to launch .exe files

## Prerelease beta

### v0.9.6 beta6

Released on 2019-11-01

1. PEBakery now requires .Net Framework 4.7.2, to solve the dependency issue. 
    - Please install [.Net Framework 4.7.2](http://go.microsoft.com/fwlink/?linkid=863262) if you are not using the latest Windows 10.
2. PEBakery now applies compatibility options as per-project starting from beta 6.
    - Please follow the following instructions.

- \[ADD\] Better-designed default theme presets
- \[ADD\] Customizable theme support
- \[ADD\] Improve realtime elapsed build time report 
- \[ADD\] Syntax checker also checks script interface
- \[ADD\] Faster loading performance of cached dir-linked scripts
- \[ADD\] Render white pixels of BMP image as transparency in `Button` controls
- \[ADD\] Support 7z format in the `Compress` command
- \[ADD\] Add `Math,Dec` as a counterpart of `Math,Hex`
- \[ADD\] Add `Math,Rand`
- \[ADD\] Support RTF format in `TextFile`control
- \[ADD\] Script attachment manager now reports file attachment progress
- \[ADD\] Support drag-n-drop in script interface editor
- \[ADD\] Support multi-interface in script interface editor
- \[ADD\] Better support for Windows 7 classic theme
- \[ADD\] Better support for low-resolution device
- \[ADD\] Introduced filtering of saved logs
- \[ADD\] An origin script of command are logged in build logs
- \[ADD\] Context menu for collapsing & expanding script tree
- \[ADD\] Allow chaining of `If ~ Else`
- \[ADD\] Added `IniCompact` command
- \[ADD\] `Compress` command creates ZIP files with multiple threads
- \[ADD\] Multi-threaded LZMA2 compression for file attachment
- \[ADD\] Initial implementation of the script updater (WIP, may change in the future!)
- \[ADD\] Enable force stop of sub-processes by user request
- \[ADD\] Support non-standard registry subkey type  in the new `RegWriteEx` command
- \[CHANGE\] Targets .NET Framework 4.7.2
- \[CHANGE\] Some `Math` commands now requires `BitSize`
- \[CHANGE\] Optimized encoded file handling
- \[CHANGE\] More commands report per-command progress
- \[CHANGE\] `Math,Hex` produces `0x` prefix 
- \[CHANGE\] `EchoFile` deprecated and removed the `ENCODE` flag
- \[CHANGE\] Script logo is now displayed with exact pixel-size
- \[CHANGE\] Improved `If` command parsing
- \[CHANGE\] Log export files are given default filenames
- \[CHANGE\] Report command result in `#r` in `ShellExecute` and `WebGet`.
- \[CHANGE\] Reworked layout of Script attachment manager
- \[CHANGE\] `StrFormat,SubStr` was removed in favor of `StrFormat,Mid`
- \[CHANGE\] `TXTDelSpaces` now also trims trailing whitespace
- \[CHANGE\] More accurate build progress report
- \[CHANGE\] `GetParam` is no longer considered as a deprecated command
- \[CHANGE\] `Math,Bool` now allows C-style integer boolean 
- \[FIX\] Fixed many possible memory leaks
- \[FIX\] A lot of code cleanup for stability
- \[FIX\] `Set,<Var>,PERMANENT` now handles cached scripts properly
- \[FIX\] `FileCopy` command creates a warning log instead of an error when no source files were found
- \[FIX\] `CopyOrExpand` produces a warning log instead of an error when a wildcard was used
- \[FIX\] Better script tree sorting 
- \[FIX\] Improved stability of deferred logging
- \[FIX\] Deleting script interface control also deletes associated attached file
- \[FIX\] Fix `If ~ Else` block parsing bug
- \[FIX\] `TextBox` control now properly handles BOM bytes
- \[FIX\] Better handling of destination paths in commands
- \[FIX\] Proper escaping of strings in interface controls 
- \[FIX\] Better handling of `System,SetLocal` and `System.EndLocal`
- \[FIX\] Scan for .link files from linked directories
- \[FIX\] `Halt` command no longer opens a MessageBox
- \[FIX\] `WriteInterface` command updates interface variables
- \[FIX\] Always auto-redirect HTTP 302 responses in `WebGet`
- \[FIX\] `AddVariables` now properly processes `GLOBAL` flag 
- \[FIX\] Support `REG_NONE` in the registry commands


### v0.9.5.1 beta5 (bugfix 1)

Released on 2018-09-08

This release fixes several issues of beta 5.

Starting with beta 5, all compatibility options are turned off by default.
You should set appropriate compatibility options to build legacy projects successfully.

- \[FIX\] Correct status bar text after a refreshing script
- \[FIX\] Saving settings no longer crashes when no projects are loaded

### v0.9.5 beta5

Released on 2018-09-05

Starting with beta 5, all compatibility options are turned off by default.
You should set appropriate compatibility options to build legacy projects successfully.

- \[ADD\] `List` commands added
- \[ADD\] `StrFormat,Left` and `StrFormat,Right` added
- \[ADD\] Full deferred logging for interface build
- \[ADD\] Command's real position is shown in warning and error logs 
- \[ADD\] Filtering comments and macros are supported in LogWindow
- \[ADD\] Section out parameter support (e.g. `#o1`, `#o2`), ...
- \[ADD\] `RunEx`, `LoopEx` and `LoopLetterEx` added to support section out parameter
- \[ADD\] Compatibility options for turning off extended section parameter (`#r`, `#a`, `#o1`)
- \[ADD\] Compatibility options are turned off by default
- \[CHANGE\] `IniReadSection` redesigned
- \[CHANGE\] Allow short terms in `Message`
- \[CHANGE\] Interface value of `TextLabel` is also saved to variables when running a script
- \[FIX\] `UserInput,Dir` no longer crashes
- \[FIX\] `#r`, `#a` is matched case-insensitively
- \[FIX\] `SaveLog` produces a proper log when deferred logging is set
- \[FIX\] Scripts are ordered like the Windows File Explorer
- \[FIX\] Proper refresh of MainScript 
- \[FIX\] Script editor correctly reflects any changes made by a user
- \[FIX\] Prevent crash from a race condition in `ShellExecute`
- \[FIX\] Fix rare crash when opening script source
- \[FIX\] Several regressions affected build of Win10PESE and Win10XPE are fixed

### v0.9.4 beta4

Released on 2018-06-03

- \[ADD\] Script header/logo/interface/attachment editor
- \[ADD\] Runtime optimization of `WimPath*`, `WimExtract`, `ReadInterface`, `WriteInterface`
- \[ADD\] Compression method can be set when using `Encode` (None, Deflate, LZMA2)
- \[ADD\] Error in `WimExtractBulk` can be suppressed by using NOERR flag
- \[ADD\] Optimized memory usage when attaching/extracting files from script
- \[ADD\] `WebGet` saves HTTP status code into %StatusCode%
- \[ADD\] Implemented `RegCopy`
- \[ADD\] Support more properties in `ReadInterface` and `WriteInterface`
- \[CHANGE\] Treat %SourceDir% , %ISOFile%, %TargetDir% as global variables
- \[CHANGE\] Fixed variables are no longer overridable
- \[CHANGE\] Deprecate environment variables
- \[FIX\] Directory link (folder.project) support
- \[FIX\] Report error when WimExtract cannot find single file
- \[FIX\] Nested `System,SetLocal` support 
- \[FIX\] Proper implementation of `System,ErrorOff`
- \[FIX\] Web browser is no longer launched as Adminstrator in WebLabel control
- \[FIX\] Display caption of Bevel control by default
- \[FIX\] Logging is enabled by default in interface button
- \[FIX\] Use default encoding of console in `ShellExecute` console ouput

### v0.9.3 beta3

Released on 2018-02-25

- \[ADD\] Commands for wim file 
- \[ADD\] Command LoopLetter 
- \[ADD\] Show url of WebLabel as tooltip
- \[ADD\] Caption can be specified in Bevel
- \[ADD\] NT6 Style FolderBrowserDialog
- \[ADD\] Indicate build progress on taskbar
- \[FIX\] Before execution of scripts run `script.project`
- \[FIX\] Missing formats added to StrFormat,Date
- \[FIX\] Drive letter support for StrFormat,Inc/Dec
- \[FIX\] Escape characters interpreted case insensitive properly
- \[FIX\] Escape character `##` works properly with `#c`, `#a`, `#r`

### v0.9.2 beta2

Released on 2018-01-20

- \[FIXED\] WebLabel parsing bug
- \[FIXED\] Additional Permission added to the license

### v0.9.1 beta1

Released on 2018-01-03

- Initial release.
