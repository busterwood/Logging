# Logging
Moves rolling file logging out of your application, you just write your logging to StdErr, for example:

```
FileLogger myapp
```
Files are written with data a sequence number, for example `logfile-20170731-01.log`


## Options

```
--folder    the directory to write files to, default is '.'
--prefix    the file prefix to use, default is 'logfile'
--maxlines  max number to lines to write to each file, default to 10,000
--inbuffer  number of lines to buffer between reader and writer threads, default is 100
--outbuffer size of the output stream buffer, default is 4096
--tee       copies the processes StdErr to StdErr
--time      outputs the number of lines written per second to StdErr
```

## Performance

My basic tests show `FileLogger` can write just over 350,000 lines per second to a local SSD (around 40MB per second), but you may like to test it with your own data.

```
C:\Dev\BusterWood.Logging>cat test.txt | Logging\bin\Release\FileLogger.exe --time --maxlines 100000
Logging: now writing to .\logfile-20170801-01.log
Logging: now writing to .\logfile-20170801-02.log
Logging: now writing to .\logfile-20170801-03.log
Logging: now writing to .\logfile-20170801-04.log
Logging: wrotes 354,675 lines per second
Logging: now writing to .\logfile-20170801-05.log
Logging: now writing to .\logfile-20170801-06.log
Logging: now writing to .\logfile-20170801-07.log
Logging: now writing to .\logfile-20170801-08.log
Logging: wrotes 388,605 lines per second
```
