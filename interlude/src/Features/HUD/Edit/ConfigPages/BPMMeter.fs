namespace Interlude.Features.Noteskins.Edit
open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude
open Prelude.Skinning.Noteskins
open Interlude.Content
open Interlude.UI.Menu

// currently unused
type BPMMeterPage(on_close: unit -> unit) =
    inherit Page()

    let noteskin_options = Content.NoteskinConfig.HUD

    let pos = Setting.simple noteskin_options.BPMMeterPosition

    let preview =
        { new ConfigPreview(0.35f, pos) with
            override this.DrawComponent(bounds) =
                Text.fill_b (Style.font, "727 BPM", bounds, Colors.text_subheading, Alignment.CENTER)
        }

    override this.Content() = preview

    override this.Title = %"hud.bpmmeter"
    override this.OnDestroy() = preview.Destroy()

    override this.OnClose() =
        on_close ()
