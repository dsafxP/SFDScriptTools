#!/usr/bin/env -S dotnet fsi --

open System
open System.IO

// Constants

[<Literal>]
let DllName = "SFD.GameScriptInterface.dll"

let defaultOutputPath = Path.Combine(".", "lib", DllName)

let homeDir = Environment.GetFolderPath Environment.SpecialFolder.UserProfile

/// Default locations to probe for the DLL, in order of preference.
let defaultSearchPaths: string list =
    [
      // Linux Flatpak Steam
      Path.Combine(
          homeDir,
          ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Superfighters Deluxe",
          DllName
      )
      // Linux native Steam
      Path.Combine(homeDir, ".local/share/Steam/steamapps/common/Superfighters Deluxe", DllName)
      // Windows Steam
      @"C:\Program Files (x86)\Steam\steamapps\common\Superfighters Deluxe\" + DllName ]

// Argument parsing

let printUsage () =
    printfn "Usage: SFDLibrarySetup.fsx [-f|--file <path>] [-o|--output <path>] [-h|--help]"
    printfn ""
    printfn "  -f, --file    Explicit path to %s (skips auto-search)" DllName
    printfn "  -o, --output  Output path for the symlink/copy (default: %s)" defaultOutputPath
    printfn "  -h, --help    Show this help message and exit"

let scriptArgs = fsi.CommandLineArgs |> List.ofArray

type ParsedArgs =
    { File: string option
      Output: string option
      Help: bool }

let rec parseArgs (args: string list) (acc: ParsedArgs) =
    match args with
    | [] -> acc
    | ("-f" | "--file") :: value :: rest -> parseArgs rest { acc with File = Some value }
    | ("-o" | "--output") :: value :: rest -> parseArgs rest { acc with Output = Some value }
    | ("-h" | "--help") :: rest -> parseArgs rest { acc with Help = true }
    | unknown :: rest ->
        eprintfn "Warning: Unknown argument '%s' (ignored)." unknown
        parseArgs rest acc

let parsed =
    parseArgs
        scriptArgs
        { File = None
          Output = None
          Help = false }

if parsed.Help then
    printUsage ()
    exit 0

let explicitFile = parsed.File
let outputPath = defaultArg parsed.Output defaultOutputPath

// Guard: don't run if the DLL already exists nested within the current dir

let currentDir = Directory.GetCurrentDirectory()

let existingNestedCopies =
    Directory.GetFiles(currentDir, DllName, SearchOption.AllDirectories)

if existingNestedCopies.Length > 0 then
    printfn "'%s' already exists within the current directory:" DllName

    for p in existingNestedCopies do
        printfn "  %s" p

    printfn "Nothing to do. Remove it first if you want to re-run this script."
    exit 0

// Locate the source DLL

let resolveSourcePath () : string option =
    match explicitFile with
    | Some path ->
        if File.Exists(path) then
            Some(Path.GetFullPath path)
        else
            eprintfn "Specified file via -f does not exist: %s" path
            None
    | None -> defaultSearchPaths |> List.tryFind File.Exists |> Option.map Path.GetFullPath

let sourcePath =
    match resolveSourcePath () with
    | Some p -> p
    | None ->
        eprintfn "Could not find '%s' in any of the default locations:" DllName

        for p in defaultSearchPaths do
            eprintfn "  %s" p

        eprintfn ""
        eprintfn "Please specify its location manually with -f <path>."
        exit 1

// Create output folder(s) and link/copy the DLL

let fullOutputPath = Path.GetFullPath outputPath
let outputDir = Path.GetDirectoryName fullOutputPath

if not (String.IsNullOrEmpty outputDir) && not (Directory.Exists outputDir) then
    Directory.CreateDirectory outputDir |> ignore
    printfn "Created directory: %s" outputDir

if File.Exists fullOutputPath then
    printfn "Output file already exists at: %s" fullOutputPath
    printfn "Nothing to do."
    exit 0

printfn "Found source DLL at: %s" sourcePath
printfn "Linking to:          %s" fullOutputPath

try
    File.CreateSymbolicLink(fullOutputPath, sourcePath) |> ignore
    printfn "Successfully created symlink."
with ex ->
    printfn "Symlink creation failed (%s)." ex.Message
    printfn "Falling back to copying the file instead..."

    try
        File.Copy(sourcePath, fullOutputPath, overwrite = false)
        printfn "Successfully copied the DLL."
    with copyEx ->
        eprintfn "Copy also failed: %s" copyEx.Message
        exit 1

printfn "Done."
