using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace BusterWood.Logging
{
    class Program
    {
        static BlockingCollection<string> buffer;
        static FileNameProvider fileName;

        static int outBuffer;
        static int? maxLines;
        static bool time;
        static bool tee;

        static int Main(string[] argv)
        {
            var args = argv.ToList();
            var inBuffer = args.IntFlag("--inbuffer") ?? 100;
            var prefix = args.StringFlag("--prefix", "logfile");
            var folder = args.StringFlag("--folder", ".");
            outBuffer = args.IntFlag("--outbuffer") ?? 4096;
            maxLines = args.IntFlag("--maxlines", 10000);
            tee = args.Remove("--tee");
            time = args.Remove("--time");

            fileName = new FileNameProvider(prefix, folder);
            buffer = new BlockingCollection<string>(inBuffer);

            if (args.Count == 0)
            {
                Console.Error.WriteLine("FileLoggging: command [args] - you must pass the command to run and optional arguments");
                return 1;
            }

            var exe = args[0];
            args.RemoveAt(0);

            Process p;
            try
            {
                p = Process.Start(
                    new ProcessStartInfo
                    {
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        FileName = exe,
                        Arguments = string.Join(" ", args),
                    }
                );

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return 9999;
            }

            var stdInCopier = new Thread(CopyInput) { IsBackground = true };
            stdInCopier.Start(p.StandardInput);

            var stdOutCopier = new Thread(CopyOutput);
            stdOutCopier.Start(p.StandardOutput);

            // write on a separate thread so we can flush the buffer is no lines are received for a while (currently 100ms)
            var writeStdErrToFile = new Thread(CopyBufferToFile);
            writeStdErrToFile.Start();

            for (;;)
            {
                var line = p.StandardError.ReadLine();
                buffer.Add(line); // send the null down to terminate the writingThread
                if (line == null)
                    break;
            }

            p.WaitForExit();

            // make sure all buffered lines have been written before we exit
            writeStdErrToFile.Join();
            stdOutCopier.Join();

            if (Debugger.IsAttached)
                Debugger.Break();
            return p.ExitCode;
        }

        static void CopyOutput(object state)
        {
            var @in = (StreamReader)state;
            @in.BaseStream.CopyToAsync(Console.OpenStandardOutput(), 4096);
        }

        static void CopyInput(object state)
        {
            var @out = (StreamWriter)state;
            Console.OpenStandardInput().CopyTo(@out.BaseStream, 4096);
        }

        static void CopyBufferToFile()
        {
            for(;;)
            {
                try
                {
                    WriteToFile(); 
                    return;
                }
                catch (Exception ex)
                {
                    //TODO: handle no being able to write to the file, disk full, etc
                    Console.Error.WriteLine("Logging: " + ex);
                }
            }
        }

        static void WriteToFile()
        {
            var timeout = TimeSpan.FromMilliseconds(100);

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
                    if (tee)
                        Console.Error.WriteLine(line);
                    lineCount++;
                    if (time)
                    {
                        perSec++;
                        if (DateTime.UtcNow - windowStart >= TimeSpan.FromSeconds(1))
                        {
                            Console.Error.WriteLine($"Logging: wrote {perSec:N0} lines per second");
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
