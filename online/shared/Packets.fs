﻿namespace Interlude.Web.Shared

open System
open System.IO

[<AutoOpen>]
module Packets =

    let PROTOCOL_VERSION = 0uy

    type LobbyChart =
        {
            Hash: string
            Artist: string
            Title: string
            Rate: float32
        }
        member this.Write(bw: BinaryWriter) =
            bw.Write this.Hash
            bw.Write this.Artist
            bw.Write this.Title
            bw.Write this.Rate
        static member Read(br: BinaryReader) =
            {
                Hash = br.ReadString()
                Artist = br.ReadString()
                Title = br.ReadString()
                Rate = br.ReadSingle()
            }

    type LobbySettings =
        {
            Name: string
        }

    type LobbyInfo =
        {
            Id: Guid
            Name: string
            Players: byte
            CurrentlyPlaying: string option
        }
        member this.Write(bw: BinaryWriter) =
            bw.Write (this.Id.ToByteArray())
            bw.Write this.Name
            bw.Write this.Players
            bw.Write (Option.defaultValue "" this.CurrentlyPlaying)
        static member Read(br: BinaryReader) =
            {
                Id = new Guid(br.ReadBytes 16)
                Name = br.ReadString()
                Players = br.ReadByte()
                CurrentlyPlaying = br.ReadString() |> function "" -> None | s -> Some s
            }

    [<RequireQualifiedAccess>]
    type Upstream =
        | DISCONNECT

        | VERSION of byte
        | LOGIN of username: string

        | GET_LOBBIES
        | JOIN_LOBBY of id: Guid
        | CREATE_LOBBY of name: string

        | INVITE_TO_LOBBY of username: string
        | LEAVE_LOBBY
        | CHAT of message: string
        | READY_STATUS of bool

        | IS_PLAYING
        | IS_SPECTATING
        | PLAY_DATA of byte array
        | FINISH_PLAY

        | TRANSFER_HOST of username: string
        | SELECT_CHART of LobbyChart
        | LOBBY_SETTINGS of LobbySettings
        | BEGIN_PLAYING

        | KICK_PLAYER of username: string

        static member Read(kind: byte, data: byte array) : Upstream =
            use ms = new MemoryStream(data)
            use br = new BinaryReader(ms)
            let packet = 
                match kind with
                | 0x00uy -> DISCONNECT
                | 0x01uy -> VERSION (br.ReadByte())
                | 0x02uy -> LOGIN (br.ReadString())

                | 0x10uy -> GET_LOBBIES
                | 0x11uy -> JOIN_LOBBY (new Guid(br.ReadBytes 16)) 
                | 0x12uy -> CREATE_LOBBY (br.ReadString())

                | 0x20uy -> INVITE_TO_LOBBY (br.ReadString())
                | 0x21uy -> LEAVE_LOBBY
                | 0x22uy -> CHAT (br.ReadString())
                | 0x23uy -> READY_STATUS (br.ReadBoolean())

                | 0x30uy -> IS_PLAYING
                | 0x31uy -> IS_SPECTATING
                | 0x32uy -> PLAY_DATA (br.ReadBytes(int (br.BaseStream.Length - br.BaseStream.Position)))
                | 0x33uy -> FINISH_PLAY

                | 0x40uy -> TRANSFER_HOST (br.ReadString())
                | 0x41uy -> SELECT_CHART (LobbyChart.Read br)
                | 0x42uy -> LOBBY_SETTINGS { Name = br.ReadString() }
                | 0x43uy -> BEGIN_PLAYING

                | 0x50uy -> KICK_PLAYER (br.ReadString())

                | _ -> failwithf "Unknown packet type: %i" kind
            if ms.Position <> ms.Length then failwithf "Expected end-of-packet but there are %i extra bytes" (ms.Length - ms.Position)
            packet

        member this.Write() : byte * byte array =
            use ms = new MemoryStream()
            use bw = new BinaryWriter(ms)
            let kind = 
                match this with
                | DISCONNECT -> 0x00uy
                | VERSION v -> bw.Write v; 0x01uy
                | LOGIN name -> bw.Write name; 0x02uy
                
                | GET_LOBBIES -> 0x10uy
                | JOIN_LOBBY id -> bw.Write (id.ToByteArray()); 0x11uy
                | CREATE_LOBBY name -> bw.Write name; 0x12uy
                
                | INVITE_TO_LOBBY username -> bw.Write username; 0x20uy
                | LEAVE_LOBBY -> 0x21uy
                | CHAT msg -> bw.Write msg; 0x22uy
                | READY_STATUS ready -> bw.Write ready; 0x23uy

                | IS_PLAYING -> 0x30uy
                | IS_SPECTATING -> 0x31uy
                | PLAY_DATA data -> bw.Write data; 0x32uy
                | FINISH_PLAY -> 0x33uy

                | TRANSFER_HOST username -> bw.Write username; 0x40uy
                | SELECT_CHART chart -> chart.Write bw; 0x41uy
                | LOBBY_SETTINGS settings -> bw.Write settings.Name; 0x42uy
                | BEGIN_PLAYING -> 0x43uy

                | KICK_PLAYER username -> bw.Write username; 0x50uy
            kind, ms.ToArray()
            
    [<RequireQualifiedAccess>]
    type Downstream =
        | DISCONNECT of reason: string
        | HANDSHAKE_SUCCESS
        | LOGIN_SUCCESS of username: string

        | LOBBY_LIST of lobbies: LobbyInfo array
        | YOU_JOINED_LOBBY of players: string array
        | INVITED_TO_LOBBY of by_who: string * id: Guid

        | YOU_LEFT_LOBBY
        | YOU_ARE_HOST
        | PLAYER_JOINED_LOBBY of username: string
        | PLAYER_LEFT_LOBBY of username: string
        | SELECT_CHART of LobbyChart
        | LOBBY_SETTINGS of LobbySettings
        | SYSTEM_MESSAGE of string
        | CHAT of sender: string * message: string
        | READY_STATUS of username: string * ready: bool

        | BEGIN_PLAYING
        | USER_IS_PLAYING of username: string
        | PLAY_DATA of username: string * data: byte array
        | FINISH_PLAYING

        static member Read(kind: byte, data: byte array) : Downstream =
            use ms = new MemoryStream(data)
            use br = new BinaryReader(ms)
            let packet = 
                match kind with
                | 0x00uy -> DISCONNECT (br.ReadString())
                | 0x01uy -> HANDSHAKE_SUCCESS
                | 0x02uy -> LOGIN_SUCCESS (br.ReadString())

                | 0x10uy -> LOBBY_LIST ( Array.init (br.ReadByte() |> int) (fun _ -> LobbyInfo.Read br) )
                | 0x11uy -> YOU_JOINED_LOBBY ( Array.init (br.ReadByte() |> int) (fun _ -> br.ReadString()) )
                | 0x12uy -> INVITED_TO_LOBBY (br.ReadString(), new Guid(br.ReadBytes 16))

                | 0x20uy -> YOU_LEFT_LOBBY
                | 0x21uy -> YOU_ARE_HOST
                | 0x22uy -> PLAYER_JOINED_LOBBY (br.ReadString())
                | 0x23uy -> PLAYER_LEFT_LOBBY (br.ReadString())
                | 0x24uy -> SELECT_CHART (LobbyChart.Read br)
                | 0x25uy -> LOBBY_SETTINGS { Name = br.ReadString() }
                | 0x26uy -> SYSTEM_MESSAGE (br.ReadString())
                | 0x27uy -> CHAT (br.ReadString(), br.ReadString())
                | 0x28uy -> READY_STATUS (br.ReadString(), br.ReadBoolean())

                | 0x30uy -> BEGIN_PLAYING
                | 0x31uy -> USER_IS_PLAYING (br.ReadString())
                | 0x32uy -> PLAY_DATA (br.ReadString(), br.ReadBytes(int (br.BaseStream.Length - br.BaseStream.Position)))
                | 0x33uy -> FINISH_PLAYING

                | _ -> failwithf "Unknown packet type: %i" kind
            if ms.Position <> ms.Length then failwithf "Expected end-of-packet but there are %i extra bytes" (ms.Length - ms.Position)
            packet

        member this.Write() : byte * byte array =
            use ms = new MemoryStream()
            use bw = new BinaryWriter(ms)
            let kind = 
                match this with
                | DISCONNECT reason -> bw.Write reason; 0x00uy
                | HANDSHAKE_SUCCESS -> 0x01uy
                | LOGIN_SUCCESS name -> bw.Write name; 0x02uy

                | LOBBY_LIST lobbies -> 
                    bw.Write (byte lobbies.Length)
                    for lobby in lobbies do lobby.Write bw
                    0x10uy
                | YOU_JOINED_LOBBY players -> 
                    bw.Write (byte players.Length)
                    for player in players do bw.Write player
                    0x11uy
                | INVITED_TO_LOBBY (by_who, id) -> bw.Write by_who; bw.Write (id.ToByteArray()); 0x12uy

                | YOU_LEFT_LOBBY -> 0x20uy
                | YOU_ARE_HOST -> 0x21uy
                | PLAYER_JOINED_LOBBY username -> bw.Write username; 0x22uy
                | PLAYER_LEFT_LOBBY username -> bw.Write username; 0x23uy
                | SELECT_CHART chart -> chart.Write bw; 0x24uy
                | LOBBY_SETTINGS settings -> bw.Write settings.Name; 0x25uy
                | SYSTEM_MESSAGE message -> bw.Write message; 0x26uy
                | CHAT (sender, msg) -> bw.Write sender; bw.Write msg; 0x27uy
                | READY_STATUS (username, ready) -> bw.Write username; bw.Write ready; 0x28uy
                
                | BEGIN_PLAYING -> 0x30uy
                | USER_IS_PLAYING username -> bw.Write username; 0x31uy
                | PLAY_DATA (username, data) -> bw.Write username; bw.Write data; 0x32uy
                | FINISH_PLAYING -> 0x33uy
            kind, ms.ToArray()