﻿namespace Prelude.Tests.Noteskins

open NUnit.Framework
open Prelude
open Prelude.Skins
open Prelude.Skins.Noteskins
open Helpers

module Validation =

    let ONEPIXELIMAGE = new Bitmap(1, 1)

    [<Test>]
    let MissingTextures () =
        let noteskin =
            InMemoryNoteskinBuilder(NoteskinConfig.Default)
                .AddImageFile("holdbody.png", ONEPIXELIMAGE)
                .AddImageFile("holdhead.png", ONEPIXELIMAGE)
                .AddImageFile("holdtail.png", ONEPIXELIMAGE)
                .AddImageFile("receptorlighting.png", ONEPIXELIMAGE)
                .Build()

        let validation_results = noteskin.Validate() |> Array.ofSeq

        printfn "%A" validation_results

        Assert.AreEqual(2, validation_results.Length)

        match
            Array.tryFind
                (function
                | ValidationError { Element = "note" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected an error message for missing 'note' texture")
        | _ -> ()

        match
            Array.tryFind
                (function
                | ValidationError { Element = "receptor" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected an error message for missing 'receptor' texture")
        | _ -> ()

    [<Test>]
    let WithRequiredTextures () =
        let noteskin =
            InMemoryNoteskinBuilder(NoteskinConfig.Default)
                .AddImageFile("note.png", ONEPIXELIMAGE)
                .AddImageFile("holdbody.png", ONEPIXELIMAGE)
                .AddImageFile("holdhead.png", ONEPIXELIMAGE)
                .AddImageFile("holdtail.png", ONEPIXELIMAGE)
                .AddImageFile("receptor.png", ONEPIXELIMAGE)
                .AddImageFile("receptorlighting.png", ONEPIXELIMAGE)
                .Build()

        let validation_results = noteskin.Validate() |> Array.ofSeq

        printfn "%A" validation_results

        Assert.AreEqual(0, validation_results.Length)

    [<Test>]
    let WithExtraTextures () =
        let noteskin =
            InMemoryNoteskinBuilder(
                { NoteskinConfig.Default with
                    EnableColumnLight = false
                }
            )
                .AddImageFile("note[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdbody[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdhead[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdtail[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("receptor[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("receptorlighting[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("noteexplosion[1x1].png", ONEPIXELIMAGE)
                .Build()

        let validation_results = noteskin.Validate() |> Array.ofSeq

        printfn "%A" validation_results

        Assert.AreEqual(2, validation_results.Length)

        match
            Array.tryFind
                (function
                | ValidationWarning { Element = "noteexplosion" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected a warning message for unnecessary 'noteexplosion' texture")
        | _ -> ()

        match
            Array.tryFind
                (function
                | ValidationWarning { Element = "receptorlighting" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected a warning message for unnecessary 'receptorlighting' texture")
        | _ -> ()

    [<Test>]
    let WithWrongNoteTextureModeLoose () =
        let noteskin =
            InMemoryNoteskinBuilder(NoteskinConfig.Default)
                .AddImageFile("note[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdbody[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdhead[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdtail[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("receptor[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("receptorlighting[1x1].png", ONEPIXELIMAGE)
                .Build()

        let validation_results = noteskin.Validate() |> Array.ofSeq

        printfn "%A" validation_results

        Assert.AreEqual(1, validation_results.Length)

        match
            Array.tryFind
                (function
                | ValidationError { Element = "note" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected an error message for wrong mode on 'note' texture")
        | _ -> ()

    [<Test>]
    let WithWrongNoteTextureModeGrid () =
        let noteskin =
            InMemoryNoteskinBuilder(NoteskinConfig.Default)
                .AddImageFile("note-0-0.png", ONEPIXELIMAGE)
                .AddImageFile("holdbody.png", ONEPIXELIMAGE)
                .AddImageFile("holdhead.png", ONEPIXELIMAGE)
                .AddImageFile("holdtail.png", ONEPIXELIMAGE)
                .AddImageFile("receptor.png", ONEPIXELIMAGE)
                .AddImageFile("receptorlighting.png", ONEPIXELIMAGE)
                .Build()

        let validation_results = noteskin.Validate() |> Array.ofSeq

        printfn "%A" validation_results

        Assert.AreEqual(1, validation_results.Length)

        match
            Array.tryFind
                (function
                | ValidationError { Element = "note" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected an error message for wrong mode on 'note' texture")
        | _ -> ()

    [<Test>]
    let WithTooManyRows () =
        use receptor_grid_bmp = new Bitmap(16, 3)

        let noteskin =
            InMemoryNoteskinBuilder(NoteskinConfig.Default)
                .AddImageFile("note[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdbody[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdhead[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdtail[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("receptor[16x21].png", receptor_grid_bmp)
                .AddImageFile("receptorlighting[1x1].png", ONEPIXELIMAGE)
                .Build()

        let validation_results = noteskin.Validate() |> Array.ofSeq

        printfn "%A" validation_results

        Assert.AreEqual(1, validation_results.Length)

        match
            Array.tryFind
                (function
                | ValidationError { Element = "receptor" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected an error message for row count on 'receptor' texture")
        | _ -> ()

    [<Test>]
    let WithWrongGridImageDimensions () =
        use receptor_grid_bmp = new Bitmap(16, 24)

        let noteskin =
            InMemoryNoteskinBuilder(
                { NoteskinConfig.Default with
                    ReceptorStyle = ReceptorStyle.Receptors
                }
            )
                .AddImageFile("note[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdbody[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdhead[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdtail[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("receptor[16x2].png", receptor_grid_bmp)
                .AddImageFile("receptorlighting[1x1].png", ONEPIXELIMAGE)
                .Build()

        let validation_results = noteskin.Validate() |> Array.ofSeq

        printfn "%A" validation_results

        Assert.AreEqual(1, validation_results.Length)

        match
            Array.tryFind
                (function
                | ValidationError { Element = "receptor" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected an error message for image size on 'receptor' texture")
        | _ -> ()

    [<Test>]
    let WithRowsAndColumnsBackwards () =
        use receptor_grid_bmp = new Bitmap(200, 700)

        let noteskin =
            InMemoryNoteskinBuilder(
                { NoteskinConfig.Default with
                    ReceptorStyle = ReceptorStyle.Receptors
                }
            )
                .AddImageFile("note[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdbody[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdhead[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("holdtail[1x1].png", ONEPIXELIMAGE)
                .AddImageFile("receptor[7x2].png", receptor_grid_bmp)
                .AddImageFile("receptorlighting[1x1].png", ONEPIXELIMAGE)
                .Build()

        let validation_results = noteskin.Validate() |> Array.ofSeq

        printfn "%A" validation_results

        Assert.AreEqual(1, validation_results.Length)

        match
            Array.tryFind
                (function
                | ValidationError { Element = "receptor" } -> true
                | _ -> false)
                validation_results
        with
        | None -> Assert.Fail("Expected an error message for image size on 'receptor' texture")
        | _ -> ()
