﻿namespace YAVSRG.CLI.Features

open System.IO
open System.IO.Compression
open System.Diagnostics
open System.Runtime.InteropServices
open YAVSRG.CLI.Utils

module Play =

    let GAME_FOLDER = Path.Combine(YAVSRG_PATH, "GAME")

    let detect_build_info() =
        let arch = RuntimeInformation.OSArchitecture
        match arch with
        | Architecture.X64 when System.OperatingSystem.IsWindows() ->
            Releases.BuildPlatformInfo.WINDOWS_X64
        | Architecture.X64 when System.OperatingSystem.IsLinux() ->
            Releases.BuildPlatformInfo.LINUX_X64
        | Architecture.Arm64 when System.OperatingSystem.IsMacOS() ->
            Releases.BuildPlatformInfo.OSX_ARM64
        | _ -> failwithf "Your platform (%O) is not supported! Maybe complain in the discord?" arch

    let update () =
        exec "git" "checkout main"
        exec "git" "pull"
        exec "git" "fetch --tags"

        let tag_digest = eval "git" "rev-list --tags --max-count=1"
        let tag_name = eval "git" (sprintf "describe --tags \"%s\"" tag_digest)
        exec "git" (sprintf "checkout %s" tag_name)

        try
            Directory.CreateDirectory GAME_FOLDER |> ignore
            let build_info = detect_build_info()
            Releases.build_platform build_info
            ZipFile.ExtractToDirectory(Path.Combine(YAVSRG_PATH, "interlude", "releases", sprintf "Interlude-%s.zip" build_info.Name), GAME_FOLDER, true)
        with err -> printfn "Error creating GAME folder: %O" err

        exec "git" "checkout main"

    let play () =

        if not (Directory.Exists GAME_FOLDER && Directory.EnumerateFileSystemEntries GAME_FOLDER |> Seq.isEmpty |> not) then
            update()

        if File.Exists(Path.Combine(GAME_FOLDER, "Interlude.exe")) then
            Process.Start(Path.Combine(GAME_FOLDER, "Interlude.exe")).WaitForExit()
        elif File.Exists(Path.Combine(GAME_FOLDER, "Interlude")) then
            Process.Start(Path.Combine(GAME_FOLDER, "Interlude")).WaitForExit()
        else
            printfn "Your GAME folder is missing an Interlude executable, run `yavsrg update` to fix it"

    let debug_run () =
        exec_at INTERLUDE_SOURCE_PATH "dotnet" "build --configuration Debug -v q"
        try
            let build_info = detect_build_info()
            File.Copy(
                Path.Combine(YAVSRG_PATH, "engine", "lib", build_info.RuntimeId, build_info.BassLibraryFile),
                Path.Combine(INTERLUDE_SOURCE_PATH, "bin", "Debug", "net8.0", build_info.BassLibraryFile),
                true
            )

            File.Copy(
                Path.Combine(YAVSRG_PATH, "engine", "lib", build_info.RuntimeId, build_info.BassFxLibraryFile),
                Path.Combine(INTERLUDE_SOURCE_PATH, "bin", "Debug", "net8.0", build_info.BassFxLibraryFile),
                true
            )
        with err -> printfn "Error detecting platform: %O" err
        exec_at (Path.Combine(INTERLUDE_SOURCE_PATH, "bin", "Debug", "net8.0")) "dotnet" "run --project ../../.."