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
        private readonly List<string> _listOfAllowedActions = new List<string>{ Constants.WcfServiceReference, Constants.WcfMetadata, Constants.Reference, Constants.ProjectReference, 
                                                                                    Constants.Folder, Constants.Service, Constants.BootstrapperPackage, Constants.PackageReference };
        private readonly XNamespace _ns = "http://schemas.microsoft.com/developer/msbuild/2003";

        /// <inheritdoc />
        public IList<string> Changes { get; } = new List<string>();

        /// <inheritdoc />
        public XDocument ModifiedDocument { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetFrameworkTemplate"/> class.
        /// </summary>
        /// <param name="file">The path of the processed file.</param>
        public DotNetFrameworkTemplate(string file) : base(file)
        {
            ModifiedDocument = XDocument.Load(file);

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
                ProcessConfigFiles(element, originalCaseIncludeValue);

                // fix copy issues
                FixCopyIssue(element.Element(_ns + Constants.CopyToOutputDirectoryElement), Changes);

                // make cshtml's that are not embedded or content into Content
                if (!originalCaseIncludeValue.ToLower(CultureInfo.InvariantCulture).EndsWith(".cshtml") 
                    || element.Name.LocalName == Constants.ContentNode 
                    || element.Name.LocalName == Constants.EmbeddedResourceNode) 
                    return;

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
        public void DeleteDuplicates(ItemGroupEntity entity) => DeleteDuplicatesBasedOnAttribute(entity, Changes, x => x.AttributeValueByName(Constants.IncludeAttribute));

        /// <inheritdoc />
        public void DeleteReferencesToNonExistentFiles(ItemGroupEntity entity)
        {
            if (!_listOfAllowedActions.Contains(entity.LocalName))
                ItemGroupElements.Elements()
                    .Where(x => IsDeletable(x, Path.GetDirectoryName(FilePath) ?? "."))
                    .ForEach(x =>
                    {
                        Changes.Add($"{entity.LocalName}: removed reference to {x.AttributeValueByName(Constants.IncludeAttribute)} because it doesn't exist");
                        entity.Element.Remove(x);
                    });
        }

        /// <inheritdoc />
        public void MergeAndSortItemGroups(ItemGroupEntity entity, bool sort) => MergeAndSortItemGroups(new XElement(_ns + Constants.ItemGroupNode), entity, sort);

        /// <inheritdoc />
        public void SortPropertyGroups() => Sort(ModifiedDocument);

        private void ProcessConfigFiles(XElement element, string originalCaseIncludeValue)
        {
            if(!originalCaseIncludeValue.ToLower(CultureInfo.InvariantCulture).EndsWithAnyOf("packages.config", "app.config", "web.config"))
                return;

            if (originalCaseIncludeValue.ToLower(CultureInfo.InvariantCulture).EndsWith("web.config") &&
                element.Name.LocalName.Equals(Constants.ContentNode, StringComparison.InvariantCultureIgnoreCase))
            {
                Changes.Add($"{element.Name.LocalName}: changing to Content for {originalCaseIncludeValue}");
                element.Name = _ns + Constants.ContentNode;
            }

            if (element.Name.LocalName.Equals(Constants.NoneNode, StringComparison.InvariantCultureIgnoreCase))
            {
                Changes.Add($"{element.Name.LocalName}: changing to None for {originalCaseIncludeValue}");
                element.Name = _ns + Constants.NoneNode;
            }

            // these config files should never be copied
            var n = element.Element(_ns + Constants.CopyToOutputDirectoryElement);
            if (n == null)
                return;

            Changes.Add($"{element.Name.LocalName}: Removing CopyToOutputDirectory for {originalCaseIncludeValue}");
            n.Remove();
        }

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
