# Logging
Moves rolling file logging out of your application, you just write to StdOut (or StdErr with redirection), for example:

```
myapp | FileLogger
```
Files are written with data a sequence number, for example `logfile-20170731-01.log`


## Options

```
--folder    the directory to write files to, default is '.'
--prefix    the file prefix to use, default is 'logfile'
--maxlines  max number to lines to write to each file, default to 10,000
--inbuffer  number of lines to buffer between reader and writer threads, default is 100
--outbuffer size of the output stream buffer, default is 4096
--time      outputs the number of lines written per second to StdErr
```

## Performance

My basic tests show `FileLogger` can write just over 350,000 lines per second to a local SSD, but you may like to test it with your own data.

```
cat test.txt | Logging\bin\Release\FileLogger.exe --time
....
Logging: wrotes 363,708 lines per second
....
Logging: wrotes 381,926 lines per second
....
```
