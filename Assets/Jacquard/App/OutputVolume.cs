using System;
using UnityEngine;

namespace Jacquard.App {

// How loud the finished mix leaves the app.
//
// One number, and it is the last one in the whole path. Everything above it is a
// decision about the sound — a level per note, a send per channel, a limiter across the
// sum of them — and every one of those lands the mix at full scale by construction: the
// make-up gives back whatever the threshold took off, and the soft clip rounds off what
// is left over the top. So there was nowhere to say *and play it this loud*, and the
// only answer to a mix that was too much for a pair of headphones was to undo the mix.
//
// It goes after the soft clip, which is the whole of why it is a separate number rather
// than a second make-up. A gain in front of the clip is not a level at all: it moves the
// mix into or out of the clipping, so turning it down takes the edge off the sound and
// turning it up puts it back. After the clip nothing downstream can hear the difference
// — the shape is settled and this scales it — which is what makes it the one control
// here that changes only how loud the piece is.
//
// It only goes down, for the same reason. What arrives at it cannot exceed full scale,
// so a volume above unity would be asking the device to clip what the soft clip was
// careful not to, and squarely rather than roundly. Unity is the top of the bar and
// where every machine starts, so an install that never opens the System panel sounds
// exactly as it did before there was one.
//
// It belongs to the machine and not to the piece, which puts it in PlayerPrefs beside
// the buffer size and the auditioning rather than in the file. The argument for the
// other side is real — a mix is left at a level, and a project driven hard into the
// limiter is finished quieter as surely as it is finished with the delay at that
// feedback — but what a hand actually reaches for this for is the room it is in: a pair
// of headphones at midnight, a speaker across a desk, the phone it was carried out on.
// None of that travels with the file, and a volume that did would arrive on somebody
// else's machine as an instruction about their room. So the mix stays at full scale
// wherever it is opened and this says how loud that is played here.
//
// Kept the way the buffer size is kept: the write does not flush, because a drag crosses
// every value between two of them and a flush is a write to disk. The hand coming off is
// what commits it. See Flush and DspBuffer.
//
// In decibels, which only the limiter's threshold is otherwise, and for its reason: it
// is a ratio of amplitude, so a linear bar spends most of its travel in the top doubling
// and reads out as a multiplier nobody thinks in. Where the two part company is the
// shape of the bar over those decibels — the threshold's is straight and this one's is
// not, since only one of them is played across the whole of its range. That belongs to
// the panel and is argued there.

// Public where the other two settings this panel keeps are not, for the reason the mix
// buses are: the self test drives the conversion below to check what the bar's bottom
// end does, and it runs from the editor assembly.
public static class OutputVolume
{
    // What every machine starts at: unity, which is what the output stage did before
    // there was anything to set.
    public const float Default = 0.0f;

    // The two ends of the bar. They are here rather than in the UI for the reason the
    // limiter's are: what a number is useful over belongs with the number.
    //
    // The bottom is silence rather than 60dB down, which is a decision and not a
    // rounding. A volume control whose lowest setting still lets something through is
    // one a hand cannot trust — the point of taking it to the bottom is to stop the
    // sound — and a thousandth of full scale is inaudible in every room but the one
    // where it is the only sound. So the bar's last position is off, and the readout
    // says so.
    public const float MinVolume = -60.0f;

    // What the setting says, in dB below full scale.
    //
    // Read once a frame, on the way to the audio thread with the rest of the mix
    // settings, which is a lookup in the table PlayerPrefs already holds in memory and
    // not a visit to the disk. The disk is Flush, and only the hand coming off asks for
    // it.
    public static float Decibels
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(Key, Default), MinVolume, 0.0f);
        set => PlayerPrefs.SetFloat(Key, Mathf.Clamp(value, MinVolume, 0.0f));
    }

    public static void Flush() => PlayerPrefs.Save();

    // The setting as the gain the audio thread multiplies by, with the floor folded in.
    // It is the limiter's own conversion with one branch added rather than a call to it,
    // since the branch is the difference between a level and a switch and it belongs to
    // this number alone.
    public static float Gain(float decibels)
      => decibels <= MinVolume ? 0.0f : MathF.Pow(10.0f, decibels / 20.0f);

    const string Key = "Jacquard.OutputVolume";
}

} // namespace Jacquard.App
