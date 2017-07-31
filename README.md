# Logging
Moves rolling file logging out of your application, you just write to StdOut (or StdErr with redirection), for example:

```
myapp | FileLogger
```



## Options

```
--prefix    the file prefix to use, defaults to logfile
--maxlines  max number to lines to write to each file, default to 10,000
```
## Performance

My basic tests show `FileLogger` can write just over 350,000 lines per second to a local SSD, but you may like to test it with your own data.

```
cat test.txt | Logging\bin\Release\FileLogger.exe --time
Logging: timing
....
Logging: wrotes 363,708 lines per second
....
Logging: wrotes 381,926 lines per second
....
```
