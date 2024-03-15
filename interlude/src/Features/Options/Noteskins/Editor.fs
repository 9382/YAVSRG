﻿namespace Interlude.Features.OptionsMenu.Noteskins

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude.Content.Noteskins
open Interlude.Content
open Interlude.Utils
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Features.OptionsMenu.Gameplay

type EditNoteskinPage(from_hotkey: bool) as this =
    inherit Page()

    let data = Content.NoteskinConfig

    let name = Setting.simple data.Name

    let preview = NoteskinPreview(0.35f, true)

    let grid =
        GridFlowContainer<TextureCard>(
            150.0f,
            6,
            WrapNavigation = false,
            Spacing = (15.0f, 15.0f),
            Position = Position.Box(0.0f, 0.0f, 100.0f, 680.0f, 975.0f, 315.0f)
        )

    let refresh_texture_grid () =
        grid.Clear()

        for texture in
            Content.Noteskin.RequiredTextures
            |> Seq.except [ "receptorlighting"; "stageleft"; "stageright" ] do
            grid |* TextureCard(texture, (fun () -> TextureEditPage(texture).Show()))

    do
        refresh_texture_grid ()
        let pos = menu_pos 2.0f

        column ()
        |+ PageTextEntry("noteskins.edit.noteskinname", name)
            .Pos(pos.Step 1.5f, PRETTYWIDTH, PRETTYHEIGHT)
        |+ PageButton(
            "noteskins.edit.playfield",
            fun () ->
                { new PlayfieldSettingsPage() with
                    override this.OnClose() =
                        base.OnClose()
                        preview.Refresh()
                }
                    .Show()
        )
            .Tooltip(Tooltip.Info("noteskins.edit.playfield"))
            .Pos(pos.Step())
        |+ PageButton(
            "noteskins.edit.holdnotes",
            fun () ->
                { new HoldNoteSettingsPage() with
                    override this.OnClose() =
                        base.OnClose()
                        preview.Refresh()
                }
                    .Show()
        )
            .Tooltip(Tooltip.Info("noteskins.edit.holdnotes"))
            .Pos(pos.Step())
        |+ PageButton(
            "noteskins.edit.colors",
            fun () ->
                { new ColorSettingsPage() with
                    override this.OnClose() =
                        base.OnClose()
                        preview.Refresh()
                }
                    .Show()
        )
            .Tooltip(Tooltip.Info("noteskins.edit.colors"))
            .Pos(pos.Step())
        |+ PageButton(
            "noteskins.edit.rotations",
            fun () ->
                { new RotationSettingsPage() with
                    override this.OnClose() =
                        base.OnClose()
                        preview.Refresh()
                }
                    .Show()
        )
            .Tooltip(Tooltip.Info("noteskins.edit.rotations"))
            .Pos(pos.Step())
        |+ PageButton(
            "noteskins.animations",
            fun () ->
                { new AnimationSettingsPage() with
                    override this.OnClose() =
                        base.OnClose()
                        preview.Refresh()
                }
                    .Show()
        )
            .Tooltip(Tooltip.Info("noteskins.animations"))
            .Pos(pos.Step())
        |+ grid
        |+ preview
        |> this.Content

        this.Add(
            Conditional(
                (fun () -> not from_hotkey),
                Callout.frame
                    (Callout.Small
                        .Icon(Icons.INFO)
                        .Title(%"noteskins.edit.hotkey_hint")
                        .Hotkey("edit_noteskin"))
                    (fun (w, h) -> Position.SliceTop(h + 40.0f + 40.0f).SliceRight(w + 40.0f).Margin(20.0f, 20.0f))
            )
        )

    override this.Title = data.Name
    override this.OnDestroy() = preview.Destroy()

    override this.OnReturnTo() =
        refresh_texture_grid ()
        base.OnReturnTo()

    override this.OnClose() =
        Noteskins.save_config
            { Content.NoteskinConfig with
                Name = name.Value
            }
