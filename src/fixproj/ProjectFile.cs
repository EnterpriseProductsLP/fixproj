using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FixProjects
{
    public class ProjectFile
    {
        public IList<string> Changes { get; } = new List<string>();

        public CommandLineOptions CommandLineOptions { get; set; }

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

            // fix up various special cases before all other
            // processing because things could be in the wrong place, etc
            if (CommandLineOptions.FixContent)
            {
                Changed.Root
                    .ElementsByLocalName("PropertyGroup")
                    .ForEach(x =>
                    {
                        // make elements with no real content into empty ones
                        if (!x.HasNoContent())
                            return;
                        Record("Removing empty content from property.");
                        x.MakeEmpty();
                    });

                groups.Elements().ForEach(x =>
                {
                    var nodeName = x.Name.LocalName;
                    var originalCaseIncludeValue = x.Attribute("Include").Value;
                    var lowerCaseIncludeValue = originalCaseIncludeValue.ToLower();

                    // make elements with no real content into empty ones
                    if (x.HasNoContent())
                    {
                        Record($"{nodeName}: removing all empty content from {originalCaseIncludeValue}");
                        x.MakeEmpty();
                    }

                    // remove superfluous Code subtype
                    var st = x.Element(ns + "SubType");
                    if (st != null && st.Value == "Code")
                    {
                        Record($"{nodeName}: removed Code SubType from {originalCaseIncludeValue}");
                        st.Remove();
                    }

                    // fix specific config files wherever they may be located
                    if (lowerCaseIncludeValue.EndsWithAnyOf("packages.config", "app.config", "web.config"))
                    {
                        if (lowerCaseIncludeValue.EndsWith("web.config"))
                        {
                            // this is super critical. I have seen builds fail for weird reasons on a TFS
                            // build server (but not a dev box) when web.config is <None>.
                            if (nodeName != "Content")
                            {
                                Record($"{nodeName}: changing to Content for {originalCaseIncludeValue}");
                                x.Name = ns + "Content";
                            }
                        }
                        else if (nodeName != "None")
                        {
                            Record($"{nodeName}: changing to None for {originalCaseIncludeValue}");
                            x.Name = ns + "None";
                        }

                        // these config files should never be copied
                        var n = x.Element(ns + "CopyToOutputDirectory");
                        if (n != null)
                        {
                            Record($"{nodeName}: Removing CopyToOutputDirectory for {originalCaseIncludeValue}");
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
                            Record($"{x.Name.LocalName}: Changing {x.Attribute("Include").Value} to PreserveNewest instead of CopyAlways");
                        }

                        // conflicting statements: is it embedded or to be copied?
                        // we have to do this interactively, even if it is annoying
                        if (nodeName == "EmbeddedResource")
                            for (;;)
                            {
                                Console.WriteLine("EmbeddedResource {0} claims it also wants to be copied to the output folder.", originalCaseIncludeValue);
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


                    later:
                    // make cshtml's that are not embedded or content into Content
                    if (!lowerCaseIncludeValue.EndsWith(".cshtml") || nodeName == "Content" || nodeName == "EmbeddedResource") return;
                    Record($"{nodeName}: making {lowerCaseIncludeValue} into Content");
                    x.Name = ns + "Content";
                });
            }

            // put all of the items in buckets based on the type of the item
            // TODO: don't use an anon type.. it's preventing refactoring to smaller methods
            var itemGroups = groups
                .SelectMany(x => x.Elements())
                .ToLookup(x => x.Name)
                .OrderBy(x => x.Key.LocalName)
                .Select(x => new { GroupName = x.Key.LocalName, Items = new List<XElement>(x) })
                .ToList();

            if (groups.Count != itemGroups.Count)
                Record($"Replacing {groups.Count} ItemGroup elements with {itemGroups.Count}");

            foreach (var itemGroup in itemGroups)
            {
                var groupToAdd = new XElement(ns + "ItemGroup");
                var dir = Path.GetDirectoryName(FileName) ?? ".";

                if (!string.IsNullOrEmpty(CommandLineOptions.AddCompileFilesThatExistOnDisk))
                {
                    // this ain't very DRY. should fix...

                    if (itemGroup.GroupName == "Compile")
                    {
                        var exts = CommandLineOptions.AddCompileFilesThatExistOnDisk.Split(',');
                        var lowerIncludeToElement = itemGroup.Items.DistinctBy(x => x.Attribute("Include").Value.ToLower()).ToDictionary(x => x.Attribute("Include").Value.ToLower());
                        var actualShortFileNameToLong = exts.SelectMany(x => Directory.EnumerateFiles(dir, x, SearchOption.AllDirectories)).ToDictionary(x => x.Substring(dir.Length + 1));                        
                        foreach (var a in actualShortFileNameToLong.Keys)
                        {
                            XElement e;
                            if (!lowerIncludeToElement.TryGetValue(a.ToLower(), out e))
                                continue;
                            if (e.Attribute("Include").Value == a)
                                continue;

                            Record($"{itemGroup.GroupName}: case mismatch between Include and file system name. Changing case \n  from Include value '{e.Attribute("Include").Value}'\n    to file system name '{a}'");
                            e.Attribute("Include").Value = a;
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
                                Record($"{group.GroupName}: added {kvp.Key}");
                                return new XElement(ns + "Compile", new XAttribute("Include", kvp.Key));
                            });

                        itemGroup.Items.AddRange(newReferences);
                    }
                }                
                if (!string.IsNullOrEmpty(CommandLineOptions.AddEmbeddedResourceFilesThatExistOnDisk))
                {
                    if (itemGroup.GroupName == "EmbeddedResource")
                    {
                        var exts = CommandLineOptions.AddEmbeddedResourceFilesThatExistOnDisk.Split(',');
                        var lowerIncludeToElement = itemGroup.Items.DistinctBy(x => x.Attribute("Include").Value.ToLower()).ToDictionary(x => x.Attribute("Include").Value.ToLower());
                        var actualShortFileNameToLong = exts.SelectMany(x => Directory.EnumerateFiles(dir, x, SearchOption.AllDirectories)).ToDictionary(x => x.Substring(dir.Length + 1));                        
                        foreach (var a in actualShortFileNameToLong.Keys)
                        {                                            
                            XElement e;
                            if (!lowerIncludeToElement.TryGetValue(a.ToLower(), out e))
                                continue;
                            if (e.Attribute("Include").Value == a)
                                continue;

                            Record($"{itemGroup.GroupName} case mismatch between Include and file system name. Changing case \n  from Include value '{e.Attribute("Include").Value}'\n    to file system name '{a}'");
                            e.Attribute("Include").Value = a;
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
                                Record($"{group.GroupName}: added {kvp.Key}");
                                return new XElement(ns + "EmbeddedResource", new XAttribute("Include", kvp.Key));
                            });

                        itemGroup.Items.AddRange(newReferences);
                    }
                }

                if (!string.IsNullOrEmpty(CommandLineOptions.AddContentFilesThatExistOnDisk))
                {
                    if (itemGroup.GroupName == "Content")
                    {
                        var exts = CommandLineOptions.AddContentFilesThatExistOnDisk.Split(',');
                        var lowerIncludeToElement = itemGroup.Items.DistinctBy(x => x.Attribute("Include").Value.ToLower()).ToDictionary(x => x.Attribute("Include").Value.ToLower());
                        var actualShortFileNameToLong = exts.SelectMany(x => Directory.EnumerateFiles(dir, x, SearchOption.AllDirectories)).ToDictionary(x => x.Substring(dir.Length + 1));                        
                        foreach (var a in actualShortFileNameToLong.Keys)
                        {
                            XElement e;
                            if (!lowerIncludeToElement.TryGetValue(a.ToLower(), out e))
                                continue;
                            if (e.Attribute("Include").Value == a)
                                continue;

                            Record($"{itemGroup.GroupName} case mismatch. Changing case \n  from {e.Attribute("Include").Value}\n    to {a}");
                            e.Attribute("Include").Value = a;
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
                                Record($"{group.GroupName}: added {kvp.Key}");
                                return new XElement(ns + "Content", new XAttribute("Include", kvp.Key));
                            });

                        itemGroup.Items.AddRange(newReferences);
                    }
                }

                if (CommandLineOptions.DeleteDuplicates)
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
                            Record($"{itemGroup.GroupName}: removed {dupesOfFirst.Count} dupes of {dupesOfFirst.First().Attribute("Include").Value}");
                            return dupesOfFirst;
                        })
                        // remove the ones we found from the original collection
                        .SelectMany(x => x)
                        .ForEach(x => itemGroup.Items.Remove(x));

                if (CommandLineOptions.DeleteReferencesToNonExistentFiles)
                    if (!new[] { "WCFServiceReference", "WCFMetadata", "Reference", "ProjectReference", "Folder", "Service", "BootstrapperPackage", "PackageReference" }.Contains(itemGroup.GroupName))
                        itemGroup.Items
                            // operate on a copy since we will modify the original list
                            .ToList()
                            .Where(x => IsDeletable(x, dir))
                            .ForEach(x =>
                            {
                                Record($"{itemGroup.GroupName}: removed reference to {x.Attribute("Include").Value} because it doesn't exist");
                                itemGroup.Items.Remove(x);
                            });

                if (CommandLineOptions.Sort)
                {
                    groupToAdd.Add(itemGroup.Items.OrderBy(x => x.Attribute("Include").Value));
                    Record($"{itemGroup.GroupName}: sorted");
                }
                else
                    groupToAdd.Add(itemGroup.Items);

                // add the actual ItemGroup back to the project
                insertAt.AddAfterSelf(groupToAdd);
                insertAt = groupToAdd;
            }

            if (CommandLineOptions.Sort)
            {
                Changed.Root
                    .ElementsByLocalName("PropertyGroup")
                    .ForEach(e => e.Sort());
            }
        }

        public void Record(string message)
        {
            CommandLineOptions.Output("  " + message);
            Changes.Add(message);
        }

        private static bool IsSpecialFile(string file)
        {
            return SpecialFileEx.IsMatch(file);
        }

        private bool IsValidPackageReferenceVersion(XElement element) =>
            element.HasAttributes && element.Attribute("Version")?.Value.StartsWith("$(") == true;

        private bool IsDeletable(XElement x, string dir)
        {
            // get the value of the Include attribute, unencoding
            var value = x.Attribute("Include").Value.Replace("%27", "'").Replace("%28", "(").Replace("%29", ")");

            if (value.Contains(@"\packages\"))
            {
                // these may not exist because sln was cleaned; don't remove them to be safe
                return false;
            }

            if (value.Contains("*"))
            {
                // skip wildcards
                return false;
            }
            
             var fullPath = Path.Combine(Directory.GetCurrentDirectory(), dir, value);
            return !File.Exists(fullPath);
        }

        private static readonly Regex SpecialFileEx = new Regex(@"(packages|\\bin|\\obj)\\|\.cs\.|TemporaryGenerated", RegexOptions.Compiled);
    }
}