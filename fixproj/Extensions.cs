using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace fixproj
{
    public static class Extensions
    {
        public static bool EndsWithAnyOf(this string subject, params string[] suffixes)
        {
            return suffixes.Any(subject.EndsWith);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var seenKeys = new HashSet<TKey>();
            return source.Where(element => seenKeys.Add(keySelector(element)));
        }

        public static IEnumerable<XElement> ElementsByLocalName(this XElement element, string localName)
        {
            return element.Elements().Where(x => x.Name.LocalName == localName);
        }

        public static IEnumerable<XElement> DescendantsByLocalName(this XElement element, string localName)
        {
            return element.Descendants().Where(x => x.Name.LocalName == localName);
        }

        public static bool HasNoContent(this XElement element)
        {
            return string.IsNullOrWhiteSpace(element.Value) && !element.HasElements;
        }

        public static void MakeEmpty(this XElement element)
        {
            element.ReplaceNodes(null);
        }

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var v in source)
                action(v);
        }

        public static void Sort(this XElement source, bool sortAttributes = true)
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
            sorted.ForEach(c => c.Sort());
            sorted.ForEach(source.Add);
        }
    }
}