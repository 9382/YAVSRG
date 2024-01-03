﻿namespace Interlude.Features.LevelSelect

open Percyqaz.Common
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude.Common
open Prelude.Data.Charts.Caching
open Prelude.Data.Charts.Library
open Prelude.Data.Charts.Sorting
open Prelude.Data.Charts.Collections
open Interlude.Options
open Interlude.Utils
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Features.Gameplay

module CollectionManager =

    let add_to (name: string, collection: Collection, cc: CachedChart) =
        if
            match collection with
            | Folder c -> c.Add cc
            | Playlist p -> p.Add(cc, rate.Value, selected_mods.Value)
        then
            if options.LibraryMode.Value = LibraryMode.Collections then
                LevelSelect.refresh_all ()
            else
                LevelSelect.refresh_details ()

            Notifications.action_feedback (Icons.FOLDER_PLUS, [ cc.Title; name ] %> "collections.added", "")
            true
        else
            false

    let remove_from (name: string, collection: Collection, cc: CachedChart, context: LibraryContext) =
        if
            match collection with
            | Folder c -> c.Remove cc
            | Playlist p ->
                match context with
                | LibraryContext.Playlist(i, in_name, _) when name = in_name -> p.RemoveAt i
                | _ -> p.RemoveSingle cc
        then
            if options.LibraryMode.Value <> LibraryMode.All then
                LevelSelect.refresh_all ()
            else
                LevelSelect.refresh_details ()

            Notifications.action_feedback (Icons.FOLDER_MINUS, [ cc.Title; name ] %> "collections.removed", "")

            if Some cc = Chart.CACHE_DATA then
                Chart.LIBRARY_CTX <- LibraryContext.None

            true
        else
            false

    let reorder_up (context: LibraryContext) : bool =
        match context with
        | LibraryContext.Playlist(index, id, data) ->
            if collections.GetPlaylist(id).Value.MoveChartUp index then
                if Chart.LIBRARY_CTX = context then
                    Chart.LIBRARY_CTX <- LibraryContext.Playlist(index - 1, id, data)

                LevelSelect.refresh_all ()
                true
            else
                false
        | _ -> false

    let reorder_down (context: LibraryContext) : bool =
        match context with
        | LibraryContext.Playlist(index, id, data) ->
            if collections.GetPlaylist(id).Value.MoveChartDown index then
                if Chart.LIBRARY_CTX = context then
                    Chart.LIBRARY_CTX <- LibraryContext.Playlist(index + 1, id, data)

                LevelSelect.refresh_all ()
                true
            else
                false
        | _ -> false

type private CreateFolderPage() as this =
    inherit Page()

    let new_name = Setting.simple "Folder" |> Setting.alphanumeric
    let icon = Setting.simple Icons.HEART

    do
        this.Content(
            column ()
            |+ PageTextEntry("collections.edit.folder_name", new_name).Pos(200.0f)
            |+ PageSetting("collections.edit.icon", Selector(CreateFolderPage.Icons, icon))
                .Pos(300.0f)
            |+ PageButton(
                "confirm.yes",
                (fun () ->
                    if collections.CreateFolder(new_name.Value, icon.Value).IsSome then
                        Menu.Back()
                )
            )
                .Pos(400.0f)
        )

    override this.Title = %"collections.create_folder.name"
    override this.OnClose() = ()

    static member Icons =
        [|
            Icons.HEART, Icons.HEART
            Icons.STAR, Icons.STAR
            Icons.BOOKMARK, Icons.BOOKMARK
            Icons.FOLDER, Icons.FOLDER
        |]

type private CreatePlaylistPage() as this =
    inherit Page()

    let new_name = Setting.simple "Playlist" |> Setting.alphanumeric
    let icon = Setting.simple Icons.HEART

    do
        this.Content(
            column ()
            |+ PageTextEntry("collections.edit.playlist_name", new_name).Pos(200.0f)
            |+ PageSetting("collections.edit.icon", Selector(CreatePlaylistPage.Icons, icon))
                .Pos(300.0f)
            |+ PageButton(
                "confirm.yes",
                (fun () ->
                    if collections.CreatePlaylist(new_name.Value, icon.Value).IsSome then
                        Menu.Back()
                    else
                        Notifications.action_feedback (
                            Icons.X,
                            "Name is taken",
                            "A collection already exists with that name"
                        )
                )
            )
                .Pos(400.0f)
        )

    override this.Title = %"collections.create_playlist.name"
    override this.OnClose() = ()

    static member Icons =
        [|
            Icons.STAR, Icons.STAR
            Icons.FLAG, Icons.FLAG
            Icons.PLAY, Icons.PLAY
            Icons.LIST, Icons.LIST
        |]

type private EditFolderPage(name: string, folder: Folder) as this =
    inherit Page()

    let new_name = Setting.simple name |> Setting.alphanumeric

    do
        let content =
            column ()
            |+ PageTextEntry("collections.edit.folder_name", new_name).Pos(200.0f)
            |+ PageSetting("collections.edit.icon", Selector(CreateFolderPage.Icons, folder.Icon))
                .Pos(270.0f)
            |+ PageButton(
                "collections.edit.delete",
                (fun () ->
                    ConfirmPage(
                        [ name ] %> "misc.confirmdelete",
                        fun () ->
                            if collections.Delete name then
                                if options.LibraryMode.Value = LibraryMode.Collections then
                                    LevelSelect.refresh_all ()

                                // todo: unselect collection when deleted

                                Menu.Back()
                    )
                        .Show()
                ),
                Icon = Icons.TRASH
            )
                .Pos(370.0f)

        this.Content content

    override this.Title = name

    override this.OnClose() =
        if new_name.Value <> name && new_name.Value.Length > 1 then
            if collections.RenameCollection(name, new_name.Value) then
                Logging.Debug(sprintf "Renamed collection '%s' to '%s'" name new_name.Value)
            else
                Notifications.action_feedback (Icons.X, "Rename failed", "A collection already exists with that name")
                Logging.Debug "Rename failed, maybe that name already exists?"

type private EditPlaylistPage(name: string, playlist: Playlist) as this =
    inherit Page()

    let new_name = Setting.simple name |> Setting.alphanumeric

    do
        let content =
            column ()
            |+ PageTextEntry("collections.edit.playlist_name", new_name).Pos(200.0f)
            |+ PageSetting("collections.edit.icon", Selector(CreatePlaylistPage.Icons, playlist.Icon))
                .Pos(270.0f)
            |+ PageButton(
                "collections.edit.delete",
                (fun () ->
                    ConfirmPage(
                        [ name ] %> "misc.confirmdelete",
                        fun () ->
                            if collections.Delete name then
                                if options.LibraryMode.Value = LibraryMode.Collections then
                                    LevelSelect.refresh_all ()

                                // todo: unselect collection when deleted

                                Menu.Back()
                    )
                        .Show()
                ),
                Icon = Icons.TRASH
            )
                .Pos(370.0f)

        this.Content content

    override this.Title = name

    override this.OnClose() =
        if new_name.Value <> name && new_name.Value.Length > 0 then
            if collections.RenamePlaylist(name, new_name.Value) then
                Logging.Debug(sprintf "Renamed playlist '%s' to '%s'" name new_name.Value)
            else
                Notifications.action_feedback (Icons.X, "Rename failed", "A collection already exists with that name")
                Logging.Debug "Rename failed, maybe that name already exists?"

type private CollectionButton(icon, name, action) =
    inherit
        StaticContainer(
            NodeType.Button(fun () ->
                Style.click.Play()
                action ()
            )
        )

    override this.Init(parent: Widget) =
        this
        |+ Text(
            K(sprintf "%s %s  >" icon name),
            Color = (fun () -> ((if this.Focused then Colors.yellow_accent else Colors.white), Colors.shadow_2)),
            Align = Alignment.LEFT,
            Position = Position.Margin Style.PADDING
        )
        |* Clickable.Focus this

        base.Init parent

    override this.OnFocus() =
        Style.hover.Play()
        base.OnFocus()

    override this.Draw() =
        if this.Focused then
            Draw.rect this.Bounds Colors.yellow_accent.O1

        base.Draw()

type SelectCollectionPage(on_select: (string * Collection) -> unit) as this =
    inherit Page()

    let container = FlowContainer.Vertical<Widget>(PRETTYHEIGHT)

    let refresh () =
        container.Clear()

        container
        |+ PageButton("collections.create_folder", (fun () -> Menu.ShowPage CreateFolderPage))
            .Tooltip(Tooltip.Info("collections.create_folder"))
        |+ PageButton("collections.create_playlist", (fun () -> Menu.ShowPage CreatePlaylistPage))
            .Tooltip(Tooltip.Info("collections.create_playlist"))
        |* Dummy()

        for name, collection in collections.List do
            match collection with
            | Folder f -> container.Add(CollectionButton(f.Icon.Value, name, (fun () -> on_select (name, collection))))
            | Playlist p ->
                container.Add(CollectionButton(p.Icon.Value, name, (fun () -> on_select (name, collection))))

        if container.Focused then
            container.Focus()

    do
        refresh ()

        this.Content(ScrollContainer.Flow(container, Position = Position.Margin(100.0f, 200.0f)))

    override this.Title = %"collections.name"
    override this.OnClose() = ()
    override this.OnReturnTo() = refresh ()

    static member Editor() =
        SelectCollectionPage(fun (name, collection) ->
            match collection with
            | Folder f -> EditFolderPage(name, f).Show()
            | Playlist p -> EditPlaylistPage(name, p).Show()
        )
