using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FixProjects
{
    internal static class Extensions
    {
        /// <summary>
        ///     Finds attribute value by name.
        /// </summary>
        /// <param name="element">XElement.</param>
        /// <param name="attributeName">Attribute name.</param>
        /// <returns>An attribute value.</returns>
        internal static string AttributeValueByName(this XElement element, string attributeName)
        {
            if (element == null || !element.HasAttributes ||
                string.IsNullOrWhiteSpace(element.Attribute(attributeName)?.Value)) return null;

            return element.Attribute(attributeName)?.Value;
        }

        /// <summary>
        ///     Finds element by local name.
        /// </summary>
        /// <param name="element">XElement.</param>
        /// <param name="localName">Local name.</param>
        /// <returns>A collection of XElement.</returns>
        internal static IEnumerable<XElement> ElementsByLocalName(this XElement element, string localName)
        {
            return element.Elements().Where(x => x.Name.LocalName == localName);
        }

        /// <summary>
        ///     Checks if string is a member of input array.
        /// </summary>
        /// <param name="subject">String.</param>
        /// <param name="suffixes">Array of string.</param>
        /// <returns>Boolean.</returns>
        internal static bool EndsWithAnyOf(this string subject, params string[] suffixes)
        {
            return suffixes.Any(subject.EndsWith);
        }

        /// <summary>
        ///     Returns attribute name based on local name.
        /// </summary>
        /// <param name="localName">Local name.</param>
        /// <returns>Attribute name.</returns>
        internal static string GetAttributeName(this string localName)
        {
            if (string.IsNullOrWhiteSpace(localName)) return null;

            var attributeName = Constants.IncludeAttribute;

            if (localName.Equals(Constants.CompileNode)) attributeName = Constants.RemoveAttribute;

            return attributeName;
        }

        /// <summary>
        ///     Checks if node contains value.
        /// </summary>
        /// <param name="element">XElement.</param>
        /// <returns>Boolean.</returns>
        internal static bool HasNoContent(this XElement element)
        {
            return string.IsNullOrWhiteSpace(element.Value) && !element.HasElements;
        }

        /// <summary>
        ///     Checks if attribute value contains provided extension.
        /// </summary>
        /// <param name="element">XElement.</param>
        /// <param name="extension">Extension.</param>
        /// <returns>An attribute value.</returns>
        internal static string IfAttributesContainExtension(this XElement element, string extension)
        {
            return element.Attributes().FirstOrDefault(x => x.Value.EndsWith(extension))?.Value;
        }

        /// <summary>
        ///     Makes element as empty node.
        /// </summary>
        /// <param name="element">XElement.</param>
        internal static void MakeEmpty(this XElement element)
        {
            element.ReplaceNodes(null);
        }
    }
}