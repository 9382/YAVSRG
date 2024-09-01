namespace Interlude.Features.Import.Etterna

open System.IO
open System.Text.RegularExpressions
open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude
open Prelude.Skins.Conversions.Stepmania
open Interlude.UI
open Interlude.Content

module Skins =

    type ImportEtternaNoteskinPage(source_path: string, folder_name: string, existing_folder: string option) =
        inherit Page()

        let is_arrows: Setting<bool> = Setting.simple false
        let delete_existing: Setting<bool> = Setting.simple false

        override this.Content() =
            page_container()
            |+ PageSetting(%"etterna_skin_import.isarrows", Checkbox is_arrows)
                .Pos(2)
            |+ 
                match existing_folder with
                | Some folder ->
                    [
                        Text([folder] %> "etterna_skin_import.delete_prompt", Align = Alignment.LEFT, Position = pretty_pos(5, 2, PageWidth.Full).Shrink(Style.PADDING))
                        PageSetting(%"etterna_skin_import.delete_existing", Checkbox delete_existing).Pos(7)
                    ]
                | None -> []
            |+ PageButton
                .Once(
                    %"etterna_skin_import.confirm",
                    fun () ->
                        try
                            StepmaniaSkinConverter.convert_to_skin
                                source_path
                                (Path.Combine(get_game_folder "Skins", folder_name))
                                is_arrows.Value

                            if delete_existing.Value then 
                                try
                                    match existing_folder with
                                    | Some old_name ->
                                        let skin_path = Path.Combine(get_game_folder "Skins", old_name)
                                        if Directory.Exists skin_path then
                                            Directory.Delete(skin_path, true)
                                    | None -> failwith "impossible"
                                with err ->
                                    Logging.Error("Error deleting old skin")
                            Skins.load ()
                            Skins.selected_noteskin_id.Set folder_name
                            Skins.selected_hud_id.Set folder_name
                        with err ->
                            Logging.Error("Error while converting to skin", err)

                        Menu.Back()
                )
                .Pos(if existing_folder.IsSome then 10 else 5)
            |+ Callout.frame
                (Callout.Normal
                    .Icon(Icons.INFO)
                    .Title(%"etterna_skin_import.disclaimer.title")
                    .Body(%"etterna_skin_import.disclaimer.body")
                )
                (fun (w, h) -> pretty_pos(15, PAGE_BOTTOM - 15, PageWidth.Custom w))
            :> Widget

        override this.Title = Path.GetFileName source_path
        override this.OnClose() = ()

    let import_stepmania_noteskin (path: string) =
        let id = Regex("[^a-zA-Z0-9_-]").Replace(Path.GetFileName(path), "")
        let timestamp = "-" + System.DateTime.Now.ToString("ddMMyyyyHHmmss")

        let existing_id = Skins.list_noteskins() |> Seq.map (fun (id, _, _) -> id) |> Seq.tryFind (fun x -> x.StartsWith id)
        ImportEtternaNoteskinPage(
            path,
            id + timestamp,
            existing_id
        )
            .Show()