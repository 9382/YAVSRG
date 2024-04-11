﻿namespace Percyqaz.Flux.UI

open System
open Percyqaz.Common
open Percyqaz.Flux.Input
open Percyqaz.Flux.Utils

/// Container that automatically positions its contents stacked in Vertical/Horizontal arrangements
/// Each item requests a size (height or width respectively depending on container direction) and can signal to the container that this size has changed
module DynamicFlowContainer =

    type FlowItem<'T when 'T :> Widget and 'T :> IResize> = { Widget: 'T; mutable Visible: bool }

    [<AbstractClass>]
    type Base<'T when 'T :> Widget and 'T :> IResize>() as this =
        inherit StaticWidget(NodeType.Container(fun _ -> this.WhoShouldFocus))

        let mutable filter: 'T -> bool = K true
        let mutable sort: ('T -> 'T -> int) option = None
        let mutable spacing = 0.0f
        let mutable refresh = true
        let mutable refresh_visible = false
        let mutable last_selected = 0
        let children = ResizeArray<FlowItem<'T>>()

        /// Total children in this container, visible or not
        member this.Count = children.Count

        member private this.WhoIsFocused: int option =
            Seq.tryFindIndex (fun c -> c.Widget.Focused) children

        member private this.WhoShouldFocus: ISelection option =
            if children.Count = 0 then
                None
            else

                if last_selected >= children.Count then
                    last_selected <- 0

                Some children.[last_selected].Widget

        override this.Focusable = if children.Count = 0 then false else base.Focusable

        member this.Previous() =
            match this.WhoIsFocused with
            | Some i ->
                let mutable index = (i + children.Count - 1) % children.Count

                while index <> i
                      && (not children.[index].Visible || not children.[index].Widget.Focusable) do
                    index <- (index + children.Count - 1) % children.Count

                last_selected <- index
                children.[index].Widget.Focus false
            | None -> ()

        member this.Next() =
            match this.WhoIsFocused with
            | Some i ->
                let mutable index = (i + 1) % children.Count

                while index <> i
                      && (not children.[index].Visible || not children.[index].Widget.Focusable) do
                    index <- (index + 1) % children.Count

                last_selected <- index
                children.[index].Widget.Focus false
            | None -> ()

        member this.SelectFocusedChild() =
            match this.WhoIsFocused with
            | Some i ->
                last_selected <- i
                children.[i].Widget.Select false
            | None -> ()

        override this.OnUnfocus(by_mouse: bool) =
            base.OnUnfocus by_mouse

            match this.WhoIsFocused with
            | Some i -> last_selected <- i
            | None -> ()

        abstract member Navigate: unit -> unit

        member this.Filter
            with set value =
                filter <- value
                refresh_visible <- true
                refresh <- true

        member this.Sort
            with set (comp: 'T -> 'T -> int) =
                sort <- Some comp
                children.Sort(fun { Widget = a } { Widget = b } -> comp a b)
                refresh <- true

        member this.Spacing
            with get () = spacing
            and set (value) =
                spacing <- value
                refresh <- true

        member val Floating = false with get, set
        member val AllowNavigation = true with get, set

        member this.Clear() =
            require_ui_thread ()
            this.Iter(fun c -> c.OnSizeChanged <- ignore)
            children.Clear()

        override this.Draw() =
            for i = children.Count - 1 downto 0 do
                let { Widget = c; Visible = visible } = children.[i]

                if visible && (this.Floating || c.VisibleBounds.Visible) then
                    c.Draw()

        override this.Update(elapsed_ms, moved) =
            base.Update(elapsed_ms, moved || refresh)

            let moved =
                if refresh_visible then
                    for c in children do
                        c.Visible <- filter c.Widget

                    refresh_visible <- false

                if refresh then
                    refresh <- false
                    this.FlowContent children
                    true
                else
                    moved

            for { Widget = c; Visible = visible } in children do
                if visible && (moved || this.Floating || c.VisibleBounds.Visible) then
                    c.Update(elapsed_ms, moved)

            if this.AllowNavigation && this.Focused then
                this.Navigate()

        abstract member FlowContent: ResizeArray<FlowItem<'T>> -> unit

        member this.Add(child: 'T) =
            require_ui_thread ()

            children.Add
                {
                    Widget = child
                    Visible = filter child
                }

            match sort with
            | Some comp -> children.Sort(fun { Widget = a } { Widget = b } -> comp a b)
            | None -> ()

            if this.Initialised then
                child.Init this
                child.OnSizeChanged <- fun () -> refresh <- true
                refresh <- true

        member this.Remove(child: 'T) =
            require_ui_thread ()

            match Seq.tryFind (fun { Widget = c } -> Object.ReferenceEquals(c, child)) children with
            | Some x ->
                children.Remove x |> ignore
                child.OnSizeChanged <- ignore
                refresh <- true
            | None -> Logging.Error(sprintf "%O is not in flow container %O, can't remove" child this)

        override this.Init(parent: Widget) =
            base.Init parent
            this.FlowContent children

            for { Widget = child } in children do
                child.Init this
                child.OnSizeChanged <- fun () -> refresh <- true

        member this.Iter(f: 'T -> unit) =
            require_ui_thread ()

            for { Widget = c } in children do
                f c

        static member (|+)(parent: #Base<'T>, child: 'T) =
            parent.Add child
            parent

        static member (|+)(parent: #Base<'T>, children: 'T seq) =
            Seq.iter parent.Add children
            parent

        static member (|*)(parent: #Base<'T>, child: 'T) = parent.Add child
        static member (|*)(parent: #Base<'T>, children: 'T seq) = Seq.iter parent.Add children

    [<Sealed>]
    type Vertical<'T when 'T :> Widget and 'T :> IResize and 'T :> IHeight>() =
        inherit Base<'T>()

        let mutable size_change = ignore
        let mutable content_height = 0.0f

        override this.FlowContent children =
            let mutable t = 0.0f
            let mutable b = 0.0f

            for { Widget = c; Visible = visible } in children do
                if visible then
                    let height = c.Height
                    c.Position <- Position.Row(t, height)
                    b <- t + height
                    t <- t + height + this.Spacing

            if b <> content_height then
                content_height <- b
                size_change ()

        override this.Navigate() =
            if (%%"up").Tapped() then
                this.Previous()

            if (%%"down").Tapped() then
                this.Next()

            if (%%"select").Tapped() then
                this.SelectFocusedChild()

        override this.Init(parent) = base.Init parent

        interface IHeight with
            member this.Height = content_height

        interface IResize with
            member this.OnSizeChanged
                with set v = size_change <- v

    [<Sealed>]
    type LeftToRight<'T when 'T :> Widget and 'T :> IResize and 'T :> IWidth>() =
        inherit Base<'T>()

        let mutable size_change = ignore
        let mutable content_width = 0.0f

        override this.FlowContent children =
            let mutable l = 0.0f
            let mutable r = 0.0f

            for { Widget = c; Visible = visible } in children do
                if visible then
                    let width = c.Width
                    c.Position <- Position.Column(l, width)
                    r <- l + width
                    l <- l + width + this.Spacing

            if r <> content_width then
                content_width <- r
                size_change ()

        override this.Navigate() =
            if (%%"left").Tapped() then
                this.Previous()

            if (%%"right").Tapped() then
                this.Next()

            if (%%"select").Tapped() then
                this.SelectFocusedChild()

        interface IWidth with
            member this.Width = content_width

        interface IResize with
            member this.OnSizeChanged
                with set v = size_change <- v

    [<Sealed>]
    type RightToLeft<'T when 'T :> Widget and 'T :> IWidth and 'T :> IResize>() =
        inherit Base<'T>()

        let mutable size_change = ignore
        let mutable content_width = 0.0f

        override this.FlowContent children =
            let mutable r = 0.0f
            let mutable l = 0.0f

            for { Widget = c; Visible = visible } in children do
                if visible then
                    let width = c.Width

                    c.Position <-
                        {
                            Left = 1.0f %- (r + width)
                            Top = Position.min
                            Right = 1.0f %- r
                            Bottom = Position.max
                        }

                    l <- r + width
                    r <- r + width + this.Spacing

                if l <> content_width then
                    content_width <- r
                    size_change ()

        override this.Navigate() =
            if (%%"left").Tapped() then
                this.Next()

            if (%%"right").Tapped() then
                this.Previous()

            if (%%"select").Tapped() then
                this.SelectFocusedChild()

        interface IWidth with
            member this.Width = content_width

        interface IResize with
            member this.OnSizeChanged
                with set v = size_change <- v
