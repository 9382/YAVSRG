namespace Interlude.Features.Import.osu

open System
open System.Text.RegularExpressions
open System.IO
open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Percyqaz.Flux.Input
open Prelude
open Prelude.Data.Library
open Prelude.Data.Library.Sorting
open Prelude.Data
open Interlude.Content
open Interlude.UI
open Interlude.Features.Import

type private BeatmapDownloadStatus =
    | NotDownloaded
    | Downloading
    | Installed
    | DownloadFailed

type private BeatmapImportCard(data: NeriNyanBeatmapset) as this =
    inherit
        Container(
            NodeType.Button(fun () ->
                Style.click.Play()
                this.Download()
            )
        )

    let mutable status = NotDownloaded
    let mutable progress = 0.0f

    let download () =
        if status = NotDownloaded || status = DownloadFailed then
            let target =
                Path.Combine(get_game_folder "Downloads", Guid.NewGuid().ToString() + ".osz")

            WebServices.download_file.Request(
                (sprintf "https://catboy.best/d/%in" data.id, target, (fun p -> progress <- p)),
                fun completed ->
                    if completed then
                        Imports.auto_convert.Request(
                            (target, true, Content.Library),
                            function
                            | Some result ->
                                Notifications.task_feedback (
                                    Icons.DOWNLOAD, 
                                    %"notification.install_song",
                                    [data.title; result.ConvertedCharts.ToString(); result.SkippedCharts.Length.ToString()] %> "notification.install_song.body"
                                )
                                defer charts_updated_ev.Trigger
                                status <- Installed
                                File.Delete target
                            | None ->
                                Notifications.error (%"notification.install_song_failed", data.title)
                                status <- DownloadFailed
                                File.Delete target
                        )
                    else
                        status <- DownloadFailed
            )

            status <- Downloading

    let fill, border, ranked_status =
        match data.status with
        | "ranked" -> Colors.cyan, Colors.cyan_accent, "Ranked"
        | "qualified" -> Colors.green, Colors.green_accent, "Qualified"
        | "loved" -> Colors.pink, Colors.pink_accent, "Loved"
        | "pending" -> Colors.grey_2, Colors.grey_1, "Pending"
        | "wip" -> Colors.grey_2, Colors.grey_1, "WIP"
        | "graveyard"
        | _ -> Colors.grey_2, Colors.grey_1, "Graveyard"

    let beatmaps = data.beatmaps |> Array.filter (fun x -> x.mode = "mania")

    let keymodes_string =
        let modes =
            beatmaps
            |> Seq.map (fun bm -> int bm.cs)
            |> Seq.distinct
            |> Seq.sort
            |> Array.ofSeq

        if modes.Length > 3 then
            sprintf "%i-%iK" modes.[0] modes.[modes.Length - 1]
        else
            modes |> Seq.map (fun k -> sprintf "%iK" k) |> String.concat ", "

    do

        this
        |+ Frame(
            Fill = (fun () -> if this.Focused then fill.O3 else fill.O2),
            Border = fun () -> if this.Focused then Colors.white else border.O2
        )
        |* Clickable.Focus this
    //|+ Button(Icons.OPEN_IN_BROWSER,
    //    fun () -> openUrl(sprintf "https://osu.ppy.sh/beatmapsets/%i" data.beatmapset_id)
    //    ,
    //    Position = Position.SliceRight(160.0f).TrimRight(80.0f).Margin(5.0f, 10.0f))

    override this.OnFocus(by_mouse: bool) =
        base.OnFocus by_mouse
        Style.hover.Play()

    override this.Draw() =
        base.Draw()

        match status with
        | Downloading -> Draw.rect (this.Bounds.SliceLeft(this.Bounds.Width * progress)) Colors.white.O1
        | _ -> ()

        Text.fill_b (
            Style.font,
            data.title,
            this.Bounds.SliceTop(45.0f).Shrink(10.0f, 0.0f),
            Colors.text,
            Alignment.LEFT
        )

        Text.fill_b (
            Style.font,
            data.artist + "  •  " + data.creator,
            this.Bounds.SliceBottom(45.0f).Shrink(10.0f, 5.0f),
            Colors.text_subheading,
            Alignment.LEFT
        )

        let status_bounds =
            this.Bounds.SliceBottom(40.0f).SliceRight(150.0f).Shrink(5.0f, 0.0f)

        Draw.rect status_bounds Colors.shadow_2.O2

        Text.fill_b (
            Style.font,
            ranked_status,
            status_bounds.Shrink(5.0f, 0.0f).TrimBottom(5.0f),
            (border, Colors.shadow_2),
            Alignment.CENTER
        )

        let download_bounds =
            this.Bounds.SliceTop(40.0f).SliceRight(300.0f).Shrink(5.0f, 0.0f)

        Draw.rect download_bounds Colors.shadow_2.O2

        Text.fill_b (
            Style.font,
            (match status with
             | NotDownloaded -> Icons.DOWNLOAD + " Download"
             | Downloading -> Icons.DOWNLOAD + " Downloading .."
             | DownloadFailed -> Icons.X + " Error"
             | Installed -> Icons.CHECK + " Downloaded"),
            download_bounds.Shrink(5.0f, 0.0f).TrimBottom(5.0f),
            (match status with
             | NotDownloaded -> if this.Focused then Colors.text_yellow_2 else Colors.text
             | Downloading -> Colors.text_yellow_2
             | DownloadFailed -> Colors.text_red
             | Installed -> Colors.text_green),
            Alignment.CENTER
        )

        let stat x text =
            let stat_bounds = this.Bounds.SliceBottom(40.0f).TrimRight(x).SliceRight(145.0f)
            Draw.rect stat_bounds Colors.shadow_2.O2

            Text.fill_b (
                Style.font,
                text,
                stat_bounds.Shrink(5.0f, 0.0f).TrimBottom(5.0f),
                Colors.text_subheading,
                Alignment.CENTER
            )

        stat 150.0f (sprintf "%s %i" Icons.HEART data.favourite_count)
        stat 300.0f (sprintf "%s %i" Icons.PLAY data.play_count)
        stat 450.0f keymodes_string

        if this.Focused && Mouse.x () > this.Bounds.Right - 600.0f then
            let popover_bounds =
                Rect.Box(
                    this.Bounds.Right - 900.0f,
                    this.Bounds.Bottom + 10.0f,
                    600.0f,
                    45.0f * float32 beatmaps.Length
                )

            Draw.rect popover_bounds Colors.shadow_2.O3
            let mutable y = 0.0f

            for beatmap in beatmaps do
                Text.fill_b (
                    Style.font,
                    beatmap.version,
                    popover_bounds.SliceTop(45.0f).Translate(0.0f, y).Shrink(10.0f, 5.0f),
                    Colors.text,
                    Alignment.LEFT
                )

                Text.fill_b (
                    Style.font,
                    sprintf "%.2f*" beatmap.difficulty_rating,
                    popover_bounds.SliceTop(45.0f).Translate(0.0f, y).Shrink(10.0f, 5.0f),
                    Colors.text,
                    Alignment.RIGHT
                )

                y <- y + 45.0f

    member private this.Download() = download ()

type private SortingDropdown
    (options: (string * string) seq, label: string, setting: Setting<string>, reverse: Setting<bool>, bind: Hotkey) =
    inherit Container(NodeType.None)

    let mutable display_value =
        Seq.find (fun (id, _) -> id = setting.Value) options |> snd

    let dropdown_wrapper = DropdownWrapper(fun d -> Position.SliceTop(d.Height + 60.0f).TrimTop(60.0f).Margin(Style.PADDING, 0.0f))

    override this.Init(parent: Widget) =
        this
        |+ StylishButton(
            (fun () -> this.ToggleDropdown()),
            K(label + ":"),
            !%Palette.HIGHLIGHT_100,
            Hotkey = bind,
            Position = Position.SliceLeft 120.0f
        )
        |+ StylishButton(
            (fun () -> reverse.Value <- not reverse.Value),
            (fun () ->
                sprintf
                    "%s %s"
                    display_value
                    (if reverse.Value then
                         Icons.CHEVRONS_DOWN
                     else
                         Icons.CHEVRONS_UP)
            ),
            !%Palette.DARK_100,
            TiltRight = false,
            Position = Position.TrimLeft 145.0f
        )
        |* dropdown_wrapper

        base.Init parent

    member this.ToggleDropdown() =
        dropdown_wrapper.Toggle(fun () ->
            Dropdown
                {
                    Items = options
                    ColorFunc = K Colors.text
                    Setting =
                        setting
                        |> Setting.trigger (fun v ->
                            display_value <- Seq.find (fun (id, _) -> id = v) options |> snd
                        )
                }
        )

module Beatmaps =

    type Beatmaps() as this =
        inherit Container(NodeType.Container(fun _ -> Some this.Items))

        let items = FlowContainer.Vertical<BeatmapImportCard>(80.0f, Spacing = 15.0f)

        let scroll =
            ScrollContainer(items, Margin = Style.PADDING, Position = Position.TrimTop(120.0f).TrimBottom(65.0f))

        let mutable filter: Filter = []
        let query_order = Setting.simple "updated"
        let descending_order = Setting.simple true
        let mutable statuses = Set.singleton "Ranked"
        let mutable when_at_bottom: (unit -> unit) option = None
        let mutable loading = false

        let json_downloader =
            { new Async.SwitchService<string * (unit -> unit), NeriNyanBeatmapSearch option * (unit -> unit)>() with
                override this.Process((url, action_at_bottom)) =
                    async {
                        match! WebServices.download_string.RequestAsync(url) with
                        | Some bad_json ->
                            let fixed_json = Regex.Replace(bad_json, @"[^\u0000-\u007F]+", "")

                            match JSON.FromString(fixed_json) with 
                            | Ok data ->
                                return Some data, action_at_bottom
                            | Error err ->
                                Logging.Error("Failed to parse json data from " + url, err)
                                return None, action_at_bottom
                        | None -> return None, action_at_bottom
                    }

                override this.Handle((data: NeriNyanBeatmapSearch option, action_at_bottom)) =
                    match data with
                    | Some d ->
                        for p in d do
                            items.Add(BeatmapImportCard p)

                        if d.Length >= 50 then
                            when_at_bottom <- Some action_at_bottom

                        loading <- false
                    | None -> ()
            }

        let rec search (filter: Filter) (page: int) =
            loading <- true
            when_at_bottom <- None

            let mutable request =
                {
                    m = "mania"
                    page = page
                    query = ""
                    ranked = (String.concat "," statuses).ToLower()
                    sort = query_order.Value + if descending_order.Value then "_desc" else "_asc"
                    cs = {| min = 3.0; max = 10.0 |}
                }

            let mutable invalid = false

            List.iter
                (function
                | Impossible -> invalid <- true
                | String s ->
                    request <-
                        { request with
                            query =
                                match request.query with
                                | "" -> s
                                | t -> request.query + " " + s
                        }
                | Equals("k", n)
                | Equals("key", n)
                | Equals("keys", n) ->
                    match Int32.TryParse n with
                    | (true, i) ->
                        request <-
                            { request with
                                cs = {| min = float i; max = float i |}
                            }
                    | _ -> ()
                | _ -> ())
                filter

            let url =
                "https://api.nerinyan.moe/search?b64="
                + (request
                   |> JSON.ToString
                   |> fun s ->
                       s.Replace("\n", "")
                       |> System.Text.Encoding.UTF8.GetBytes
                       |> Convert.ToBase64String
                       |> Uri.EscapeDataString)
                + "&ps=50"

            json_downloader.Request(url, (fun () -> search filter (page + 1)))

        let begin_search (filter: Filter) =
            search filter 0
            items.Clear()

        let status_button (status: string) (position: Position) (color: Color) =
            StylishButton(
                (fun () ->
                    if statuses.Contains status then
                        statuses <- Set.remove status statuses
                    else
                        statuses <- Set.add status statuses

                    begin_search filter
                ),
                (fun () ->
                    if statuses.Contains status then
                        Icons.CHECK + " " + status
                    else
                        Icons.X + " " + status
                ),
                (fun () -> if statuses.Contains status then color.O3 else color.O1),
                Position = position
            )

        override this.Focusable = items.Focusable

        override this.Init(parent) =
            begin_search filter

            this
            |+ (SearchBox(
                    Setting.simple "",
                    (fun (f: Filter) ->
                        filter <- f
                        defer (fun () -> begin_search filter)
                    ),
                    Position = Position.SliceTop 60.0f
                )
                |+ LoadingIndicator.Border(fun () -> loading))
            |+ Text(%"imports.osu.disclaimer", Position = Position.SliceBottom 55.0f)
            |+ scroll
            |+ (let r =
                    status_button
                        "Ranked"
                        { Position.TrimTop(65.0f).SliceTop(50.0f) with
                            Right = 0.18f %- 25.0f
                        }
                        Colors.cyan

                r.TiltLeft <- false
                r)
            |+ status_button
                "Qualified"
                { Position.TrimTop(65.0f).SliceTop(50.0f) with
                    Left = 0.18f %+ 0.0f
                    Right = 0.36f %- 25.0f
                }
                Colors.green
            |+ status_button
                "Loved"
                { Position.TrimTop(65.0f).SliceTop(50.0f) with
                    Left = 0.36f %+ 0.0f
                    Right = 0.54f %- 25.0f
                }
                Colors.pink
            |+ status_button
                "Unranked"
                { Position.TrimTop(65.0f).SliceTop(50.0f) with
                    Left = 0.54f %+ 0.0f
                    Right = 0.72f %- 25.0f
                }
                Colors.grey_2
            |+ EmptyState(Icons.SEARCH, %"imports.osu.no_results", Position = Position.TrimTop(120.0f))
                .Conditional(fun () -> not loading && items.Count = 0)
            |* SortingDropdown(
                [
                    "plays", "Play count"
                    "updated", "Date"
                    "difficulty", "Difficulty"
                    "favourites", "Favourites"
                ],
                "Sort",
                query_order |> Setting.trigger (fun _ -> begin_search filter),
                descending_order |> Setting.trigger (fun _ -> begin_search filter),
                "sort_mode",
                Position =
                    {
                        Left = 0.72f %+ 0.0f
                        Top = 0.0f %+ 65.0f
                        Right = 1.0f %- 0.0f
                        Bottom = 0.0f %+ 115.0f
                    }
            )

            base.Init parent

        override this.Update(elapsed_ms, moved) =
            json_downloader.Join()
            base.Update(elapsed_ms, moved)

            if when_at_bottom.IsSome && scroll.PositionPercent > 0.9f then
                when_at_bottom.Value()
                when_at_bottom <- None

        member private this.Items = items

    let tab = Beatmaps()