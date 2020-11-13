using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Plossum.CommandLine;

namespace FixProjects
{
    // ReSharper disable PossibleNullReferenceException
    // ReSharper disable PossibleMultipleEnumeration
    public class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var commandLineOptions = new CommandLineOptions();
                var parser = new CommandLineParser(commandLineOptions);
                parser.Parse();
                if (parser.HasErrors)
                {
                    Console.WriteLine(parser.UsageInfo);
                    return 1;
                }

                if (commandLineOptions.Preview)
                {
                    Console.WriteLine("*** PREVIEW ONLY! DON'T PANIC!\n");
                }

                var entries = new List<ProjectFile>();

                Directory
                    .GetFiles(commandLineOptions.TargetDirectory, commandLineOptions.FileMask, commandLineOptions.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Where(file => !file.Contains(@"\packages\"))
                    .ForEach(file =>
                    {
                        if (!commandLineOptions.Preview)
                        {
                            Console.WriteLine("Processing: {0}", file);
                        }

                        var entry = new ProjectFile { Changed = XDocument.Load(file), FileName = file, Original = XDocument.Load(file), CommandLineOptions = commandLineOptions };
                        entry.Change();
                        if (XNode.DeepEquals(entry.Original, entry.Changed))
                        {
                            if (!commandLineOptions.Preview)
                            {
                                Console.WriteLine("  NO CHANGES\n");
                            }

                            return;
                        }

                        if (commandLineOptions.Preview)
                        {
                            Console.WriteLine("Processing: {0}", file);
                        }

                        Console.WriteLine("  {0} CHANGES\n", entry.Changes.Count);

                        entries.Add(entry);
                    });

                if (!commandLineOptions.Preview)
                {
                    Console.WriteLine("\nSaving {0} sanitized files.", entries.Count);
                    foreach (var e in entries)
                    {
                        if (commandLineOptions.CreateBackup)
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

                return entries.Count == 0 ? 0 : 1;
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
