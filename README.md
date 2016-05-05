# fixproj
Fixproj performs various cleanups and other operations on Visual Studio **.csproj** files, mostly to reduce merge conflicts.

Because **fixproj** is a command-line utility you can run it with `psake`, as part of a pre-commit git hook, whenever you feel like it, etc.
## Features

* Removes duplicate items.
* Normalizes the case of the items to the case of the corresponding files on disk.
* Adds references to content on disk such as C# files, embedded resources, etc.
* Removes references to missing files
* Sorts properties
* Sorts nodes that have an `Include` attribute such as `Compile`, `Reference`, and `EmbeddedResource` nodes.
* And more!

## Usage

`fixproj [`*`options`*`] -t ` *`directory`*` -m ` *`filemask`*

Options include

    -add, -ac, -a       Add references to C# files that exist on disk
    -backup, -b         Create a backup. Yes, .BAK is back!
    -content, -act      Add references to content files that exist on disk
    -dedupe, -dd        Deduplicate nodes that have the
                        same value for the Include attribute
    -delete, -d         Delete references to files that don't exist
    -embed, -ae         Add references to embedded resources that exist on disk
    -fixcontent, -fix   Fixes content nodes so they  don't copy,
                        copy if newer, etc. correctly based on type
    -mask, -m           Project search mask
    -preview, -p        Preview the changes that would
                        be made without making them
    -recursive, -r      Search the target directory recursively
                        for project files that match the mask
    -ripstylecop,
    -rip                Rips stylecop out.
    -sort, -s           Sort nodes by Include attribute
    -target, -t         Directory containing the project file, or
                        the root directory to search when recursive
    -unripstylecop,
    -unrip              Puts stylecop back in
    -verbose, -v        Verbose output


### Examples ###
Sort all the nodes of all the `.csproj` files of all the projects in the `Source` directory.

    fixproj -r -s -v -t Source -m *.csproj

Preview the results if you were to perform various content fixups of the nodes in a web project, delete duplicates and delete references to non-existent files.

    fixproj -v -dd -d -p -t Source\Web -m Web.csproj

## Tested With
* Visual Studio 2015
* Visual Studio 2013