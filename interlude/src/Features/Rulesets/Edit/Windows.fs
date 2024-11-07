﻿namespace Interlude.Features.Rulesets.Edit

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude
open Prelude.Gameplay.Rulesets
open Interlude.UI

type private EditWindowsPage(judgements: Judgement array, windows: Setting<(GameplayTime * GameplayTime) option> array) =
    inherit Page()

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
                |+ early.Conditional(fun () -> windows.[i].Value.IsSome)
                |+ late.Conditional(fun () -> windows.[i].Value.IsSome)
            
            container.Add (PageSetting(j.Name, c))

        page_container()
        |+ ScrollContainer(container)
            .Pos(0, PAGE_BOTTOM)
        :> Widget
        
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