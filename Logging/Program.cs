﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BusterWood.Logging
{
    class Program
    {
        static BlockingCollection<string> buffer;
        static FileNameProvider fileName;

        static void Main(string[] argv)
        {
            var args = argv.ToList();

            var inBuffer = args.IntFlag("--inbuffer");
            buffer = new BlockingCollection<string>(inBuffer ?? 100);

            var prefix = args.StringFlag("--prefix", "logfile");
            var folder = args.StringFlag("--folder", ".");
            fileName = new FileNameProvider(prefix, folder);

            // write on a separate thread so we can flush the buffer is no lines are received for a while (currently 100ms)
            var writingThread = new Thread(RunWriteToFile);
            writingThread.Start(args);

            for (;;)
            {
                var line = Console.ReadLine();
                buffer.Add(line); // send the null down to terminate the writingThread
                if (line == null)
                    break;
            }

            // make sure all buffered lines have been written before we exit
            writingThread.Join();
        }

        static void RunWriteToFile(object arg)
        {
            for(;;)
            {
                try
                {
                    var arg1 = (List<string>)arg;
                    WriteToFile(arg1.ToList()); // send a copy as args are removed
                    return;
                }
                catch (Exception ex)
                {
                    //TODO: handle no being able to write to the file, disk full, etc
                    Console.Error.WriteLine("Logging: " + ex);
                }
            }
        }

        static void WriteToFile(List<string> args)
        {
            var outBuffer = args.IntFlag("--outbuffer") ?? 4096;
            var timeout = TimeSpan.FromMilliseconds(100);
            var maxLines = args.IntFlag("--maxlines", 10000);
            bool time = args.Remove("--time");

            int lineCount = maxLines.Value + 1; // force file be opened first time through the loop
            int perSec = 0;
            DateTime windowStart = DateTime.UtcNow;
            FileStream stream = null;
            StreamWriter output = null;
            for (;;)
            {
                if (lineCount >= maxLines)
                {
                    output?.Close();
                    var path = fileName.Next();
                    stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, outBuffer); 
                    output = new StreamWriter(stream);
                    lineCount = 0;
                    Console.Error.WriteLine($"Logging: now writing to {path}");
                }

                string line;
                if (!buffer.TryTake(out line, timeout))
                {
                    output.Flush(); // no new lines for 100ms, lets flush the stream
                }
                else if (line == null)
                {
                    output.Close();
                    break;
                }
                else
                {
                    output.WriteLine(line);
                    lineCount++;
                    if (time)
                    {
                        perSec++;
                        if (DateTime.UtcNow - windowStart >= TimeSpan.FromSeconds(1))
                        {
                            Console.Error.WriteLine($"Logging: wrotes {perSec:N0} lines per second");
                            perSec = 0;
                            windowStart = DateTime.UtcNow;
                        }
                    }
                }
            }
        }

    }

    class FileNameProvider
    {
        readonly string prefix;
        readonly string folder;
        DateTime lastDate;
        int fileNumber;

        public FileNameProvider(string prefix, string folder)
        {
            this.prefix = prefix;
            this.folder = folder;
        }

        public string Next()
        {
            for(;;)
            {
                var now = DateTime.UtcNow.Date;
                if (now != lastDate)
                {
                    lastDate = now;
                    fileNumber = 1;
                }
                else
                    fileNumber += 1;

                var path = Path.Combine(folder, $"{prefix}-{now:yyyyMMdd}-{fileNumber:00}.log");

                if (!File.Exists(path))
                    return path;
            }
        }
    }

    public static class CommandLineExtensions
    {
        public static bool BoolFlag(this List<string> args, string flag)
        {
            int index = args.IndexOf(flag);
            if (index < 0)
                return false;
            args.RemoveAt(index);
            return true;
        }

        public static string StringFlag(this List<string> args, string flag, string @default = null)
        {
            int index = args.IndexOf(flag);
            if (index < 0 || index + 1 == args.Count)
                return @default;
            args.RemoveAt(index);   // remove flag
            var value = args[index];
            args.RemoveAt(index);   // remove value
            return value;
        }

        public static int? IntFlag(this List<string> args, string flag, int? @default = null)
        {
            int index = args.IndexOf(flag);
            if (index < 0 || index + 1 == args.Count)
                return @default;
            args.RemoveAt(index);   // remove flag
            var arg = args[index];
            args.RemoveAt(index);   // remove value
            int value;
            return int.TryParse(arg, out value) ? value : @default;
        }

    }
}
