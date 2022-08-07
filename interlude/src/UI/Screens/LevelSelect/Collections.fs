﻿namespace Interlude.UI.Screens.LevelSelect

open Percyqaz.Common
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Prelude.Common
open Prelude.Data.Charts.Caching
open Prelude.Data.Charts.Library
open Prelude.Data.Charts.Collections
open Interlude.UI
open Interlude.UI.Components
open Interlude.UI.Components.Selection
open Interlude.UI.Components.Selection.Menu
open Interlude.UI.Components.Selection.Compound
open Interlude.Gameplay
open Interlude.Options

module CollectionManager =

    let addChart(cc: CachedChart, context: LevelSelectContext) =
        if Collections.selectedName <> context.InCollection then
            let success = Collections.addChart(cc, rate.Value, selectedMods.Value)
            if success then
                if options.ChartGroupMode.Value = "Collections" then LevelSelect.refresh <- true else LevelSelect.minorRefresh <- true
                Notification.add (Localisation.localiseWith [Chart.cacheInfo.Value.Title; Collections.selectedName] "collections.added", NotificationType.Info)

    let removeChart(cc: CachedChart, context: LevelSelectContext) =
        let success = Collections.removeChart(cc, context)
        if success then
            if options.ChartGroupMode.Value = "Collections" then LevelSelect.refresh <- true else LevelSelect.minorRefresh <- true
            Notification.add (Localisation.localiseWith [Chart.cacheInfo.Value.Title; Collections.selectedName] "collections.removed", NotificationType.Info)
            if context = Chart.context then Chart.context <- LevelSelectContext.None

    let dropdownMenuOptions(cc: CachedChart, context: LevelSelectContext) =
        let canRemove =
            context.InCollection = Collections.selectedName ||
            match Collections.selectedCollection with
            | Collection ccs -> ccs.Contains cc.FilePath
            | Playlist ps -> ps.FindAll(fun (id, _) -> id = cc.FilePath).Count = 1
            | Goals gs -> gs.FindAll(fun (id, _) -> id = cc.FilePath).Count = 1
        [
            if not canRemove then sprintf "%s Add to '%s'" Icons.add Collections.selectedName, fun () -> addChart(cc, context)
            else sprintf "%s Remove from '%s'" Icons.remove Collections.selectedName, fun () -> removeChart(cc, context)
        ]

type private EditCollectionPage(originalName) as this =
    inherit Page()

    let data = (Collections.get originalName).Value

    let name = Setting.simple originalName |> Setting.alphaNum
    let originalType = match data with Collection _ -> "Collection" | Playlist _ -> "Playlist" | Goals _ -> "Goals"
    let ctype = Setting.simple originalType

    do
        this.Content(
            column()
            |+ PrettySetting("collections.edit.collectionname", Percyqaz.Flux.UI.TextEntry(name, "none")).Pos(200.0f)
            |+ PrettySetting("collections.edit.type", Selector([|"Collection", "Collection"; "Playlist", "Playlist"; "Goals", "Goals"|], ctype)).Pos(300.0f)
        )

    override this.Title = originalType
    override this.OnClose() =
        if name.Value <> originalName then
            Logging.Debug (sprintf "Renaming collection '%s' to '%s'" originalName name.Value)
            if (Collections.get name.Value).IsSome then
                Logging.Debug "Rename failed, target collection already exists."
                name.Value <- originalName
            else
                Collections.rename (originalName, name.Value) |> ignore

        let data =
            if originalType <> ctype.Value then
                Logging.Debug (sprintf "Changing type of collection to %s" ctype.Value)
                match ctype.Value with
                | "Collection" -> data.ToCollection()
                | "Playlist" -> data.ToPlaylist(selectedMods.Value, rate.Value)
                | "Goals" -> data.ToGoals(selectedMods.Value, rate.Value)
                | _ -> failwith "impossible"
            else data
        Collections.update (name.Value, data)

type private CollectionsPage() as this =
    inherit Page()

    do
        this.Content(
            column()
            |+ PrettySetting("collections", 
                Grid.create Collections.enumerate
                    (Grid.Config.Default
                        .WithColumn(id)
                        .WithAction(Icons.edit, fun c -> Menu.ShowPage (EditCollectionPage c))
                        .WithAction(Icons.delete, fun c -> 
                            Menu.ShowPage (ConfirmPage(Localisation.localiseWith [c] "collections.confirmdelete", fun () -> Collections.delete c |> ignore)))
                        .WithSelection((fun c -> Collections.selectedName = c), fun (c, b) -> if b then LevelSelect.minorRefresh <- true; Collections.select c)
                        .WithNew(fun () -> Collections.create (Collections.getNewName(), Collection.Blank) |> ignore)
                    )
                ).Pos(200.0f, PRETTYWIDTH, 600.0f)
            |+ Text(Localisation.localiseWith [(!|"add_to_collection").ToString()] "collections.addhint",
                Position = Position.SliceBottom(190.0f).SliceTop(70.0f))
            |+ Text(Localisation.localiseWith [(!|"remove_from_collection").ToString()] "collections.removehint",
                Position = Position.SliceBottom(120.0f).SliceTop(70.0f))
        )
    override this.Title = N"collections"
    override this.OnClose() = ()
    
type CollectionManager() as this =
    inherit Widget1()

    do StylishButton ((fun () -> Menu.ShowPage CollectionsPage), K "Collections", (fun () -> Style.color(100, 0.6f, 0.4f)), "collections") |> this.Add
    
    override this.Update(elapsedTime, bounds) =
        base.Update(elapsedTime, bounds)

        if Chart.cacheInfo.IsSome then

            if (!|"add_to_collection").Tapped() then CollectionManager.addChart(Chart.cacheInfo.Value, Chart.context)
            elif (!|"remove_from_collection").Tapped() then CollectionManager.removeChart(Chart.cacheInfo.Value, Chart.context)
            elif (!|"move_down_in_collection").Tapped() then
                if Collections.reorder false then LevelSelect.refresh <- true
            elif (!|"move_up_in_collection").Tapped() then
                if Collections.reorder true then LevelSelect.refresh <- true