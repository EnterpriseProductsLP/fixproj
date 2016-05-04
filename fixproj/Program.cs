using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Plossum.CommandLine;

namespace fixproj
{
    // ReSharper disable PossibleNullReferenceException
    // ReSharper disable PossibleMultipleEnumeration
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var options = new Options();
                var parser = new CommandLineParser(options);
                parser.Parse();
                if (parser.HasErrors)
                {
                    Console.WriteLine(parser.UsageInfo);
                    return 1;
                }

                if (options.Preview)
                {
                    Console.WriteLine("*** PREVIEW ONLY! DON'T PANIC!\n");
                }

                var entries = new List<ProjectFile>();

                Directory
                    .GetFiles(options.TargetDirectory, options.FileMask, options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Where(file => !file.Contains(@"\packages\"))
                    .ForEach(file =>
                    {
                        Console.WriteLine("Processing: {0}", file);
                        var entry = new ProjectFile { Changed = XDocument.Load(file), FileName = file, Original = XDocument.Load(file), Options = options };
                        entry.Change();
                        if (XNode.DeepEquals(entry.Original, entry.Changed))
                        {
                            Console.WriteLine("  NO CHANGES\n");
                            return;
                        }
                        Console.WriteLine("  {0} CHANGES\n", entry.Changes.Count);
                        entries.Add(entry);
                    });

                if (!options.Preview)
                {
                    Console.WriteLine("\nSaving {0} sanitized files.", entries.Count);
                    foreach (var e in entries)
                    {
                        if (options.CreateBackup)
                        {
                            e.Original.Save(e.FileName + ".bak");
                        }

                        e.Changed.Save(e.FileName);
                    }
                    Console.WriteLine("Saved {0} sanitized files.", entries.Count);
                }
                else
                {
                    Console.WriteLine("\nPreview: {0} files would have been changed given your criteria.", entries.Count);
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 1;
            }
            finally
            {
                if (Debugger.IsAttached)
                {
                    Console.ReadLine();
                }
            }
        }
    }
}
