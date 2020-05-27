﻿namespace Interlude.Options

open System.Collections.Generic
open System.IO
open OpenTK.Input
open Prelude.Common
open Prelude.Json
open Prelude.Data.Profiles
open Prelude.Gameplay.Score
open Interlude
open Interlude.Input

type WindowType =
    | WINDOWED = 0
    | BORDERLESS = 1
    | FULLSCREEN = 2

type WindowResolution = 
    | Preset of index:int
    | Custom of width:int * height:int

type GameConfig = {
    WorkingDirectory: string
    WindowMode: WindowType
    Resolution: WindowResolution
    FrameLimiter: float
} with
    static member Default = {
        WorkingDirectory = ""
        WindowMode = WindowType.BORDERLESS
        Resolution = Custom (1024, 768)
        FrameLimiter = 0.0
    }

type Hotkeys = {
    Exit: Setting<Bind>
    Select: Setting<Bind>
    Search: Setting<Bind>
    Toolbar: Setting<Bind>
    Tooltip: Setting<Bind>
    Screenshot: Setting<Bind>
    Volume: Setting<Bind>
    Next: Setting<Bind>
    Previous: Setting<Bind>
    End: Setting<Bind>
    Start: Setting<Bind>

    Import: Setting<Bind>
    Options: Setting<Bind>
    Help: Setting<Bind>
} with
    static member Default = {
        Exit = new Setting<Bind>(Key Key.Escape)
        Select = new Setting<Bind>(Key Key.Enter)
        Search = new Setting<Bind>(Key Key.Tab)
        Tooltip = new Setting<Bind>(Key Key.Slash)
        Toolbar = new Setting<Bind>(Key Key.T |> Ctrl)
        Screenshot = new Setting<Bind>(Key Key.F12)
        Volume = new Setting<Bind>(Key Key.AltLeft)
        Previous = new Setting<Bind>(Key Key.Left)
        Next = new Setting<Bind>(Key Key.Right)
        Start = new Setting<Bind>(Key Key.Home)
        End = new Setting<Bind>(Key Key.End)

        Import = new Setting<Bind>(Key Key.I |> Ctrl)
        Options = new Setting<Bind>(Key Key.O |> Ctrl)
        Help = new Setting<Bind>(Key Key.H |> Ctrl)
    }

type GameOptions = {
    AudioOffset: NumSetting<float>
    AudioVolume: NumSetting<float>
    CurrentProfile: Setting<string>
    CurrentChart: Setting<string>
    CurrentOptionsTab: Setting<string>
    EnabledThemes: List<string>
    AccuracySystems: List<AccuracySystemConfig>
    HPSystems: List<HPSystemConfig>
    Hotkeys: Hotkeys
} with
    static member Default = {
        AudioOffset = FloatSetting(0.0, -500.0, 500.0)
        AudioVolume = FloatSetting(0.1, 0.0, 1.0)
        CurrentProfile = Setting("")
        CurrentChart = Setting("")
        CurrentOptionsTab = Setting("General")
        EnabledThemes = new List<string>()
        AccuracySystems = new List<AccuracySystemConfig>()
        HPSystems = new List<HPSystemConfig>()
        Hotkeys = Hotkeys.Default
    }

module Options =
    let resolutions: (struct (int * int)) array =
        [|(800, 600); (1024, 768); (1280, 800); (1280, 1024); (1366, 768); (1600, 900);
            (1600, 1024); (1680, 1050); (1920, 1080); (2715, 1527)|]

    let getResolution res =
        match res with
        | Custom (w, h) -> (true, struct (w, h))
        | Preset i ->
            let i = System.Math.Clamp(i, 0, Array.length resolutions - 1)
            (false, resolutions.[i])

    let mutable internal config = GameConfig.Default
    let mutable internal options = GameOptions.Default
    let mutable internal profile = Profile.Default
    let internal configPath = Path.GetFullPath("config.json")
    let profiles = new List<Profile>()

    let load() =
        try
            config <- JsonHelper.loadFile(configPath)
        with
        | :? FileNotFoundException -> Logging.Info("No config file found, creating it.") ""
        | err ->
            Logging.Critical("Could not load config.json! Maybe it is corrupt?") (err.ToString())
            System.Console.WriteLine("If you would like to launch anyway, press ENTER.")
            System.Console.WriteLine("If you would like to try and fix the problem youself, CLOSE THIS WINDOW.")
            System.Console.ReadLine() |> ignore
            Logging.Critical("User has chosen to launch game with default config.") ""
        
        if config.WorkingDirectory <> "" then Directory.SetCurrentDirectory(config.WorkingDirectory)
        
        try
            options <- JsonHelper.loadFile(Path.Combine(getDataPath("Data"), "options.json"))
        with
        | :? FileNotFoundException -> Logging.Info("No options file found, creating it.") ""
        | err ->
            Logging.Critical("Could not load options.json! Maybe it is corrupt?") (err.ToString())
            System.Console.WriteLine("If you would like to proceed anyway, press ENTER.")
            System.Console.WriteLine("If you would like to try and fix the problem youself, CLOSE THIS WINDOW.")
            System.Console.ReadLine() |> ignore
            Logging.Critical("User has chosen to proceed with game setup with default game settings.") ""

        for pf in Directory.EnumerateFiles(Profile.profilePath) do
            try
                let data = Profile.load pf
                Profile.save data
                profiles.Add(data)
                if data.UUID = options.CurrentProfile.Get() then profile <- data
            with
            | err -> Logging.Error("Failed to load profile: " + Path.GetFileName(pf)) (err.ToString())
        options.CurrentProfile.Set(profile.UUID)
        Themes.loadThemes(options.EnabledThemes)
        Themes.changeNoteSkin(profile.NoteSkin.Get())
        Logging.Debug(sprintf "Current profile: %s (%s)" (profile.Name.Get()) profile.UUID) ""

    let changeProfile(uuid: string) =
        Profile.save profile
        match Seq.tryFind (fun p -> p.UUID = uuid) profiles with
        | Some p ->
            options.CurrentProfile.Set(p.UUID)
            profile <- p
        | None -> Logging.Error("No profile with UUID '" + uuid + "' exists") ""

    let save() =
        try
            JsonHelper.saveFile config configPath
            JsonHelper.saveFile options <| Path.Combine(getDataPath("Data"), "options.json")
            Profile.save profile
        with
        | err -> Logging.Critical("Failed to write options/config to file.") (err.ToString())