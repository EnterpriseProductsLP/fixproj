using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace fixproj
{
    internal static class Extensions
    {
        internal static bool EndsWithAnyOf(this string subject, params string[] suffixes) => suffixes.Any(subject.EndsWith);

        internal static IEnumerable<XElement> ElementsByLocalName(this XElement element, string localName) => element.Elements().Where(x => x.Name.LocalName == localName);

        internal static bool HasNoContent(this XElement element) => string.IsNullOrWhiteSpace(element.Value) && !element.HasElements;

        internal static void MakeEmpty(this XElement element) => element.ReplaceNodes(null);

        internal static string IfAttributesContainExtension(this XElement element, string extension) => element.Attributes().FirstOrDefault(x => x.Value.EndsWith(extension))?.Value;

        internal static string AttributeValueByName(this XElement element, string attributeName)
        {
            if (element == null || !element.HasAttributes || string.IsNullOrWhiteSpace(element.Attribute(attributeName)?.Value))
            {
                return null;
            }

            return element.Attribute(attributeName)?.Value;
        }
    }
}