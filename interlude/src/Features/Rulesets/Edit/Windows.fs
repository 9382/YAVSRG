﻿namespace Interlude.Features.Rulesets.Edit

open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Input
open Prelude
open Prelude.Gameplay.Rulesets
open Interlude.UI

type private EditWindowsPage(judgements: Judgement array, windows: Setting<(GameplayTime * GameplayTime) option> array) =
    inherit Page()

    let tab = Bind.mk Keys.Tab
    let entry_chain = System.Collections.Generic.Dictionary<Widget, Widget>()

    let rec next_entry (current: Widget) =
        match entry_chain.TryGetValue current with
        | true, next ->
            if next.Focusable then next.Select false
            else next_entry next
        | false, _ -> ()

    do
        assert(judgements.Length = windows.Length)

    let early_window j : Setting<GameplayTime> =
        Setting.make 
            (fun v ->
                let mutable v = -(abs v) |> max -500.0f<ms / rate>
                for i = j - 1 downto 0 do
                    match windows.[i].Value with
                    | Some (early, _) -> v <- min early v
                    | None -> ()
                for i = j + 1 to windows.Length - 1 do
                    match windows.[i].Value with
                    | Some (early, _) -> v <- max early v
                    | None -> ()
                let late = 
                    match windows.[j].Value with
                    | Some (_, late) -> late
                    | None -> 0.0f<ms / rate>
                windows.[j].Value <- Some(v, late)
            )
            (fun () ->
                match windows.[j].Value with 
                | Some (early, _) -> early
                | None -> 0.0f<ms / rate>
            )

    let late_window j : Setting<GameplayTime> =
        Setting.make 
            (fun v ->
                let mutable v = max 0.0f<ms / rate> v |> min 500.0f<ms / rate>
                for i = j - 1 downto 0 do
                    match windows.[i].Value with
                    | Some (_, late) -> v <- max late v
                    | None -> ()
                for i = j + 1 to windows.Length - 1 do
                    match windows.[i].Value with
                    | Some (_, late) -> v <- min late v
                    | None -> ()
                let early = 
                    match windows.[j].Value with
                    | Some (early, _) -> early
                    | None -> 0.0f<ms / rate>
                windows.[j].Value <- Some(early, v)
            )
            (fun () ->
                match windows.[j].Value with 
                | Some (_, late) -> late
                | None -> 0.0f<ms / rate>
            )

    override this.Content() =
        let container = FlowContainer.Vertical<Widget>(PRETTYHEIGHT)

        let mutable last_entry = None
        let add_entry (e: Widget) : Widget =
            match last_entry with
            | Some l -> entry_chain.Add(l, e)
            | None -> ()
            last_entry <- Some e
            e

        for i, j in judgements |> Array.indexed do

            let early_window = early_window i
            let late_window = late_window i

            let w = (PRETTYWIDTH - PRETTYTEXTWIDTH - 100.0f) * 0.5f

            let early = NumberEntry.create_uom "ms" early_window
            early.Position <- Position.SliceR(w).TranslateX(-w).ShrinkR(15.0f)
            let late = NumberEntry.create_uom "ms" late_window
            late.Position <- Position.SliceR(w).ShrinkL(15.0f)
            
            let c =
                NavigationContainer.Row()
                |+ Button(
                    (fun () -> if windows.[i].Value.IsSome then Icons.X_CIRCLE else Icons.PLUS_CIRCLE),
                    (fun () -> 
                        if windows.[i].Value.IsSome then 
                            windows.[i].Value <- None
                        else 
                            early_window.Set (-infinityf * 1.0f<_>)
                            late_window.Set (infinityf * 1.0f<_>)
                    ),
                    Position = Position.SliceL 100.0f)
                |+ add_entry(early.Conditional(fun () -> windows.[i].Value.IsSome))
                |+ add_entry(late.Conditional(fun () -> windows.[i].Value.IsSome))
            
            container.Add (PageSetting(j.Name, c))

        page_container()
        |+ ScrollContainer(container)
            .Pos(0, PAGE_BOTTOM)
        :> Widget

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        if tab.Tapped() then
            match Selection.get_focused_element() with
            | Some (:? Widget as w) ->
                next_entry (w.Parent.Parent)
            | _ -> ()
        
    override this.Title = %"rulesets.edit.windows"
    override this.OnClose() = ()

module EditWindows =

    let note_windows (ruleset: Setting<Ruleset>) : Page =
        let new_judgements = ruleset.Value.Judgements
        { new EditWindowsPage(
                new_judgements, 
                Array.init new_judgements.Length (fun i -> 
                    Setting.make
                        (fun v -> new_judgements.[i] <- { new_judgements.[i] with TimingWindows = v })
                        (fun () -> new_judgements.[i].TimingWindows)
                )
            ) with
            override this.OnClose() = ruleset.Value <- { ruleset.Value with Judgements = new_judgements }
        }
    
    let notes_windows_as_release_windows (ruleset: Setting<Ruleset>) : Page =
        let new_judgements = ruleset.Value.Judgements
        { new EditWindowsPage(
                new_judgements, 
                Array.init new_judgements.Length (fun i -> 
                    Setting.make
                        (fun v -> new_judgements.[i] <- { new_judgements.[i] with TimingWindows = v })
                        (fun () -> new_judgements.[i].TimingWindows)
                )
            ) with
            override this.OnClose() = ruleset.Value <- { ruleset.Value with Judgements = new_judgements }
            override this.Title = %"rulesets.mechanics.release_windows"
        }
    
    let release_windows (judgements: Judgement array, windows: (GameplayTime * GameplayTime) option array) : Page =
        { new EditWindowsPage(
                judgements, 
                Array.init windows.Length (fun i -> 
                    Setting.make
                        (fun v -> windows.[i] <- v)
                        (fun () -> windows.[i])
                )
            ) with
            override this.Title = %"rulesets.mechanics.release_windows"
        }