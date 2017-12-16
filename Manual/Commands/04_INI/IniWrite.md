# IniWrite

This command will write the key and value in the section inside the file you specify

## Syntax

```pebakery
IniWrite,<Filename>,<Section>,<Key>,<Value>
```

### Arguments

| Argument | Description |
| --- | --- |
| FileName | The full path of the file. |
| Section | The Section where the value will be written. |
| Key | The key that will contain the value.|
| Value | The value to write.|

## Remarks
If `FileName` does not exist it will be created.

## Example 1

Lets assume we have the following .ini file:
```pebakery
// C:\myFile.ini
[mySection]
myKey=myValue
anotherKey=anotherValue
```

In the following example the value of `1234` will be written to the key `myKey`  
```pebakery
IniWrite,C:\myFile.ini,mySection,myKey,1234
```

## Example 2

A common usage of WriteIni is to retrieve values from the plugin interface and write them to an applications configuration file.  

Lets assume we have a program that uses the following .ini file:
```pebakery
// C:\myConfig.ini
[Config]
ShowWindow=true
ShowText=MyString
```

Code
```pebakery
[Process]
// Store the value of a checkbox in the config file
IniWrite,C:\myConfig.ini,Config,ShowWindow,%pCheckbox1%

//Store the value of a textbox in the config file
IniWrite,C:\myConfig.ini,Config,MyString,%pText1%

[Interface]
pText1="Show this text:",1,1,10,8,106,18,8,Normal
pCheckbox1="Show Window?",1,3,10,32,278,18,False
```