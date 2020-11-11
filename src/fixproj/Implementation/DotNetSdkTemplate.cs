using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using fixproj.Abstract;

namespace fixproj.Implementation
{
    public class DotNetSdkTemplate : BaseTemplate, IOperateOnProjectFiles
    {
        private readonly List<string> _listOfAllowedActions = new List<string> 
        { 
            Constants.Reference, Constants.ProjectReference, Constants.Folder, Constants.PackageReference, Constants.NoneNode, Constants.CompileNode, Constants.ContentNode
        };

        private readonly List<string> _listOfDirectoryBuildProperties = new List<string>
        {
            Constants.Authors, Constants.Company, Constants.Copyright, Constants.GenerateDocumentationFile, Constants.Product, Constants.AssemblyName, Constants.RootNamespace,
            Constants.GeneratePackageOnBuild, Constants.AutoGenerateBindingRedirects, Constants.GenerateBindingRedirectsOutputType, Constants.RestoreProjectStyle, Constants.PlatformTarget
        };

        /// <inheritdoc />
        public IList<string> Changes { get; } = new List<string>();

        /// <inheritdoc />
        public XDocument ModifiedDocument { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotNetSdkTemplate"/> class.
        /// </summary>
        /// <param name="file">The path of the processed file.</param>
        public DotNetSdkTemplate(string file) : base(file)
        {
            ModifiedDocument = XDocument.Load(file);

            Initialize(ModifiedDocument);
        }

        /// <inheritdoc />
        public List<ItemGroupEntity> FixContent()
        {
            ItemGroupElements.ForEach(x => x.Remove());

            FixPropertyGroups(ModifiedDocument.Root, Changes, _listOfDirectoryBuildProperties);

            //exclude elements which contain EmbeddedResource local name, its attribute contains .resx extension, and they are part of this project
            //by default, the new sdk will pick up default globbing pattern <EmbeddedResource Include="**\*.resx" />
            ItemGroupElements.Elements()
                .Where(element => element.Name.LocalName.Equals(Constants.EmbeddedResourceNode)
                                  && element.HasAttributes 
                                  && !string.IsNullOrWhiteSpace(element.IfAttributesContainExtension(".resx")))
                .Remove();

            // exclude elements which contain Compile local name and its attribute is Include
            // By default, the new SDK will pick up default globbing patterns <Compile Include="**\*.cs" />
            ItemGroupElements.Elements().Where(element => element.Name.LocalName.Equals(Constants.CompileNode) 
                                                          && element.HasAttributes 
                                                          && !string.IsNullOrWhiteSpace(element.AttributeValueByName(Constants.IncludeAttribute)))
                .Remove();

            // exclude elements which contain None local name and Remove attribute
            // these elements will be populated only if csproj file contains EmbeddedResources
            ItemGroupElements.Elements().Where(element => element.Name.LocalName.Equals(Constants.NoneNode) 
                                                          && !string.IsNullOrWhiteSpace(element.AttributeValueByName(Constants.RemoveAttribute)))
                .Remove();

            var itemGroupElement = new XElement(Constants.ItemGroupNode);
            ItemGroupElements.Elements().ForEach(element =>
            {
                var originalCaseIncludeValue = element.AttributeValueByName(Constants.IncludeAttribute);

                if (element.HasNoContent())
                {
                    Changes.Add($"{element.Name.LocalName}: removing all empty content from {originalCaseIncludeValue}");
                    element.MakeEmpty();
                }

                if(string.IsNullOrWhiteSpace(originalCaseIncludeValue))
                    return;

                FixCopyIssue(element.Element(Constants.CopyToOutputDirectoryElement), Changes);

                // if file contains EmbeddedResource or Content nodes, these nodes should be listed in None item groups as well
                // creates this group automatically
                if (element.Name.LocalName.Equals(Constants.EmbeddedResourceNode) || element.Name.LocalName.Equals(Constants.ContentNode))
                {
                    itemGroupElement.Add(new XElement(Constants.NoneNode,
                        new XAttribute(Constants.RemoveAttribute, element.AttributeValueByName(Constants.IncludeAttribute))));
                }
            });

            if(!itemGroupElement.HasNoContent())
                ItemGroupElements.Add(itemGroupElement);

            return ItemGroupElements
                .SelectMany(x => x.Elements())
                .ToLookup(x => x.Name)
                .OrderBy(x => x.Key.LocalName)
                .Select(x => new ItemGroupEntity { LocalName = x.Key.LocalName, Element = new List<XElement>(x) })
                .ToList();
        }

        /// <inheritdoc />
        public void DeleteDuplicates(ItemGroupEntity entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var attributeName = Constants.IncludeAttribute;

            if (entity.LocalName.Equals(Constants.CompileNode))
            {
                attributeName = Constants.RemoveAttribute;
            }

            // additional check for None local name because it can contain either Remove or Include attribute
            // <None Include="..\SharedAppSettings.config" Link="SharedAppSettings.config">
            // <None Remove="InvoicePdfDomain\Reports\InvoiceTemplate-AccountsPayable.rdlc" />
            if (entity.LocalName.Equals(Constants.NoneNode))
            {
                attributeName = entity.Element.Select(x => x.FirstAttribute).FirstOrDefault()?.Name.LocalName;
            }

            DeleteDuplicatesBasedOnAttribute(entity, Changes, x => x.AttributeValueByName(attributeName));
        }

        /// <inheritdoc />
        public void DeleteReferencesToNonExistentFiles(ItemGroupEntity entity)
        {
            if (!_listOfAllowedActions.Contains(entity.LocalName))
                ItemGroupElements.Elements()
                    .Where(x => x.Name.LocalName.Equals(entity.LocalName))
                    .ForEach(x =>
                    {
                        Changes.Add($"{entity.LocalName}: removed to {x.AttributeValueByName(Constants.IncludeAttribute)} because it doesn't exist");
                        entity.Element.Remove(x);
                    });
        }

        /// <inheritdoc />
        public void MergeAndSortItemGroups(ItemGroupEntity entity, bool sort) => MergeAndSortItemGroups(new XElement(Constants.ItemGroupNode), entity, sort);

        /// <inheritdoc />
        public void SortPropertyGroups() => Sort(ModifiedDocument);
    }
}
