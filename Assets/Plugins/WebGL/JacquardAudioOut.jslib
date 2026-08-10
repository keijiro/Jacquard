// The browser half of Jacquard's audio output. Its caller is
// Assets/Jacquard/Audio/WebAudioOut.cs, and what it is for is written up in
// Assets/Jacquard/Audio/FmSynthWeb.cs.
//
// One block becomes one AudioBufferSourceNode started at the exact time the previous
// one ends, which is the only way to lay finished audio end to end without a callback
// on the audio thread. A node per block is more allocation than a ring buffer behind
// an AudioWorklet would be, but a worklet only helps if the samples can be written
// from the worklet's own thread, and reaching WebAssembly memory from there needs a
// SharedArrayBuffer — which needs the page to be served cross-origin isolated. That
// is a condition on where the build is hosted, and this way there is no condition at
// all.

var JacquardAudioOut = {

$JQAudio: {
  ctx: null,
  next: 0,        // Context time at which the audio pushed so far runs out
  restart: 0.02,  // How far ahead of the clock a dried-up queue starts again

  // Browsers withhold audio until the page has been interacted with, and a context
  // that is still suspended reports a currentTime that does not advance. Nothing has
  // to special-case that: a clock that has stopped looks exactly like a queue that is
  // full, so the synth simply renders nothing until this fires.
  //
  // Coming back is a fresh start for the queue, which is what clearing next says: the
  // clock does not resume where it stopped, so what was scheduled against the old one
  // is already in the past and the next block has to be placed rather than appended.
  unlock: function () {
    if (!JQAudio.ctx || JQAudio.ctx.state === 'running') return;
    JQAudio.ctx.resume().then(function () { JQAudio.next = 0; });
  },

  events: ['pointerdown', 'touchend', 'keydown']
},

JacquardAudioOpen: function () {
  var Ctx = window.AudioContext || window.webkitAudioContext;
  if (!Ctx) return 0;

  JQAudio.ctx = new Ctx({ latencyHint: 'interactive' });
  JQAudio.next = 0;

  JQAudio.events.forEach(function (name) {
    document.addEventListener(name, JQAudio.unlock, true);
  });

  JQAudio.unlock();

  return JQAudio.ctx.sampleRate | 0;
},

JacquardAudioQueued: function () {
  var ctx = JQAudio.ctx;
  if (!ctx) return 0;
  return Math.max(0, (JQAudio.next - ctx.currentTime) * ctx.sampleRate) | 0;
},

JacquardAudioPush: function (left, right, frames) {
  var ctx = JQAudio.ctx;
  if (!ctx) return;

  var buffer = ctx.createBuffer(2, frames, ctx.sampleRate);
  buffer.getChannelData(0).set(HEAPF32.subarray(left >> 2, (left >> 2) + frames));
  buffer.getChannelData(1).set(HEAPF32.subarray(right >> 2, (right >> 2) + frames));

  var source = ctx.createBufferSource();
  source.buffer = buffer;
  source.connect(ctx.destination);

  // Behind the clock means the queue ran dry: the gap has already been heard, and all
  // that is left to decide is where to start again. Warned about rather than counted,
  // because the thing it says about is the frame rate, which the browser's own tools
  // are better placed to explain.
  if (JQAudio.next < ctx.currentTime) {
    if (JQAudio.next > 0) console.warn('Jacquard: audio underrun');
    JQAudio.next = ctx.currentTime + JQAudio.restart;
  }

  source.start(JQAudio.next);
  JQAudio.next += frames / ctx.sampleRate;
},

JacquardAudioClose: function () {
  if (!JQAudio.ctx) return;

  JQAudio.events.forEach(function (name) {
    document.removeEventListener(name, JQAudio.unlock, true);
  });

  JQAudio.ctx.close();
  JQAudio.ctx = null;
}

};

autoAddDeps(JacquardAudioOut, '$JQAudio');
mergeInto(LibraryManager.library, JacquardAudioOut);
