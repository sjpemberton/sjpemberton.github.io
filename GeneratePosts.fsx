#I "packages/FSharp.Formatting.2.6.2/lib/net40"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"

open System.IO
open FSharp.Literate

let relative subdir = Path.Combine(__SOURCE_DIRECTORY__, subdir)
let EnumerateFiles path searchPattern = Directory.EnumerateFiles(path, searchPattern)

//TODO - check for modification
let generateTemplate filePath = 
    //Cheating as I know I will include the YAML as the last section of file (pre-pended by a hide tag)
    let tmpPath = Path.GetFileNameWithoutExtension(filePath) + "tmp"
    File.WriteAllLines(tmpPath, 
                       Array.concat (seq [ [| "---" |];
                                           File.ReadAllLines filePath 
                                           |> Array.rev
                                           |> Seq.skip 1
                                           |> Seq.takeWhile (fun x -> x <> "---")
                                           |> Seq.toArray;
                                           [| "---"; "{document}"; "{tooltips}"; |] ]))
    tmpPath //Return tmp path for later deletion

let processFile fullPath outPath = 
    let file = Path.GetFileNameWithoutExtension(fullPath)
    let outFile = outPath + file + ".html"
    let tmpPath = generateTemplate fullPath
    Literate.ProcessScriptFile(fullPath, tmpPath, output = outFile, format = OutputKind.Html)
    File.Delete tmpPath

EnumerateFiles (relative "fsharp-posts") "*fsx" |> Seq.iter (fun f -> processFile f (relative "_posts/"))