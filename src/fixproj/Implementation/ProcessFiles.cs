using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using fixproj.Abstract;

namespace fixproj.Implementation
{
    public class ProcessFiles : IProcess
    {
        private readonly Dictionary<string, XDocument> _listOfChangedFiles = new Dictionary<string, XDocument>();

        public Options Options { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessFiles"/> class.
        /// </summary>
        /// <param name="options">Options.</param>
        public ProcessFiles(Options options)
        {
            Options = options;
        }

        /// <inheritdoc />
        public int Run()
        {
            var files = Directory
                .GetFiles(Options.TargetDirectory, Options.FileMask,
                    Options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(file => !file.Contains(@"\packages\"));

            if (Options.Preview)
            {
                Console.WriteLine("*** PREVIEW ONLY! DON'T PANIC!\n");
            }

            List<ItemGroupEntity> itemGroupEntities = null;
            foreach (var file in files)
            {
                var originalContent = XDocument.Load(file);
                var templateInstance = new TemplateFactory().Build(file);

                Console.WriteLine($"Processing: {file}");

                if (Options.FixContent)
                {
                    itemGroupEntities = templateInstance.FixContent();
                }

                if (Options.Sort)
                {
                    templateInstance.SortPropertyGroups();
                }

                itemGroupEntities?.ForEach(itemGroup =>
                {
                    if (Options.DeleteDuplicates)
                    {
                        templateInstance.DeleteDuplicates(itemGroup);
                    }

                    if (Options.DeleteReferencesToNonExistentFiles)
                    {
                        templateInstance.DeleteReferencesToNonExistentFiles(itemGroup);
                    }

                    templateInstance.MergeAndSortItemGroups(itemGroup, Options.Sort);

                    if (Options.Verbose)
                    {
                        templateInstance.Verbose();
                    }
                });

                if (!AreEqual(originalContent, templateInstance.ModifiedDocument))
                {
                    _listOfChangedFiles.Add(file, templateInstance.ModifiedDocument);
                }

                Console.WriteLine("  {0} CHANGES\n", templateInstance.Changes.Count);
            }

            SaveChanges();

            return _listOfChangedFiles.Count;
        }

        private bool AreEqual(XDocument originalFile, XDocument modifiedFile)
        {
            if (!XNode.DeepEquals(originalFile, modifiedFile))
            {
                return false;
            }

            if (!Options.Preview)
            {
                Console.WriteLine("  NO CHANGES\n");
            }

            return true;
        }

        private void SaveChanges()
        {
            if (!Options.Preview)
            {
                Console.WriteLine("\nSaving {0} sanitized files.", _listOfChangedFiles.Count);
                foreach (var file in _listOfChangedFiles)
                {
                    file.Value.Save(file.Key);
                }

                Console.WriteLine("Saved {0} sanitized files.", _listOfChangedFiles.Count);
            }
            else
            {
                Console.WriteLine("\nPreview: {0} files would have been changed given your criteria.",
                    _listOfChangedFiles.Count);
            }
        }
    }
}
