using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FixProjects.Abstract;

namespace FixProjects.Implementation
{
    public class DotNetSdkTemplate : BaseTemplate, IOperateOnProjectFiles
    {
        private readonly List<string> _listOfAllowedActions = new List<string>
        {
            Constants.Reference, Constants.ProjectReference, Constants.Folder, Constants.PackageReference,
            Constants.NoneNode, Constants.CompileNode, Constants.ContentNode, Constants.EmbeddedResourceNode,
            Constants.Folder
        };

        private readonly List<string> _listOfDirectoryBuildProperties = new List<string>
        {
            Constants.Authors, Constants.Company, Constants.Copyright, Constants.GenerateDocumentationFile,
            Constants.Product, Constants.AssemblyName, Constants.RootNamespace,
            Constants.GeneratePackageOnBuild, Constants.AutoGenerateBindingRedirects,
            Constants.GenerateBindingRedirectsOutputType, Constants.RestoreProjectStyle, Constants.PlatformTarget
        };

        /// <summary>
        ///     Initializes a new instance of the <see cref="DotNetSdkTemplate" /> class.
        /// </summary>
        /// <param name="file">The path of the processed file.</param>
        public DotNetSdkTemplate(string file)
            : base(file)
        {
            ModifiedDocument = XDocument.Load(file);

            Initialize(ModifiedDocument);
        }

        /// <inheritdoc />
        public IList<string> Changes { get; } = new List<string>();

        /// <inheritdoc />
        public XDocument ModifiedDocument { get; }

        /// <inheritdoc />
        public void DeleteDuplicates(ItemGroupEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var attributeName = entity.Element.FirstOrDefault().GetAttributeName();

            DeleteDuplicatesBasedOnAttribute(entity, Changes, x => x.AttributeValueByName(attributeName));
        }

        /// <inheritdoc />
        public void DeleteReferencesToNonExistentFiles(ItemGroupEntity entity)
        {
            if (!_listOfAllowedActions.Contains(entity.LocalName))
                ItemGroupElements.Elements()
                    .Where(x => x.Name.LocalName.Equals(entity.LocalName))
                    .ForEach(
                        x =>
                        {
                            Changes.Add(
                                $"{entity.LocalName}: removed to {x.AttributeValueByName(Constants.IncludeAttribute)} because it doesn't exist");
                            entity.Element.Remove(x);
                        });
        }

        /// <inheritdoc />
        public List<ItemGroupEntity> FixContent()
        {
            ItemGroupElements.ForEach(x => x.Remove());

            FixPropertyGroups(ModifiedDocument.Root, Changes, _listOfDirectoryBuildProperties);

            // exclude elements which contain EmbeddedResource local name, its attribute contains .resx extension, and they are part of this project
            // by default, the new sdk will pick up default globbing pattern <EmbeddedResource Include="**\*.resx" />
            ItemGroupElements.Elements()
                .Where(
                    element => element.Name.LocalName.Equals(Constants.EmbeddedResourceNode)
                               && element.HasAttributes
                               && !string.IsNullOrWhiteSpace(element.IfAttributesContainExtension(".resx")))
                .Remove();

            // exclude elements which contain Compile local name and its attribute is Include
            // By default, the new SDK will pick up default globbing patterns <Compile Include="**\*.cs" />
            ItemGroupElements.Elements().Where(
                    element => element.Name.LocalName.Equals(Constants.CompileNode)
                               && element.HasAttributes
                               && !string.IsNullOrWhiteSpace(element.AttributeValueByName(Constants.IncludeAttribute)))
                .Remove();

            // exclude elements which contain None local name and Remove attribute
            // these elements will be populated only if csproj file contains EmbeddedResources
            ItemGroupElements.Elements().Where(
                    element => element.Name.LocalName.Equals(Constants.NoneNode)
                               && !string.IsNullOrWhiteSpace(element.AttributeValueByName(Constants.RemoveAttribute)))
                .Remove();

            var noneRemoveElements = new XElement(Constants.ItemGroupNode);
            ItemGroupElements.Elements().ForEach(
                element =>
                {
                    if (element.HasNoContent())
                        element.MakeEmpty();

                    FixCopyIssue(element.Element(Constants.CopyToOutputDirectoryElement), Changes);

                    // if file contains EmbeddedResource or Content nodes, these nodes should be listed in None item groups as well
                    // creates this group automatically
                    if (element.Name.LocalName.Equals(Constants.EmbeddedResourceNode) || element.Name.LocalName.Equals(Constants.ContentNode))
                        InsertIntoNoneRemoveElements(noneRemoveElements, element.AttributeValueByName(element.GetAttributeName()));
                });

            var itemsForProcessing = new List<ItemGroupEntity>();

            if (!noneRemoveElements.HasNoContent()) 
                itemsForProcessing.Add(new ItemGroupEntity { LocalName = Constants.NoneNode, Element = noneRemoveElements.Elements().ToList() });

            itemsForProcessing.AddRange(ItemGroupElements
                .SelectMany(x => x.Elements())
                .ToLookup(x => new { x.Name, x.FirstAttribute?.Name.LocalName })
                .OrderBy(x => x.Key.Name.LocalName)
                .Select(x => new ItemGroupEntity { LocalName = x.Key.Name.LocalName, Element = new List<XElement>(x) })
                .ToList());

            return itemsForProcessing;
        }

        /// <inheritdoc />
        public void MergeAndSortItemGroups(ItemGroupEntity entity, bool sort)
        {
            MergeAndSortItemGroups(new XElement(Constants.ItemGroupNode), entity, sort);
        }

        private void InsertIntoNoneRemoveElements(XElement noneRemoveElement, string attributeValue)
        {
            if(noneRemoveElement == null) throw new ArgumentNullException(nameof(noneRemoveElement));

            if(string.IsNullOrWhiteSpace(attributeValue) || attributeValue.Contains(".config")) return;

            noneRemoveElement.Add(
                new XElement(
                    Constants.NoneNode,
                    new XAttribute(Constants.RemoveAttribute, attributeValue)));

            Changes.Add($"Create new none node with Remove attribute name and attribute value {attributeValue}");
        }
    }
}