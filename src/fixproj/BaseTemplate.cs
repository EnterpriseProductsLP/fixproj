using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace fixproj
{
    public class BaseTemplate
    {
        /// <summary>
        /// Represents a collection of ItemGroup elements.
        /// </summary>
        protected List<XElement> ItemGroupElements { get; set; }
        
        /// <summary>
        /// Represents the last inserted node. 
        /// </summary>
        protected XNode InsertedAt { get; set; }
        
        /// <summary>
        /// Csproj file path.
        /// </summary>
        protected string FilePath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseTemplate"/> class.
        /// </summary>
        /// <param name="filePath">Csproj filePath</param>
        protected BaseTemplate(string filePath) => FilePath = filePath;

        /// <summary>
        /// Initialize properties.
        /// </summary>
        /// <param name="document">XDocument.</param>
        protected void Initialize(XDocument document)
        {
            ItemGroupElements = document.Root.ElementsByLocalName(Constants.ItemGroupNode).ToList();
            InsertedAt = ItemGroupElements.First().PreviousNode;
        }

        /// <summary>
        /// Fixes property group elements.
        /// </summary>
        /// <param name="element">XElement.</param>
        /// <param name="changes">A collection of created changes.</param>
        /// <param name="propertiesToDelete">A collection of properties which should not exist in csproj file.</param>
        protected void FixPropertyGroups(XElement element, IList<string> changes, IList<string> propertiesToDelete = null) =>
            element.ElementsByLocalName(Constants.PropertyGroupNode)
                .ForEach(property =>
                {
                    // make elements with no real content into empty ones
                    if (property.HasNoContent())
                    {
                        changes.Add($"Removing empty content from property {property.Name}.");
                        property.MakeEmpty();
                        return;
                    }

                    if (propertiesToDelete == null)
                        return;

                    var propertyNodes = property.Elements().ToList();

                    propertyNodes?.ForEach(node =>
                    {
                        if(propertiesToDelete.Contains(node.Name.LocalName))
                            property.Elements().Where(x => x.Name.LocalName == node.Name.LocalName).Remove();
                    });
                });

        /// <summary>
        /// Sorts elements.
        /// </summary>
        /// <param name="element">XElement.</param>
        /// <param name="sortAttributes">A flag indicating whether we need to sort the element.</param>
        protected void Sort(XElement element, bool sortAttributes = true)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            if (sortAttributes)
            {
                var atts = element.Attributes().OrderBy(a => a.ToString()).ToList();
                atts.RemoveAll(x => true);
                atts.ForEach(element.Add);
            }

            var sorted = element.Elements().OrderBy(e => e.Name.ToString()).ToList();
            if (!element.HasElements)
                return;

            element.RemoveNodes();
            sorted.ForEach(c => Sort(c));
            sorted.ForEach(element.Add);
        }

        /// <summary>
        /// Deletes duplicated elements.
        /// </summary>
        /// <param name="entity">ItemGroupEntity.</param>
        /// <param name="changes">A collection of created changes.</param>
        /// <param name="func">Group by delegate.</param>
        protected void DeleteDuplicatesBasedOnAttribute(ItemGroupEntity entity, IList<string> changes, Func<XElement, string> func) =>
            entity.Element
                // duplicates are those that have the same Include more than one time
                .GroupBy(func)
                .Where(g => g.Count() > 1)
                .Select(g =>
                {
                    // skip the first one, the rest are the actual duplicates
                    var dupesOfFirst = g.Skip(1).ToList();
                    changes.Add($"{entity.LocalName}: removed {dupesOfFirst.Count} dupes of {dupesOfFirst.First().Attribute("Include")?.Value}");
                    return dupesOfFirst;
                })
                // remove the ones we found from the original collection
                .SelectMany(x => x)
                .ForEach(x => entity.Element.Remove(x));

        /// <summary>
        /// Sorts property groups.
        /// </summary>
        /// <param name="modifiedDocument">XDocument.</param>
        protected void Sort(XDocument modifiedDocument) =>
            modifiedDocument.Root.ElementsByLocalName(Constants.PropertyGroupNode).ForEach(e => Sort(e));

        /// <summary>
        /// Inserts elements into modified document.
        /// </summary>
        /// <param name="groupToAdd">XElement.</param>
        /// <param name="entity">ItemGroupEntity.</param>
        /// <param name="sort">Boolean.</param>
        protected void MergeAndSortItemGroups(XElement groupToAdd, ItemGroupEntity entity, bool sort)
        {
            if(groupToAdd == null)
                throw new ArgumentNullException(nameof(groupToAdd));

            if (sort)
            {
                groupToAdd.Add(entity.Element.OrderBy(x => x.AttributeValueByName(Constants.IncludeAttribute)));
            }
            else
            {
                groupToAdd.Add(entity.Element);
            }

            InsertedAt.AddAfterSelf(groupToAdd);
            InsertedAt = groupToAdd;
        }

        /// <summary>
        /// Fixes CopyToOutputDirectory node if its value is set to Always.
        /// </summary>
        /// <param name="element">XElement.</param>
        /// <param name="changes">A collection of created changes.</param>
        protected void FixCopyIssue(XElement element, IList<string> changes)
        {
            if(element?.Value.Contains(Constants.AlwaysNodeValue) != true)
                return;

            element.Value = Constants.PreserveNewestNodeValue;
            changes.Add($"{element.Name.LocalName}: Changing {element.AttributeValueByName(Constants.IncludeAttribute)} to PreserveNewest instead of CopyAlways");
        }
    }
}
