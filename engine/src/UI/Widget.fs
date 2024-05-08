﻿namespace Percyqaz.Flux.UI

open Percyqaz.Flux.Graphics

[<AbstractClass>]
type Widget(node_type) =
    inherit ISelection(node_type)

    let mutable _parent = None
    let mutable focused = false
    let mutable focused_by_mouse = false
    let mutable selected = false

    member this.Selected = selected
    member this.Focused = focused
    member this.FocusedByMouse = focused_by_mouse

    member this.Parent =
        match _parent with
        | Some p -> p
        | None -> failwithf "%O has no parent (probably due to not calling init)" this

    member val Initialised = false with get, set
    member val Bounds = Rect.ZERO with get, set
    member val VisibleBounds = Rect.ZERO with get, set

    abstract member Position: Position with set
    abstract member Update: float * bool -> unit

    abstract member Draw: unit -> unit

    // The container must call this before calling Draw or Update
    abstract member Init: Widget -> unit

    default this.Init(parent: Widget) =
        if this.Initialised then
            failwithf "This widget %O has already been initialised" this

        this.Initialised <- true
        _parent <- Some parent

    override this.FocusTree: ISelection list =
        if not this.NodeType._IsNone then
            this :: this.Parent.FocusTree
        else
            this.Parent.FocusTree

    member this.Focus(by_mouse: bool) =
        if this.Focusable then
            Selection.focus by_mouse this

    member this.Select(by_mouse: bool) =
        if this.Focusable then
            Selection.select by_mouse this

    override this.OnFocus by_mouse =
        if not focused then
            focused <- true
            focused_by_mouse <- by_mouse

    override this.OnUnfocus _ =
        focused <- false
        focused_by_mouse <- false

    override this.OnSelected _ = selected <- true
    override this.OnDeselected _ = selected <- false

    override this.ToString() =
        if _parent.IsNone then "*" else _parent.Value.ToString()
        + " > "
        + this.GetType().Name
    
    static member inline (|>>)(child: Widget, constructor: NodeType -> 'T) : 'T =
        constructor (NodeType.Container (fun () -> Some child)) |+ child

[<AbstractClass>]
type StaticWidget(node_type) =
    inherit Widget(node_type)

    let mutable pos = Position.Default

    override this.Position
        with set (value) =
            pos <- value

            if this.Initialised then
                this.UpdateBounds()

    member private this.UpdateBounds() =
        this.Bounds <- Position.calculate pos this.Parent.Bounds
        this.VisibleBounds <- this.Bounds.Intersect this.Parent.VisibleBounds

    override this.Update(elapsed_ms, moved) =
        if moved then
            this.UpdateBounds()

    override this.Init(parent: Widget) =
        base.Init parent
        this.UpdateBounds()

[<AbstractClass>]
type Overlay(node_type: NodeType) =
    inherit Widget(node_type)

    override this.Position
        with set _ = failwith "Position can not be set for overlay components"

    override this.Init(parent: Widget) =
        base.Init parent
        this.Bounds <- Viewport.bounds
        this.VisibleBounds <- Viewport.bounds

    override this.Update(elapsed_ms, moved) =
        if moved then
            this.Bounds <- Viewport.bounds
            this.VisibleBounds <- Viewport.bounds

[<Sealed>]
type Dummy() =
    inherit StaticWidget(NodeType.None)
    override this.Draw() = ()