using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FixProjects.Abstract
{
    /// <summary>
    ///     Provides operations on project files.
    /// </summary>
    internal interface IOperateOnProjectFiles
    {
        /// <summary>
        ///     Gets the list of logged changes.
        /// </summary>
        IList<string> Changes { get; }

        /// <summary>
        ///     Gets the final version of project file.
        /// </summary>
        XDocument ModifiedDocument { get; }

        /// <summary>
        ///     Deletes duplicate nodes.
        /// </summary>
        void DeleteDuplicates(ItemGroupEntity entity);

        /// <summary>
        ///     Deletes references to non existent files.
        /// </summary>
        void DeleteReferencesToNonExistentFiles(ItemGroupEntity entity);

        /// <summary>
        ///     Fix project nodes.
        /// </summary>
        List<ItemGroupEntity> FixContent();

        /// <summary>
        ///     Creates and sorts a final version of document.
        /// </summary>
        void MergeAndSortItemGroups(ItemGroupEntity entity, bool sort);

        /// <summary>
        ///     Sorts property nodes.
        /// </summary>
        void SortPropertyGroups()
        {
            ModifiedDocument.Root.ElementsByLocalName(Constants.PropertyGroupNode).ForEach(e => Sort(e));

            void Sort(XElement element, bool sortAttributes = true)
            {
                if (element == null) throw new ArgumentNullException(nameof(element));

                if (sortAttributes)
                {
                    var sortedAttributes = element.Attributes().OrderBy(a => a.ToString()).ToList();
                    sortedAttributes.RemoveAll(x => true);
                    sortedAttributes.ForEach(element.Add);
                }

                var sorted = element.Elements().OrderBy(e => e.Name.ToString()).ToList();
                if (!element.HasElements) return;

                element.RemoveNodes();
                sorted.ForEach(c => Sort(c));
                sorted.ForEach(element.Add);
            }
        }

        /// <summary>
        ///     Writes detailed logging.
        /// </summary>
        void Verbose()
        {
            Changes.ForEach(x => Console.WriteLine(x));
        }
    }
}