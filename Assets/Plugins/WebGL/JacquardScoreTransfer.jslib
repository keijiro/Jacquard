// The browser half of Jacquard's Export and Import. Its caller is
// Assets/Jacquard/App/ScoreTransfer.cs, whose Web arm this file mirrors and which
// argues why the pair exists at all. The shape — one state object, a poll once a frame
// while a transfer is out, and five little readers to take the outcome with — is the
// Android side's, and it is the same shape here so that the two arms of that file read
// as one thing done twice.
//
// Two findings about the browser decided everything below and neither is a taste. Both
// are readings off real builds of this project in Chrome 152 and Safari 26.6.2 on
// macOS. iOS Safari is untested — no device — and its download prompt is likely to
// differ.
//
// **Import has to call showPicker() and not click().** Unity does not run a C# button
// handler inside the DOM event that pressed it: the press is recorded and the handler
// runs later, in the requestAnimationFrame task that drives the player. That is a
// different task, and it has lost WebKit's UserGestureIndicator — click() on a file
// input then opens nothing at all in Safari and says nothing about it either, no throw
// and no console line. showPicker() is gated on *transient activation* instead, which
// survives the hop with room to spare: true one frame after the press, three frames
// after it, sixty frames after it and two seconds after it, and false at six seconds,
// where it throws NotAllowedError. It also consumes the activation, so there is one
// picker per press whatever this file does.
//
// **Export is not gated on anything.** A Blob, an object URL and a synthetic
// <a download> click downloaded byte-identical text in both browsers even six seconds
// after the press, with navigator.userActivation.isActive false. What Safari gates
// instead is a site's first download, and it does it where the page cannot see:
// a.click() returns normally while the download sits as *Waiting for permission*
// behind a modal, and nothing programmatic can observe the prompt, the wait or the
// answer. So the app reports what it can honestly know — that the file was handed over
// — and the manual carries the prompt, since the reader is the only one who can see
// it. Two a.click() calls in one tick are worse than useless there, Safari silently
// losing the first, and window.open on an object URL returns null, so neither is a
// fallback. One export per press, which is what the C# side's _busy already says.

var JacquardScoreTransfer = {

// The whole of the state, and nothing is kept on the C# side except whether a transfer
// is out — which is the Android arm's arrangement and is there for the same reason: a
// frame with nothing out then costs no interop call at all.
//
// Declared as this object's dependencies rather than each function's, since autoAddDeps
// hangs $JQTransfer off all of them and dependencies are followed through. The three are
// the string helpers both directions need and the free that answers for the buffers this
// object allocates.
$JQTransfer__deps: ['$UTF8ToString', '$stringToNewUTF8', 'free'],
$JQTransfer: {
  // The states, whose numbers are the C# Transfer enum's and have to stay in step with
  // it. WAITING is also what a cleared slate reads as, so nothing has to distinguish
  // "nothing has happened yet" from "nothing has happened since".
  WAITING: 0,
  EXPORTED: 1,
  IMPORTED: 2,
  CANCELLED: 3,
  FAILED: 4,

  // A megabyte, which is three hundred times the largest score in this repository and
  // the same figure the Android side defends. Here it is read off file.size before a
  // single byte is touched, so somebody who picked a photo is turned away without a
  // read at all and there is no chunk loop to carry the cap.
  CAP: 1024 * 1024,

  state: 0,  // WAITING

  // The outcome, held as three UTF-8 buffers on the heap because that is the cheap
  // direction to hand a string back over: see the note on the crossing in
  // ScoreTransfer.cs. Zero is a string that is not there, which is what
  // Marshal.PtrToStringUTF8 reads as null.
  name: 0,
  text: 0,
  problem: 0,

  // Which press the outcome slot belongs to. An import that has been superseded still
  // has a picker up, and its change or cancel arrives with this number as it was when
  // that picker was opened; anything that does not match the current one is a picker
  // nobody is waiting on any more and it goes nowhere. See JacquardTransferImport for
  // why the Web supersedes where Android drops.
  gen: 0,

  // The file input the live picker belongs to, kept only so that the one before it can
  // be taken out of the document when it is superseded.
  input: null,

  // Back to a clean slate, and the three buffers back to the heap they came from. The
  // slot is owned here and never handed over, which is what makes the return trip one
  // crossing with nothing to leak.
  blank: function () {
    if (JQTransfer.name) _free(JQTransfer.name);
    if (JQTransfer.text) _free(JQTransfer.text);
    if (JQTransfer.problem) _free(JQTransfer.problem);
    JQTransfer.name = JQTransfer.text = JQTransfer.problem = 0;
    JQTransfer.state = JQTransfer.WAITING;
  },

  // Everything that ends a transfer goes through here, and the order is load-bearing:
  // the state goes in after the strings it speaks for. An import is asynchronous twice
  // over — the picker, and then the read — and the state stays WAITING across both, so
  // a poll landing anywhere in there comes back next frame rather than reading a slot
  // that is half filled in. Which is also why the enum needs no member for the read:
  // C# cannot tell the two legs apart and has no reason to.
  settle: function (state, name, text, problem) {
    JQTransfer.blank();
    if (name != null) JQTransfer.name = stringToNewUTF8(name);
    if (text != null) JQTransfer.text = stringToNewUTF8(text);
    if (problem != null) JQTransfer.problem = stringToNewUTF8(problem);
    JQTransfer.state = state;
  },

  // The message an error carries, or its name where it carries none — the same
  // judgement the Java side makes, and for the same reason: a sentence ending in
  // "undefined" is the likelier outcome of trusting the message alone.
  describe: function (error) {
    if (!error) return 'something went wrong';
    return error.message || error.name || String(error);
  }
},

// The whole export, and all of it synchronous. The state is set here rather than
// reported from here, so that the outcome arrives on the C# side the way an Android
// one does: the press happens inside the player's own Update, and the next frame's
// JacquardApp.FollowTheTransfer picks the slot up.
JacquardTransferExport: function (name, text) {
  var file = UTF8ToString(name);

  try {
    var url = URL.createObjectURL(new Blob([UTF8ToString(text)],
                                           { type: 'text/plain' }));

    var link = document.createElement('a');
    link.href = url;

    // The name as given, extension and all. A download attribute without an extension
    // gets one appended from the blob's type — .txt, which is exactly what a score
    // must not be called — while score1.jacquard is passed through untouched by both
    // browsers. Which is the point of exporting under the score folder's own
    // extension: ProjectStore enumerates *.jacquard and nothing else, so the file that
    // comes out of here can be dropped into a desktop's score folder as it stands.
    link.download = file;
    link.rel = 'noopener';

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Not in this tick. Revoking an object URL is taking away the file the download is
    // about to read, and Safari's permission prompt means the read can be a minute
    // after the click and behind a decision this page cannot see. A minute of one
    // score's worth of bytes is nothing to hold; a download that fetches a URL that
    // has been revoked is a file that silently does not arrive.
    setTimeout(function () { URL.revokeObjectURL(url); }, 60000);

    JQTransfer.settle(JQTransfer.EXPORTED, file, null, null);
  } catch (error) {
    JQTransfer.settle(JQTransfer.FAILED, file, null,
                      'could not write ' + file + ': ' + JQTransfer.describe(error));
  }
},

// One picker, opened now and answered whenever it is answered.
//
// **This supersedes where the Android side drops.** There a second press is dropped
// while a transfer is out, and it can afford to be: the picker is an activity that
// always returns, and onDestroy backstops even the case where it is taken away without
// returning. There is no backstop here. A browser that opened a picker and never fired
// cancel — the event is there in both of the two measured, but it is one event away
// from being absent — would leave a press outstanding for the rest of the run, and
// with a press dropped on top of it the pair of buttons would be dead with no way back
// but a page reload, which is an app restart nobody asked for. So this bumps the
// generation, blanks the slot and opens a picker unconditionally, and a superseded
// picker's late answer compares the number it captured and returns without touching
// anything. It costs nothing to allow, since showPicker() consumes the activation and
// there is one picker per press either way. The asymmetry that leaves — Export drops a
// second press, Import supersedes one — is what keeps the outcome slot single: Export
// can never hang, Import can never wedge.
JacquardTransferImport: function () {
  var mine = ++JQTransfer.gen;

  JQTransfer.blank();

  if (JQTransfer.input) JQTransfer.input.remove();

  var input = document.createElement('input');
  input.type = 'file';

  // No accept filter, which mirrors the Android side's */* and its argument: what
  // decides whether a file is a score is the format reader, and it fails loudly on the
  // first keyword it does not know, with the line it found it on. That is a better
  // answer than a picker that would not show the file at all.
  //
  // An element per press rather than one kept for the life of the page, so that the
  // generation can be captured in the two handlers below and die with them. Off the
  // side of the page rather than display:none: there is nothing to see either way, and
  // an element the browser lays out nowhere at all is the one shape a picker could
  // reasonably refuse to open on.
  input.style.position = 'fixed';
  input.style.left = '-9999px';
  input.style.opacity = '0';

  input.addEventListener('change', function () {
    if (mine !== JQTransfer.gen) return;

    var file = input.files && input.files[0];

    // A change with nothing chosen is not a thing either browser does, and it is a
    // press that would otherwise be outstanding for good. Cancelled is what a picker
    // that came back with no file is.
    if (!file) {
      JQTransfer.settle(JQTransfer.CANCELLED, null, null, null);
      return;
    }

    if (file.size > JQTransfer.CAP) {
      JQTransfer.settle(JQTransfer.FAILED, file.name, null,
                        'that file is too big to be a score');
      return;
    }

    // No byte order mark to strip, unlike the Android arm. Blob.text() decodes by the
    // Encoding Standard's UTF-8 decode, which removes a leading mark as part of
    // decoding, so a score written by a desktop editor arrives with `jacquard` as its
    // first word.
    file.text().then(function (text) {
      if (mine !== JQTransfer.gen) return;
      JQTransfer.settle(JQTransfer.IMPORTED, file.name, text, null);
    }, function (error) {
      if (mine !== JQTransfer.gen) return;
      JQTransfer.settle(JQTransfer.FAILED, file.name, null,
                        'could not read ' + file.name + ': ' +
                        JQTransfer.describe(error));
    });
  });

  // Backing out of a picker is a decision and not a failure, and the C# side says
  // nothing about it. Both measured browsers fire this when the picker is dismissed,
  // which is what makes _busy come back after a press that chose nothing.
  input.addEventListener('cancel', function () {
    if (mine !== JQTransfer.gen) return;
    JQTransfer.settle(JQTransfer.CANCELLED, null, null, null);
  });

  document.body.appendChild(input);
  JQTransfer.input = input;

  // Synchronously, and that is the rule the Android side states as well: a picker that
  // cannot be reached has to be a failure the first poll finds rather than a wait with
  // no end. What throws here is the activation having run out — six seconds and more
  // after the press, which no button on this app can arrange, but the throw is the one
  // thing showPicker() does say out loud and it costs a try to keep.
  try {
    input.showPicker();
  } catch (error) {
    JQTransfer.settle(JQTransfer.FAILED, null, null,
                      'the file picker could not be opened: ' +
                      JQTransfer.describe(error));
  }
},

// The state, and the only call C# makes while a transfer is out.
JacquardTransferPoll: function () { return JQTransfer.state; },

// What the file is called: the name it was exported under, or the name the picker
// handed over on the way in.
JacquardTransferName: function () { return JQTransfer.name; },

// What was read, on an import that landed.
JacquardTransferText: function () { return JQTransfer.text; },

// The sentence to show, on a transfer that failed.
JacquardTransferProblem: function () { return JQTransfer.problem; },

// Back to WAITING, once C# has taken the outcome — and the three buffers freed, which
// is the whole of the ownership question on the way back. C# reads the outcome in four
// calls and gives the slot back in this fifth one, so there is exactly one moment at
// which the heap can be let go of and this is it.
JacquardTransferClear: function () { JQTransfer.blank(); }

};

autoAddDeps(JacquardScoreTransfer, '$JQTransfer');
mergeInto(LibraryManager.library, JacquardScoreTransfer);
