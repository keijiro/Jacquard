#if UNITY_WEBGL && !UNITY_EDITOR

using Unity.Mathematics;
using UnityEngine;

namespace Jacquard.App {

// The Web driver: the same DSP, rendered on the main thread and pushed at the
// browser.
//
// Nothing on this platform will call us for audio. The scriptable audio pipeline is
// not supported there, and neither is any of the older ways of being handed a buffer
// to fill — OnAudioFilterRead and AudioClip.PCMReaderCallback are both absent,
// because the browser mixes audio somewhere a WebAssembly main thread cannot be
// called from. What is left is to push: render blocks before they are needed and hand
// each one to the Web Audio API, which plays them back to back on a clock of its own.
//
// That clock is then the only one worth trusting, so it is also where CurrentSample
// comes from. The browser is asked how much of what was pushed is still unplayed, and
// the position is what has been rendered less that. It cannot drift from what is
// being heard, and a frame that takes too long shows up as a gap in the sound rather
// than as a synth that has quietly stopped agreeing with the sequencer about what
// time it is. What that costs is that the position moves in fits: it advances by the
// audio that was actually consumed, so a stall is a tape that dragged.
//
// The whole of the latency is here in two numbers. WriteAheadBlocks is how much
// finished audio is kept queued, and it buys the tolerance for a late frame that a
// real audio thread would not need — four blocks is around 85ms at 48kHz, which
// survives a dropped frame or two and not a hitch. MinimumLead is one block more than
// that, and is the one the hand can feel: a note cannot be placed in audio that has
// already been rendered, so the earliest a tap can sound is a block past whatever
// this Update has committed.

sealed class FmSynthWeb : IFmSynthBackend
{
    // What one Render fills. Larger is cheaper per frame and coarser in latency;
    // 1024 frames is a little over 20ms at the rates browsers hand out.
    const int BlockFrames = 1024;

    const int WriteAheadBlocks = 4;
    const int WriteAheadFrames = WriteAheadBlocks * BlockFrames;

    public int SampleRate { get; }

    public long CurrentSample => _playhead;

    // One block past what the queue covers. A note handed over during this Update
    // will not be rendered until the next one, by which time the play position has
    // moved on and the queue has been topped up from where it then stands — so the
    // block boundary is the margin, not the frame time.
    public long MinimumLead => WriteAheadFrames + BlockFrames;

    public FmSynthWeb(int maxVoices, float masterGain, int queueCapacity)
    {
        var rate = WebAudioOut.Open();

        // No audio context at all, which takes a browser old enough to lack the Web
        // Audio API. Keep going silently rather than freezing the sequencer, which is
        // what a clock that never advances would do to it.
        _open = rate > 0;
        if (!_open) Debug.LogError("No Web Audio output; the synth will be silent.");

        SampleRate = _open ? rate : 48000;

        _core.masterGain = masterGain;
        _core.Allocate(SampleRate, BlockFrames, maxVoices, queueCapacity);

        // A managed array crosses to JavaScript as an offset into the WebAssembly
        // heap, which is why the mix is copied out of its NativeArrays rather than
        // pushed from them. One copy per block is nothing next to what handing out
        // pointers into native memory would cost in unsafe code.
        _stageL = new float[BlockFrames];
        _stageR = new float[BlockFrames];
    }

    // Straight into the queue the render job reads. There is no audio thread to send
    // this to and so nothing to send it through.
    public bool Schedule(in FmNoteEvent note)
    {
        var accepted = _core.pool.QueuedCount() < _core.pool.queue.Length;
        _core.pool.Enqueue(note);
        return accepted;
    }

    public bool SetFx(in MixFxRuntime fx)
    {
        _fx = fx;
        return true;
    }

    public FmSynthStatus GetStatus() => _core.Status((ulong)_playhead);

    // Reads the clock, then renders whatever it takes to fill the queue back up.
    //
    // The loop cannot run away: every pass adds a block to what is queued, so the
    // most it will ever do in one Update is fill an empty queue. That is also what
    // recovery from a stall looks like — one queue's worth of catching up, and the
    // gap that was heard stays heard rather than being paid for again next frame.
    public void Pump()
    {
        if (!_open)
        {
            // Free-run, so that the transport and the playheads still move.
            _playhead += (long)(Time.unscaledDeltaTime * SampleRate);
            return;
        }

        var queued = WebAudioOut.Queued();

        // Never backwards. An underrun restarts the browser's queue a little way
        // ahead of its clock, which lands as a jump in what is queued rather than in
        // what has been rendered; the sequencer reads this as a playhead, and a
        // playhead that steps back is a note played twice.
        _playhead = math.max(_playhead, _rendered - queued);

        while (queued < WriteAheadFrames)
        {
            _core.Run(_rendered, _fx);

            _core.outL.CopyTo(_stageL);
            _core.outR.CopyTo(_stageR);
            WebAudioOut.Push(_stageL, _stageR, BlockFrames);

            _rendered += BlockFrames;
            queued += BlockFrames;
        }
    }

    public void Dispose()
    {
        _core.Release();
        if (_open) WebAudioOut.Close();
    }

    // Private members

    FmSynthCore _core;
    MixFxRuntime _fx;

    readonly bool _open;
    readonly float[] _stageL;
    readonly float[] _stageR;

    long _rendered; // Total frames handed to the browser
    long _playhead; // Of those, how many it says have been played
}

} // namespace Jacquard.App

#endif
