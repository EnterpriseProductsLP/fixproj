﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FixProjects.Abstract;

namespace FixProjects.Implementation
{
    public class ProjectFileOperationManager : IManageProjectFileOperations
    {
        private readonly Dictionary<string, XDocument> _listOfChangedFiles = new Dictionary<string, XDocument>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="ProjectFileOperationManager" /> class.
        /// </summary>
        /// <param name="commandLineOptions">Options.</param>
        public ProjectFileOperationManager(CommandLineOptions commandLineOptions)
        {
            CommandLineOptions = commandLineOptions;
        }

        public CommandLineOptions CommandLineOptions { get; set; }
        
        /// <inheritdoc />
        public int Run()
        {
            var searchOption = CommandLineOptions.Recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory
                .GetFiles(
                    CommandLineOptions.TargetDirectory,
                    CommandLineOptions.FileMask,
                    searchOption)
                .Where(file => !file.Contains(@"\packages\"));

            if (CommandLineOptions.Preview) Console.WriteLine("*** PREVIEW ONLY! DON'T PANIC!\n");

            List<ItemGroupEntity> itemGroupEntities = null;
            foreach (var file in files)
            {
                var originalContent = XDocument.Load(file);
                var templateInstance = new TemplateFactory().Build(file);

                Console.WriteLine($"Processing: {file}");

                if (CommandLineOptions.FixContent) itemGroupEntities = templateInstance.FixContent();

                if (CommandLineOptions.Sort) templateInstance.SortPropertyGroups();

                itemGroupEntities?.ForEach(
                    itemGroup =>
                    {
                        if (CommandLineOptions.DeleteDuplicates) templateInstance.DeleteDuplicates(itemGroup);

                        if (CommandLineOptions.DeleteReferencesToNonExistentFiles)
                            templateInstance.DeleteReferencesToNonExistentFiles(itemGroup);

                        templateInstance.MergeAndSortItemGroups(itemGroup, CommandLineOptions.Sort);

                        if (CommandLineOptions.Verbose) templateInstance.Verbose();
                    });

                if (!AreEqual(originalContent, templateInstance.ModifiedDocument))
                {
                    Console.WriteLine("  {0} CHANGES\n", templateInstance.Changes.Count);
                    _listOfChangedFiles.Add(file, templateInstance.ModifiedDocument);
                }
            }

            SaveChanges();

            return _listOfChangedFiles.Count;
        }

        private bool AreEqual(XDocument originalFile, XDocument modifiedFile)
        {
            if (!XNode.DeepEquals(originalFile, modifiedFile)) return false;

            if (!CommandLineOptions.Preview) Console.WriteLine("  NO CHANGES\n");

            return true;
        }

        private void SaveChanges()
        {
            if (!CommandLineOptions.Preview)
            {
                Console.WriteLine("\nSaving {0} sanitized files.", _listOfChangedFiles.Count);
                foreach (var file in _listOfChangedFiles) file.Value.Save(file.Key);

                Console.WriteLine("Saved {0} sanitized files.", _listOfChangedFiles.Count);
            }
            else
            {
                Console.WriteLine(
                    "\nPreview: {0} files would have been changed given your criteria.",
                    _listOfChangedFiles.Count);
            }
        }
    }
}