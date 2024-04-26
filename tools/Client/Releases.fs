﻿namespace YAVSRG.CLI.Features

open System.IO
open System.IO.Compression
open YAVSRG.CLI.Utils

module Releases =

    type BuildPlatformInfo =
        {
            Name: string
            RuntimeId: string
            BassLibraryFile: string
            GLFWLibraryFile: string
            SQLiteLibraryFile: string
            ExecutableFile: string
        }

    let build_platform (info: BuildPlatformInfo) =

        Directory.SetCurrentDirectory(INTERLUDE_SOURCE_PATH)

        let build_dir =
            Path.Combine(INTERLUDE_SOURCE_PATH, "bin", "Release", "net8.0", info.RuntimeId)

        let clean_dir =
            Path.Combine(YAVSRG_PATH, "interlude", "releases", $"Interlude-{info.Name}")

        try
            Directory.Delete(build_dir, true)
        with _ ->
            ()

        try
            Directory.Delete(clean_dir, true)
        with _ ->
            ()

        exec
            "dotnet"
            $"publish --configuration Release -r {info.RuntimeId} -p:PublishSingleFile=True --self-contained true"

        Directory.CreateDirectory clean_dir |> ignore

        let rec copy source target =
            Directory.CreateDirectory target |> ignore

            for file in Directory.GetFiles source do
                match Path.GetExtension(file).ToLower() with
                | ".dll"
                | ".so"
                | ".dylib"
                | ".txt" -> File.Copy(file, Path.Combine(target, Path.GetFileName file))
                | _ -> ()

        File.Copy(
            Path.Combine(YAVSRG_PATH, "engine", "lib", info.RuntimeId, info.BassLibraryFile),
            Path.Combine(clean_dir, info.BassLibraryFile)
        )

        File.Copy(Path.Combine(build_dir, "publish", info.ExecutableFile), Path.Combine(clean_dir, info.ExecutableFile))

        File.Copy(
            Path.Combine(build_dir, "publish", info.GLFWLibraryFile),
            Path.Combine(clean_dir, info.GLFWLibraryFile)
        )

        File.Copy(
            Path.Combine(build_dir, "publish", info.SQLiteLibraryFile),
            Path.Combine(clean_dir, info.SQLiteLibraryFile)
        )

        copy (Path.Combine(build_dir, "Locale")) (Path.Combine(clean_dir, "Locale"))

        printfn "Outputted to: %s" clean_dir

        if File.Exists(clean_dir + ".zip") then
            File.Delete(clean_dir + ".zip")

        ZipFile.CreateFromDirectory(clean_dir, clean_dir + ".zip")
        printfn "Zipped to: %s.zip" clean_dir

    let build_osx_arm64 () =
        build_platform
            {
                Name = "osx-arm64"
                RuntimeId = "osx-arm64"
                BassLibraryFile = "libbass.dylib"
                GLFWLibraryFile = "libglfw.3.dylib"
                SQLiteLibraryFile = "libe_sqlite3.dylib"
                ExecutableFile = "Interlude"
            }

    let build_linux_x64 () =
        build_platform
            {
                Name = "linux-x64"
                RuntimeId = "linux-x64"
                BassLibraryFile = "libbass.so"
                GLFWLibraryFile = "libglfw.so.3.3"
                SQLiteLibraryFile = "libe_sqlite3.so"
                ExecutableFile = "Interlude"
            }

    let build_win_x64 () =
        build_platform
            {
                Name = "win64"
                RuntimeId = "win-x64"
                BassLibraryFile = "bass.dll"
                GLFWLibraryFile = "glfw3.dll"
                SQLiteLibraryFile = "e_sqlite3.dll"
                ExecutableFile = "Interlude.exe"
            }
