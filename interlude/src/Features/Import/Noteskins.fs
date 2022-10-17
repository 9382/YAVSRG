﻿namespace Interlude.Features.Import

open System.IO
open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude.Common
open Prelude.Data.Themes.Noteskin
open Prelude.Data
open Prelude.Data.Charts.Sorting
open Interlude.UI
open Interlude.Content

type NoteskinCard(data: RepoEntry) as this =
    inherit Frame(NodeType.None, Fill = (fun () -> Style.color(120, (if this.Downloaded then 0.7f else 0.5f), 0.0f)), Border = (fun () -> Style.color(200, 0.7f, 0.2f)))
            
    let mutable downloaded = Noteskins.list() |> Array.map snd |> Array.contains data.Name
    let download() =
        let target = Path.Combine(getDataPath("Noteskins"), System.Guid.NewGuid().ToString() + ".isk")
        Notifications.add (Localisation.localiseWith [data.Name] "notification.download.noteskin", NotificationType.Task)
        WebServices.download_file.Request((data.Download, target), fun success -> if success then sync(Noteskins.load))
        downloaded <- true

    let mutable preview = Sprite.Default
    do
        this
        |+ Text(data.Name,
            Align = Alignment.LEFT,
            Position = Position.Margin(5.0f, 0.0f).TrimTop(240.0f))
        |+ Text(
            (fun () -> if downloaded then "Downloaded" else ""),
            Align = Alignment.RIGHT,
            Position = Position.Margin(5.0f, 0.0f).TrimTop(240.0f) )
        |* Clickable((fun () -> if not downloaded then download()))

    override this.Draw() =
        base.Draw()
        Draw.sprite ( Rect.Box(this.Bounds.Left, this.Bounds.Top, 320.0f, 240.0f) ) Color.White preview

    member this.LoadPreview(img: Bitmap) =
        preview <- Sprite.upload(img, 1, 1, true)

    member this.Name = data.Name
    member this.Downloaded = downloaded

    static member Filter(filter: Filter) =
        fun (c: Widget) ->
            match c with
            | :? NoteskinCard as c ->
                List.forall (
                    function
                    | Impossible -> false
                    | String str -> c.Name.ToLower().Contains(str)
                    | _ -> true
                ) filter
            | _ -> true
        
module Noteskins =

    let source = "https://raw.githubusercontent.com/YAVSRG/Interlude.Noteskins/main/index.json"