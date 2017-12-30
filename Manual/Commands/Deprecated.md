# Deprecated Commands

## Deprecated from WinBuilder 082

### FileByteExtract

No longer used.

### RegReadBin / RegWriteBin

Not needed. PEBakery has native support for QWORDs and Unicode strings in RegRead and RegWrite.

### ExtractAllFilesIfNotExist

No longer used. Can be accomplished via other methods.

### ExtractAndRun

Due to limitations of the original Winbuilder command this can be better accomplished via a macro.

### StrFormat,CharToOEM / OEMToChar

DOS charset is no longer used.

### System,Comp80

PEBakery is new implementation of WinBuilder 082, no room for WinBuilder 080.

### System,FileRedirect / RegRedirect

PEBakery can be compiled into AnyCPU / x64, it can run without WOW64.

### System,IsTerminal

No longer used.

### System,Log

No longer used.

### System,SplitParameters

No longer used, PEBakery will always split parameters.

### If,License

No longer used.

## Commands will be deprecated

### ExtractAndRun

Due to the limitations of the original Winbuilder command this is better off implemented as a macro using standard Extract and ShellExecuteDelete

### WebGetIfNotExist

This commmand is broken in WB082, and it is better to implement this as macro.

### StrFormat,ShortPath / LongPath

Success of conversion to short path depends on registry value `HKLM\System\CurrentControlSet\Control\FileSystem\NtfsDisable8dot3NameCreation`.

Thus this commands cannot be guaranteed to work properly in every system.

### System,HasUAC

Turning off UAC in Windows is very dangerous, and turning UAC by force in Windows 10 will break UWP apps.

Currently PEBakery return always true to this command.

### System,RebuildVars

While WB082 manual claims this command will refresh variables to use newer value, it just clear variables in real WB082.

Currently PEBakery reset variables to default plugin variables.

### GetParam / PackParam

Those commands are totally broken in WB082, so it was not used.

PEBakery supports infinite number of section parameter, so these commands are no longer necessary.
