using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace fixproj
{
    public class ProjectFile
    {
        public IList<string> Changes { get; } = new List<string>();

        public Options Options { get; set; }

        public string FileName { get; set; }

        public XDocument Changed { get; set; }

        public XDocument Original { get; set; }

        public void Change()
        {
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            // remove all the item groups because VS semi-randomly makes more
            // than is necessary
            var groups = Changed.Root.ElementsByLocalName("ItemGroup").ToList();
            var insertAt = groups.First().PreviousNode;
            groups.ForEach(x => x.Remove());

            // fix up various junk that can happen. we have to do this before all other
            // processing because things could be in the wrong place, etc
            if (Options.FixContent)
            {
                groups.Elements().ForEach(x =>
                {
                    var nodeName = x.Name.LocalName;
                    var value = x.Attribute("Include").Value.ToLower();

                    // remove superfluous Code subtype
                    var st = x.Element(ns + "SubType");
                    if (st != null && st.Value == "Code")
                    {
                        Record("{0}: removed Code SubType from {1}", nodeName, value);
                        st.Remove();
                    }

                    // fix config files
                    if (new[] { "packages.config", "app.config", "web.config" }.Contains(value))
                    {
                        if (nodeName != "None")
                        {
                            Record("{0}: changing to None for {1}", nodeName, value);
                            x.Name = ns + "None";
                        }

                        var n = x.Element(ns + "CopyToOutputDirectory");
                        if (n != null)
                        {
                            Record("{0}: Removing CopyToOutputDirectory for {1}", nodeName, value);
                            n.Remove();
                        }
                    }                           
                    // fix copy issues
                    var node = x.Element(ns + "CopyToOutputDirectory");
                    if (node != null)
                    {
                        if (node.Value.Contains("Always"))
                        {
                            node.Value = "PreserveNewest";
                            Record("{0}: Changing {1} to PreserveNewest instead of CopyAlways", x.Name.LocalName, x.Attribute("Include").Value);
                        }

                        // conflicting statements: is it embedded or to be copied?
                        // we have to do this interactively, even if it is annoying
                        if (nodeName == "EmbeddedResource")
                        {
                            for (;;)
                            {
                                Console.WriteLine("EmbeddedResource {0} claims it also wants to be copied to the output folder.", value);
                                Console.WriteLine("Keep it an (E)mbeddedResource, change it to (C)ontent with PreserveNewest, or (S)kip?");
                                switch (Console.ReadLine())
                                {
                                    case "e":
                                    case "E":
                                        node.Remove();
                                        goto later;
                                    case "c":
                                    case "C":
                                        node.Parent.Name = ns + "Content";
                                        goto later;
                                    case "s":
                                    case "S":
                                        goto later;
                                }
                            }
                        }
                    }

                    // make cshtml's that are not embedded or content into Content
later:
                    if (value.EndsWith(".cshtml") && nodeName != "Content" && nodeName != "EmbeddedResource")
                    {
                        Record("{0}: making {1} into Content", nodeName, value);
                        x.Name = ns + "Content";
                    }
                });
            }

            // put all of the items in buckets based on the type of the item
            var itemGroups = groups
                .SelectMany(x => x.Elements())
                .ToLookup(x => x.Name)
                .OrderBy(x => x.Key.LocalName)
                .Select(x => new { GroupName = x.Key.LocalName, Items = new List<XElement>(x) })
                .ToList();

            if (groups.Count != itemGroups.Count)
            {
                Record("Replacing {0} ItemGroup elements with {1}", groups.Count, itemGroups.Count);
            }

            foreach (var itemGroup in itemGroups)
            {
                var groupToAdd = new XElement(ns + "ItemGroup");
                var dir = Path.GetDirectoryName(FileName) ?? ".";

                // limitation for now, only does C# files recursively
                if (!string.IsNullOrEmpty(Options.AddCompileFilesThatExistOnDisk))
                {
                    if (itemGroup.GroupName == "Compile")
                    {
                        var exts = Options.AddCompileFilesThatExistOnDisk.Split(',');
                        var lowerIncludeToElement = itemGroup.Items.DistinctBy(x => x.Attribute("Include").Value.ToLower()).ToDictionary(x => x.Attribute("Include").Value.ToLower());
                        var actualShortFileNameToLong = exts.SelectMany(x => Directory.EnumerateFiles(dir, x, SearchOption.AllDirectories)).ToDictionary(x => x.Substring(dir.Length + 1));                        
                        foreach (var a in actualShortFileNameToLong.Keys)
                        {
                            XElement e;
                            if (lowerIncludeToElement.TryGetValue(a.ToLower(), out e))
                            {
                                if (e.Attribute("Include").Value != a)
                                {
                                    Record("{0} case mismatch between Include and file system name. Changing case \n  from Include value '{1}'\n    to file system name '{2}'", itemGroup.GroupName, e.Attribute("Include").Value, a);
                                    e.Attribute("Include").Value = a;
                                }
                            }
                        }

                        var includes = new HashSet<string>(itemGroup.Items.Select(x => x.Attribute("Include").Value));

                        // second pass: add the missing ones
                        var group = itemGroup;
                        var newReferences = actualShortFileNameToLong
                            // and make sure they aren't already included, and not in a special directory
                            .Where(kvp => !includes.Contains(kvp.Key) && !IsSpecialFile(kvp.Value))
                            // and then include them
                            .Select(kvp =>
                            {
                                Record("{0}: added {1}", group.GroupName, kvp.Key);
                                return new XElement(ns + "Compile", new XAttribute("Include", kvp.Key));
                            });

                        itemGroup.Items.AddRange(newReferences);
                    }
                }                
                if (!string.IsNullOrEmpty(Options.AddEmbeddedResourceFilesThatExistOnDisk))
                {
                    if (itemGroup.GroupName == "EmbeddedResource")
                    {
                        var exts = Options.AddEmbeddedResourceFilesThatExistOnDisk.Split(',');
                        var lowerIncludeToElement = itemGroup.Items.DistinctBy(x => x.Attribute("Include").Value.ToLower()).ToDictionary(x => x.Attribute("Include").Value.ToLower());
                        var actualShortFileNameToLong = exts.SelectMany(x => Directory.EnumerateFiles(dir, x, SearchOption.AllDirectories)).ToDictionary(x => x.Substring(dir.Length + 1));                        
                        //var actualShortFileNameToLong = Directory.GetFiles(dir, Options.AddEmbeddedResourceFilesThatExistOnDisk, SearchOption.AllDirectories).ToDictionary(x => x.Substring(dir.Length + 1));
                        foreach (var a in actualShortFileNameToLong.Keys)
                        {                                            
                            XElement e;
                            if (lowerIncludeToElement.TryGetValue(a.ToLower(), out e))
                            {
                                if (e.Attribute("Include").Value != a)
                                {
                                    Record("{0} case mismatch between Include and file system name. Changing case \n  from Include value '{1}'\n    to file system name '{2}'", itemGroup.GroupName, e.Attribute("Include").Value, a);
                                    e.Attribute("Include").Value = a;
                                }
                            }
                        }

                        var includes = new HashSet<string>(itemGroup.Items.Select(x => x.Attribute("Include").Value));

                        // second pass: add the missing ones
                        actualShortFileNameToLong.ForEach(k => Console.WriteLine(k.Key + " " + k.Value));

                        var group = itemGroup;
                        var newReferences = actualShortFileNameToLong
                            // and make sure they aren't already included, and not in a special directory
                            .Where(kvp => !includes.Contains(kvp.Key))
                            // and then include them
                            .Select(kvp =>
                            {                                
                                Record("{0}: added {1}", group.GroupName, kvp.Key);
                                return new XElement(ns + "EmbeddedResource", new XAttribute("Include", kvp.Key));
                            });

                        itemGroup.Items.AddRange(newReferences);
                    }
                }

                if (!string.IsNullOrEmpty(Options.AddContentFilesThatExistOnDisk))
                {
                    if (itemGroup.GroupName == "Content")
                    {
                        var exts = Options.AddContentFilesThatExistOnDisk.Split(',');
                        var lowerIncludeToElement = itemGroup.Items.DistinctBy(x => x.Attribute("Include").Value.ToLower()).ToDictionary(x => x.Attribute("Include").Value.ToLower());
                        var actualShortFileNameToLong = exts.SelectMany(x => Directory.EnumerateFiles(dir, x, SearchOption.AllDirectories)).ToDictionary(x => x.Substring(dir.Length + 1));                        
                        foreach (var a in actualShortFileNameToLong.Keys)
                        {
                            XElement e;
                            if (lowerIncludeToElement.TryGetValue(a.ToLower(), out e))
                            {
                                if (e.Attribute("Include").Value != a)
                                {
                                    Record("{0} case mismatch. Changing case \n  from {1}\n    to {2}", itemGroup.GroupName, e.Attribute("Include").Value, a);
                                    e.Attribute("Include").Value = a;
                                }
                            }
                        }

                        var includes = new HashSet<string>(itemGroup.Items.Select(x => x.Attribute("Include").Value.ToLower()));

                        // second pass: add the missing ones
                        var group = itemGroup;
                        var newReferences = actualShortFileNameToLong
                            // and make sure they aren't already included, and not in a special directory
                            .Where(kvp => !includes.Contains(kvp.Key.ToLower()))
                            // and then include them
                            .Select(kvp =>
                            {
                                Record("{0}: added {1}", group.GroupName, kvp.Key);
                                return new XElement(ns + "Content", new XAttribute("Include", kvp.Key));
                            });

                        itemGroup.Items.AddRange(newReferences);
                    }
                }

                if (Options.DeleteDuplicates)
                {
                    itemGroup.Items
                        // operate on a copy since we will modify the original list
                        .ToList()
                        // duplicates are those that have the same Include more than one time
                        .GroupBy(i => i.Attribute("Include").Value.ToLower())
                        .Where(g => g.Count() > 1)
                        .Select(g =>
                        {
                            // skip the first one, the rest are the actual duplicates
                            var dupesOfFirst = g.Skip(1).ToList();
                            Record("{0}: removed {1} dupes of {2}", itemGroup.GroupName, dupesOfFirst.Count, dupesOfFirst.First().Attribute("Include").Value);
                            return dupesOfFirst;
                        })
                        // remove the ones we found from the original collection
                        .SelectMany(x => x)
                        .ForEach(x => itemGroup.Items.Remove(x));
                }

                if (Options.DeleteReferencesToNonExistentFiles)
                {
                    if (!new[] { "WCFServiceReference", "WCFMetadata", "Reference", "ProjectReference", "Folder", "Service", "BootstrapperPackage" }.Contains(itemGroup.GroupName))
                    {
                        itemGroup.Items
                            // operate on a copy since we will modify the original list
                            .ToList()
                            .Where(x => IsDeletable(x, dir))
                            .ForEach(x =>
                            {                           
                                Record("{0}: removed reference to {1} because it doesn't exist", itemGroup.GroupName, x.Attribute("Include").Value);   
                                itemGroup.Items.Remove(x);
                            });
                    }
                }

                if (Options.Sort)
                {                    
                    groupToAdd.Add(itemGroup.Items.OrderBy(x => x.Attribute("Include").Value));
                    Record("{0}: sorted", itemGroup.GroupName);
                }
                else
                {
                    groupToAdd.Add(itemGroup.Items);
                }

                // add the actual ItemGroup back to the project
                insertAt.AddAfterSelf(groupToAdd);
                insertAt = groupToAdd;
            }

            if (Options.Rip)
            {
                var n = Changed.Root.DescendantsByLocalName("StyleCopTreatErrorsAsWarnings").FirstOrDefault();
                if (n != null && n.Value.ToLower() == "false")
                {
                    n.Value = "true";
                    Record("Made StyleCop less annoying");
                }
/*
                var imports = Changed.Root.ElementsByLocalName("Import").Where(x => x.Attribute("Project").Value.Contains("StyleCop")).ToList();
                imports.ForEach(x => x.Remove());
                Record("Removed StyleCop");
*/ 
            }

            if (Options.Unrip)
            {
                var n = Changed.Root.DescendantsByLocalName("StyleCopTreatErrorsAsWarnings").FirstOrDefault();
                if (n == null)
                {
                    var pg = Changed.Root.ElementsByLocalName("PropertyGroup").First();
                    pg.Add(new XElement(ns + "StyleCopTreatErrorsAsWarnings", "false"));
                    Record("Added StyleCop in annoying mode");
                }
                else
                if (n.Value.ToLower() == "true")
                {
                    n.Value = "false";
                    Record("Made StyleCop more annoying");
                }

                var parts = FileName.Split('\\');
                var dots = "";
                for (var i = parts.Length - 2; i != 0; i--)
                {
                    if (parts[i].ToLower() == "source")
                    {
                        break;
                    }

                    dots += @"..\";
                }

                var csharp = Changed.Root.ElementsByLocalName("Import").FirstOrDefault(x => x.Attribute("Project").Value.ToLower().Contains("microsoft.csharp.targets"));
                if (csharp != null)
                {
                    if (!Changed.Root.ElementsByLocalName("Import").Any(x => x.Attribute("Project").Value.ToLower().Contains(@"stylecop\stylecop.targets")))
                    {
                        csharp.AddAfterSelf(new XElement(
                                                ns + "Import",
                                                new XAttribute("Project", dots + @"Lib\StyleCop\StyleCop.Targets")));
                        Record("Added StyleCop.Targets");
                    }
                    if (!Changed.Root.ElementsByLocalName("Import").Any(x => x.Attribute("Project").Value.ToLower().Contains(@"msbuild\stylecop")))
                    {
                        csharp.AddAfterSelf(new XElement(
                                                ns + "Import",
                                                new XAttribute("Project", @"$(MSBuildProgramFiles32)\MSBuild\StyleCop\v4.7\StyleCop.targets"),
                                                new XAttribute("Condition", @"Exists('$(MSBuildProgramFiles32)\MSBuild\StyleCop\v4.7\StyleCop.targets')")));
                        Record("Added StyleCop.Targets");
                    }
                }
            }

            if (Options.Sort)
            {
                Changed.Root
                    .ElementsByLocalName("PropertyGroup")
                    .ForEach(e => e.Sort());
            }
        }

        public void Record(string message, params object[] args)
        {
            Options.Output("  " + message, args);
            Changes.Add(string.Format(message, args));
        }

        private static bool IsSpecialFile(string file)
        {
            return SpecialFileEx.IsMatch(file);
        }

        private bool IsDeletable(XElement x, string dir)
        {
            // get the value of the Include attribute, unencoding
            var value = x.Attribute("Include").Value.Replace("%27", "'").Replace("%28", "(").Replace("%29", ")");

            if (value.Contains(@"\packages\"))
            {
                // these may not exist because sln was cleaned; don't remove them to be safe
                return false;
            }
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), dir, value);
            return !File.Exists(fullPath);
        }

        private static readonly Regex SpecialFileEx = new Regex(@"(packages|\\bin|\\obj)\\|\.cs\.|TemporaryGenerated", RegexOptions.Compiled);
    }
}