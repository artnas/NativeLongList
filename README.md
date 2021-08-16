# Native Long List
Fast and simplified NativeList<T> implementation which allows you to store more than 1 GB of data (limitation of native collections)

Based on NativeCustomArray by Jackson Dunstan, http://JacksonDunstan.com/articles/4734

Performance testing code is included

## Supported methods

- Add
- AddRange
- Read/Write by index
- RemoveAt
- TrimExcess

## Performance

According to my tests it's just a tiny bit slower than NativeList

| Method | NativeList | NativeLongList |
| --- | --- | --- |
| Add & Read 1000000 elements | 8,03 ms | 7,49 ms |
| Add 268435455 elements (Burst) | 1 191,82 ms | 1 487,47 ms |
| AddRange 250000 elements 10 times | 8,21 ms | 7,31 ms |
