``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.101
  [Host]     : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT
  DefaultJob : .NET Core 3.1.1 (CoreCLR 4.700.19.60701, CoreFX 4.700.19.60801), X64 RyuJIT


```
|     Method |      Mean |     Error |    StdDev |
|----------- |----------:|----------:|----------:|
| NativeGZip |  9.777 ms | 0.0667 ms | 0.0624 ms |
|   NativeXZ | 24.041 ms | 0.2755 ms | 0.2577 ms |
|  NativeLZ4 |  4.827 ms | 0.0419 ms | 0.0392 ms |
| ManagedLZ4 |  5.269 ms | 0.1008 ms | 0.1274 ms |
