﻿namespace Percyqaz.Flux.Audio

open System
open ManagedBass
open Percyqaz.Common

module Waveform =

    [<Struct>]
    type Point = 
        { 
            Left: float32
            Right: float32
            mutable Low: float32
            mutable Mid: float32
            mutable High: float32
        }

    type Waveform =
        {
            MsPerPoint: float32<ms>
            Points: Point array
        }


    let private samples_per_second = 1000
    let private points_per_iteration = 1000

    // fft stuff
    let private fft_samples = DataFlags.FFT1024
    let private fft_bins = 512
    let private low_min = 20f
    let private mid_min = 100f
    let private high_min = 2000f
    let private high_max = 12000f

    let pt l r = { Left = l * l; Right = r * r; Low = 0.0f; Mid = 0.0f; High = 0.0f }

    let intensity(info: ChannelInfo, bins: float32 array, startFrequency : float32, endFrequency: float32) =
        let startBin = int <| float32 fft_bins * 2f * startFrequency / float32 info.Frequency
        let endBin = int <| float32 fft_bins * 2f * endFrequency / float32 info.Frequency

        let startBin = Math.Clamp(startBin, 0, bins.Length);
        let endBin = Math.Clamp(endBin, 0, bins.Length);

        let mutable value = 0f
        for i in startBin .. endBin - 1 do
            value <- value + bins.[i]
        value

    let generate(file: string) =
        let decode_stream = Bass.CreateStream(file, 0L, 0L, BassFlags.Decode ||| BassFlags.Float)
        let info = Bass.ChannelGetInfo(decode_stream)
        let right_channel_offset = if info.Channels > 1 then 1 else 0
        let length = Bass.ChannelGetLength(decode_stream)
        let samples_per_point = info.Frequency * info.Channels / samples_per_second
        let bytes_per_point = samples_per_point * 4
        let point_count = length / int64 bytes_per_point |> int
        let points = Array.zeroCreate<Point> point_count
        let bytes_per_iteration = bytes_per_point * points_per_iteration
        let sample_buffer = Array.zeroCreate<float32> (bytes_per_iteration / 4)
        let mutable point_index = 0

        // amplitude analysis
        let mutable read = 1
        while read > 0 do
            read <- Bass.ChannelGetData(decode_stream, sample_buffer, bytes_per_iteration)
            let samples_read = read / 4
            let mutable i = 0
            while i < samples_read && point_index < point_count do
                let mutable left = 0.0f
                let mutable right = 0.0f
                let mutable j = i
                while j < i + samples_per_point do
                    left <- max left (abs sample_buffer.[j])
                    right <- max right (abs sample_buffer.[j + right_channel_offset])
                    j <- j + info.Channels

                left <- min left 1.0f
                right <- min right 1.0f

                points.[point_index] <- pt left right
                point_index <- point_index + 1

                i <- i + samples_per_point

        // fft analysis
        Bass.ChannelSetPosition(decode_stream, 0L) |> ignore
        let bins = Array.zeroCreate<float32> fft_bins
        let mutable point_index = 0
        let mutable byte_index = 0
        let mutable read = 1
        while read > 0 do
            read <- Bass.ChannelGetData(decode_stream, bins, int fft_samples)
            byte_index <- byte_index + read

            let lo = intensity(info, bins, low_min, mid_min)
            let mid = intensity(info, bins, mid_min, high_min)
            let hi = intensity(info, bins, high_min, high_max)

            while point_index < points.Length && point_index * bytes_per_point < byte_index do
                points.[point_index].Low <- lo
                points.[point_index].Mid <- mid
                points.[point_index].High <- hi
                point_index <- point_index + 1

        Bass.StreamFree(decode_stream) |> ignore

        let points_per_second = (info.Frequency * info.Channels / samples_per_point)
        let ms_per_point = 1000.0f<ms> / float32 points_per_second

        { Points = points; MsPerPoint = ms_per_point }
            
