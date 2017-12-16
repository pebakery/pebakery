# WebGet

Download file from internet.

## Syntax

```pebakery
WebGet,<URL>,<DestPath>,[HashType],[HashDigest]
```

- Arguments

| Argument | Description |
| --- | --- |
| URL | URL of the file to download. |
| DestPath | Path to save downloaded file. |

- Optional Arugments

| Argument | Description |
| --- | --- |
| HashType   | Hash type to calcuate. Should be one of `MD5`, `SHA1`, `SHA256`, `SHA384`, `SHA512`. |
| HashDigest | Downloaded file will be verified with this hash digest. |

`HashType` and `HashDigest` should be used at same time.

## Example

```pebakery
// zlib source code will be downloaded to %BaseDir%\zlib.tar.gz.
WebGet,"https://zlib.net/zlib-1.2.11.tar.gz",%BaseDir%\zlib.tar.gz
```

```pebakery
// Downloaded tar.gz file will be validated with its SHA256 digest.
WebGet,"https://zlib.net/zlib-1.2.11.tar.gz",%BaseDir%\zlib.tar.gz,SHA256,c3e5e9fdd5004dcb542feda5ee4f0ff0744628baf8ed2dd5d66f8ca1197cb1a1
```
