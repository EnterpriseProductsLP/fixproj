using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace fixproj
{
    public class BaseTemplate
    {
        protected List<XElement> ItemGroupElements { get; set; }
        protected XNode InsertedAt { get; set; }

        protected void Initialize(XDocument document)
        {
            ItemGroupElements = document.Root.ElementsByLocalName(Constants.ItemGroupNode).ToList();
            InsertedAt = ItemGroupElements.First().PreviousNode;
        }

        protected void FixPropertyGroups(XElement element, IList<string> changes) =>
            element.ElementsByLocalName(Constants.PropertyGroupNode)
                .ForEach(property =>
                {
                    // make elements with no real content into empty ones
                    if (!property.HasNoContent())
                        return;

                    changes.Add($"Removing empty content from property {property.Name}.");
                    property.MakeEmpty();
                });

        protected void Sort(XElement source, bool sortAttributes = true)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (sortAttributes)
            {
                var atts = source.Attributes().OrderBy(a => a.ToString()).ToList();
                atts.RemoveAll(x => true);
                atts.ForEach(source.Add);
            }

            var sorted = source.Elements().OrderBy(e => e.Name.ToString()).ToList();
            if (!source.HasElements)
                return;

            source.RemoveNodes();
            sorted.ForEach(c => Sort(c));
            sorted.ForEach(source.Add);
        }

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

        protected void Sort(XDocument modifiedDocument) =>
            modifiedDocument.Root.ElementsByLocalName("PropertyGroup").ForEach(e => Sort(e));
    }
}
