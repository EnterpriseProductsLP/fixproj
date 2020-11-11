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
        /// Inserts elements into modified document.
        /// </summary>
        /// <param name="groupToAdd">XElement.</param>
        /// <param name="entity">ItemGroupEntity.</param>
        /// <param name="sort">Boolean.</param>
        protected void MergeAndSortItemGroups(XElement groupToAdd, ItemGroupEntity entity, bool sort)
        {
            if(groupToAdd == null)
                throw new ArgumentNullException(nameof(groupToAdd));

            var attributeName = entity.LocalName.GetAttributeName();

            if (sort)
            {
                groupToAdd.Add(entity.Element.OrderBy(x => x.AttributeValueByName(attributeName)));
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
