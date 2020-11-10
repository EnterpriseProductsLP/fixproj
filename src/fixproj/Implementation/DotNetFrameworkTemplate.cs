using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using fixproj.Abstract;

namespace fixproj.Implementation
{
    public class DotNetFrameworkTemplate : BaseTemplate, IOperateOnProjectFiles
    {
        private readonly List<string> _listOfAllowedActions = new List<string>{ "WCFServiceReference", "WCFMetadata", "Reference", "ProjectReference", "Folder", "Service", "BootstrapperPackage", "PackageReference" };
        private readonly XNamespace _ns = "http://schemas.microsoft.com/developer/msbuild/2003";
        private readonly string _filePath;

        public IList<string> Changes { get; }

        public XDocument ModifiedDocument { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetFrameworkTemplate"/> class.
        /// </summary>
        /// <param name="file">The path of the processed file.</param>
        public DotNetFrameworkTemplate(string file)
        {
            _filePath = file;
            ModifiedDocument = XDocument.Load(file);
            Changes = new List<string>();

            Initialize(ModifiedDocument);
        }

        /// <inheritdoc />
        public List<ItemGroupEntity> FixContent()
        {
            ItemGroupElements.ForEach(x => x.Remove());

            FixPropertyGroups(ModifiedDocument.Root, Changes);

            ItemGroupElements.Elements().ForEach(element =>
            {
                var originalCaseIncludeValue = element.AttributeValueByName(Constants.IncludeAttribute);

                // make elements with no real content into empty ones
                if (element.HasNoContent())
                {
                    Changes.Add($"{element.Name.LocalName}: removing all empty content from {originalCaseIncludeValue}");
                    element.MakeEmpty();
                }

                // remove superfluous Code subtype
                var st = element.Element(_ns + "SubType");
                if (st != null && st.Value == "Code")
                {
                    Changes.Add($"{element.Name.LocalName}: removed Code SubType from {originalCaseIncludeValue}");
                    st.Remove();
                }

                // fix specific config files wherever they may be located
                if (originalCaseIncludeValue.ToLower(CultureInfo.InvariantCulture).EndsWithAnyOf("packages.config", "app.config", "web.config"))
                {
                    if (originalCaseIncludeValue.ToLower(CultureInfo.InvariantCulture).EndsWith("web.config"))
                    {
                        // this is super critical. I have seen builds fail for weird reasons on a TFS
                        // build server (but not a dev box) when web.config is <None>.
                        if (element.Name.LocalName.Equals(Constants.ContentNode, StringComparison.InvariantCultureIgnoreCase))
                        {
                            Changes.Add($"{element.Name.LocalName}: changing to Content for {originalCaseIncludeValue}");
                            element.Name = _ns + Constants.ContentNode;
                        }
                    }
                    else if (element.Name.LocalName.Equals(Constants.NoneNode, StringComparison.InvariantCultureIgnoreCase) )
                    {
                        Changes.Add($"{element.Name.LocalName}: changing to None for {originalCaseIncludeValue}");
                        element.Name = _ns + Constants.NoneNode;
                    }

                    // these config files should never be copied
                    var n = element.Element(_ns + Constants.CopyToOutputDirectoryElement);
                    if (n != null)
                    {
                        Changes.Add($"{element.Name.LocalName}: Removing CopyToOutputDirectory for {originalCaseIncludeValue}");
                        n.Remove();
                    }
                }

                // fix copy issues
                var node = element.Element(_ns + Constants.CopyToOutputDirectoryElement);
                if (node != null)
                {
                    if (node.Value.Contains(Constants.AlwaysNodeValue))
                    {
                        node.Value = Constants.PreserveNewestNodeValue;
                        Changes.Add($"{element.Name.LocalName}: Changing {element.AttributeValueByName(Constants.IncludeAttribute)} to PreserveNewest instead of CopyAlways");
                    }

                    // conflicting statements: is it embedded or to be copied?
                    // we have to do this interactively, even if it is annoying
                    if (element.Name.LocalName == Constants.EmbeddedResourceNode)
                        for (; ; )
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
                                    node.Parent.Name = _ns + Constants.ContentNode;
                                    goto later;
                                case "s":
                                case "S":
                                    goto later;
                            }
                        }
                }


                later:
                // make cshtml's that are not embedded or content into Content
                if (!originalCaseIncludeValue.ToLower(CultureInfo.InvariantCulture).EndsWith(".cshtml") || element.Name.LocalName == Constants.ContentNode || element.Name.LocalName == Constants.EmbeddedResourceNode) return;
                Changes.Add($"{element.Name.LocalName}: making {originalCaseIncludeValue.ToLower(CultureInfo.InvariantCulture)} into Content");
                element.Name = _ns + Constants.ContentNode;
            });

            return ItemGroupElements
                .SelectMany(x => x.Elements())
                .ToLookup(x => x.Name)
                .OrderBy(x => x.Key.LocalName)
                .Select(x => new ItemGroupEntity()
                {
                    LocalName = x.Key.LocalName, 
                    Element = new List<XElement>(x)
                })
                .ToList();

        }

        /// <inheritdoc />
        public void DeleteDuplicates(ItemGroupEntity entity)
        {
            if(entity == null)
                throw new ArgumentNullException(nameof(entity));

            DeleteDuplicatesBasedOnAttribute(entity, Changes, x => x.AttributeValueByName(Constants.IncludeAttribute));
        }

        /// <inheritdoc />
        public void DeleteReferencesToNonExistentFiles(ItemGroupEntity entity)
        {
            if (!_listOfAllowedActions.Contains(entity.LocalName))
                entity.Element
                    .Where(x => IsDeletable(x, Path.GetDirectoryName(_filePath) ?? "."))
                    .ForEach(x =>
                    {
                        Changes.Add($"{entity.LocalName}: removed reference to {x.AttributeValueByName(Constants.IncludeAttribute)} because it doesn't exist");
                        entity.Element.Remove(x);
                    });
        }

        /// <inheritdoc />
        public void MergeAndSortItemGroups(ItemGroupEntity entity, bool sort)
        {
            var groupToAdd = new XElement(_ns + Constants.ItemGroupNode);

            if (sort)
            {
                groupToAdd.Add(entity.Element.OrderBy(x => x.AttributeValueByName(Constants.IncludeAttribute)));
                Changes.Add($"{entity.LocalName}: sorted");
            }
            else
            {
                groupToAdd.Add(entity.Element);
            }

            InsertedAt.AddAfterSelf(groupToAdd);
            InsertedAt = groupToAdd;
        }

        /// <inheritdoc />
        public void SortPropertyGroups() => Sort(ModifiedDocument);

        private static bool IsDeletable(XElement x, string dir)
        {
            // get the value of the Include attribute, unencoding
            var value = x.AttributeValueByName(Constants.IncludeAttribute).Replace("%27", "'").Replace("%28", "(").Replace("%29", ")");

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
    }
}
