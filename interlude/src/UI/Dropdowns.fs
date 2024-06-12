﻿namespace Interlude.UI

open Percyqaz.Common
open Percyqaz.Flux.Input
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude

module private Dropdown =

    let ITEMSIZE = 55.0f

    type Item(label: string, is_odd: bool, color: Color * Color, onclick: unit -> unit) =
        inherit
            Container(
                NodeType.Button(fun () ->
                    Style.click.Play()
                    onclick ()
                )
            )

        override this.Init(parent) =
            this 
            |+ Clickable.Focus(this, Floating = true)
            |* Text(label, Align = Alignment.LEFT, Position = Position.Margin(10.0f, 5.0f), Color = K color)
            base.Init parent

        override this.OnFocus(by_mouse: bool) =
            base.OnFocus by_mouse
            Style.hover.Play()

        override this.Draw() =
            if is_odd then
                Draw.rect this.Bounds (Colors.shadow_1.O1a 100)
            if this.Focused then
                Draw.rect this.Bounds (!*Palette.HOVER)

            base.Draw()

type DropdownWrapper(positioning: IHeight -> Position) as this =
    inherit StaticWidget(NodeType.Container(fun () -> this.Current))

    let mutable current : Widget option = None
    let mutable swapped_last_frame = false

    member private this.Current = current |> Option.map (fun x -> x :> ISelection)

    override this.Init(parent) =
        base.Init parent
        current |> Option.iter (fun dropdown -> dropdown.Init this)

    override this.Draw() = current |> Option.iter (fun dropdown -> dropdown.Draw())

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        let moved =
            if swapped_last_frame then
                swapped_last_frame <- false
                true
            else
                moved

        match current with
        | None -> ()
        | Some dropdown ->
            dropdown.Update(elapsed_ms, moved)

            if
                (%%"exit").Tapped()
                || not this.Focused
                || Mouse.left_click ()
                || Mouse.right_click ()
            then
                this.Dismiss()

            if Mouse.hover dropdown.Bounds then
                Input.finish_frame_events ()

    member this.Show<'T when 'T :> Widget and 'T :> IHeight>(dropdown: 'T) =
        dropdown.Position <- positioning (dropdown :> IHeight)
        current <- Some dropdown
        if this.Initialised then
            if not dropdown.Initialised then
                dropdown.Init this
        swapped_last_frame <- true
        if not dropdown.Focused then dropdown.Focus false

    member this.Dismiss() = current <- None

    member this.Toggle(thunk: unit -> 'T) =
        if current.IsSome then this.Dismiss()
        else this.Show(thunk())

/// Represents a dropdown menu where items represent different values for one setting, supports one value being pre-selected
type DropdownOptions<'T when 'T: equality> =
    {
        Items: ('T * string) seq
        Setting: Setting<'T>
        ColorFunc: 'T -> Color * Color
    }

type Dropdown<'T when 'T: equality>(options: DropdownOptions<'T>) as this =
    inherit FrameContainer(NodeType.Container(fun _ -> Some this.Items), Fill = !%Palette.DARK, Border = !%Palette.LIGHT)

    let flow = FlowContainer.Vertical(Dropdown.ITEMSIZE, Floating = true)

    override this.Init(parent: Widget) =
        let mutable what_to_focus: Widget = this

        flow
        |+ seq {
            for i, (value, label) in Seq.indexed options.Items do
                let item =
                    Dropdown.Item(
                        label,
                        i % 2 = 1,
                        options.ColorFunc value,
                        fun () ->
                            Selection.up false
                            options.Setting.Set value
                    )

                if value = options.Setting.Value then
                    what_to_focus <- item

                yield item
        }
        |> ScrollContainer
        |> this.Add

        base.Init parent
        what_to_focus.Focus false

    member private this.Items = flow

    interface IHeight with
        member this.Height = float32 (Seq.length options.Items) * Dropdown.ITEMSIZE

/// Represents a dropdown where each item represents a menu option
type DropdownMenuOptions =
    {
        Items: ((unit -> unit) * string) seq
    }

type DropdownMenu(options: DropdownMenuOptions) as this =
    inherit FrameContainer(NodeType.Container(fun _ -> Some this.Items), Fill = !%Palette.DARK, Border = !%Palette.LIGHT)

    let flow = FlowContainer.Vertical(Dropdown.ITEMSIZE, Floating = true)

    override this.Init(parent: Widget) =
        flow
        |+ seq {
            for i, (action, label) in Seq.indexed options.Items do
                yield
                    Dropdown.Item(
                        label,
                        i % 2 = 1,
                        Colors.text,
                        fun () ->
                            Selection.up false
                            action ()
                    )
        }
        |> ScrollContainer
        |> this.Add

        base.Init parent

    member private this.Items = flow
        
    interface IHeight with
        member this.Height = float32 (Seq.length options.Items) * Dropdown.ITEMSIZE