using Plossum.CommandLine;

namespace FixProjects
{
    [CommandLineManager(ApplicationName = "fixproj")]
    [CommandLineOptionGroup("commands", Name = "Commands", Require = OptionGroupRequirement.None)]
    public class CommandLineOptions
    {
        [CommandLineOption(Aliases = "dd", Name = "dedupe",
            Description = "Deduplicate nodes that have the same value for the Include attribute", MinOccurs = 0,
            MaxOccurs = 1)]
        public bool DeleteDuplicates { get; set; }

        [CommandLineOption(Aliases = "d", Name = "delete", Description = "Delete references to files that don't exist",
            MinOccurs = 0, MaxOccurs = 1)]
        public bool DeleteReferencesToNonExistentFiles { get; set; }

        [CommandLineOption(Aliases = "m", Name = "mask", Description = "Project search mask", MinOccurs = 1,
            MaxOccurs = 1)]
        public string FileMask { get; set; }

        [CommandLineOption(Aliases = "fix", Name = "fixcontent",
            Description = "Fixes content nodes so they  don't copy, copy if newer, etc. correctly based on type",
            MinOccurs = 0, MaxOccurs = 1)]
        public bool FixContent { get; set; }

        [CommandLineOption(Aliases = "p", Name = "preview",
            Description = "Preview the changes that would be made without making them", MinOccurs = 0, MaxOccurs = 1)]
        public bool Preview { get; set; }

        [CommandLineOption(Aliases = "r", Name = "recursive",
            Description = "Search the target directory recursively for project files that match the mask",
            MinOccurs = 0, MaxOccurs = 1)]
        public bool Recursive { get; set; }

        [CommandLineOption(Aliases = "s", Name = "sort", Description = "Sort nodes by Include attribute", MinOccurs = 0,
            MaxOccurs = 1)]
        public bool Sort { get; set; }

        [CommandLineOption(Aliases = "t", Name = "target",
            Description = "Directory containing the project file, or the root directory to search when recursive",
            MinOccurs = 1, MaxOccurs = 1)]
        public string TargetDirectory { get; set; }

        [CommandLineOption(Aliases = "v", Name = "verbose", Description = "Verbose output", MinOccurs = 0,
            MaxOccurs = 1)]
        public bool Verbose { get; set; }
    }
}