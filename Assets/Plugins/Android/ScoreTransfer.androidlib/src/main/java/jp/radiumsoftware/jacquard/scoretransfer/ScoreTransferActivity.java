package jp.radiumsoftware.jacquard.scoretransfer;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;

/**
 * One score out to a place the user picks, or one score in from a file they pick.
 *
 * This exists because Android is the one platform this app cannot hand its score
 * folder to. Since API 30 the app's own directory under Android/data — which is what
 * Application.persistentDataPath is — is hidden from the Files app, from the document
 * picker and from MTP, and hidden even from a file manager holding
 * MANAGE_EXTERNAL_STORAGE. There is no folder to show and none to drop a file into. So
 * what stands in for it is one file at a time, through the picker the system does show.
 *
 * It is a transparent activity of its own rather than anything hung off the player's,
 * and that is the whole design. An activity result needs an activity to arrive at;
 * reaching Unity's own means either knowing which class it is — GameActivity here, and
 * a project setting away from not being — or a Fragment attached to it at runtime.
 * A proxy needs neither, and it does not care what the player's activity is.
 *
 * The two sides meet through the statics at the foot of this file, and C# reads them by
 * polling once a frame while a transfer is out. onActivityResult runs on Android's UI
 * thread and Unity's scripting runs on its own, so something has to give the two a
 * happens-before edge: it is the monitor on this class, and confining it to a handful
 * of small synchronized methods is the reason polling was chosen over a callback into
 * C#.
 */
public final class ScoreTransferActivity extends Activity
{
    // The states, whose numbers are the C# Transfer enum's and have to stay in step
    // with it. WAITING is also what a cleared slate reads as, so nothing has to
    // distinguish "nothing has happened yet" from "nothing has happened since".
    public static final int WAITING = 0;
    public static final int EXPORTED = 1;
    public static final int IMPORTED = 2;
    public static final int CANCELLED = 3;
    public static final int FAILED = 4;

    /**
     * Starts a transfer: builds the intent for this activity and launches it.
     *
     * The intent is built here rather than in C# so that the whole crossing is one
     * CallStatic. Naming this class from C# would otherwise mean marshalling a
     * java.lang.Class through JNI to hand Intent's constructor, which is three calls
     * and a spelling of the class name in a second place.
     *
     * The state is set before the launch and not after it. What throws here is this
     * activity not being reachable — a manifest merge that did not take — and that
     * failure has to be the thing the first poll finds rather than a wait with no end.
     *
     * The score rides as an intent extra. The largest score in this repository is
     * about 3.3 KB and a Binder transaction gives up somewhere around 500 KB, so the
     * margin is a hundredfold and more; there is nothing to defend against and this
     * sentence is the defence.
     */
    public static synchronized void begin(Activity host, boolean export,
                                          String name, String text)
    {
        _state = WAITING;
        _name = null;
        _text = null;
        _problem = null;

        try
        {
            Intent intent = new Intent(host, ScoreTransferActivity.class);
            intent.putExtra(EXTRA_EXPORT, export);
            intent.putExtra(EXTRA_NAME, name);
            intent.putExtra(EXTRA_TEXT, text);
            host.startActivity(intent);
        }
        catch (ActivityNotFoundException error)
        {
            // Which means this activity is not in the merged manifest, and nothing
            // else in the build would have said so.
            fail("the file picker could not be opened: " + describe(error));
        }
        catch (Exception error)
        {
            fail(describe(error));
        }
    }

    /** The state, and the only call C# makes while a transfer is out. */
    public static synchronized int poll() { return _state; }

    /** The name the provider settled on, which is not always the one that was asked for. */
    public static synchronized String name() { return _name; }

    /** What was read, on an import that landed. */
    public static synchronized String text() { return _text; }

    /** The sentence to show, on a transfer that failed. */
    public static synchronized String problem() { return _problem; }

    /** Back to WAITING, once C# has taken the outcome. */
    public static synchronized void clear()
    {
        _state = WAITING;
        _name = null;
        _text = null;
        _problem = null;
    }

    // Activity implementation

    @Override
    protected void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);

        // The load-bearing line. A saved bundle means this activity has been here
        // before and the picker is already up — or was up when the process died under
        // it — so launching one again would put a second picker on the stack and leave
        // the first with nowhere to deliver to. Nothing a recreated instance needs is
        // kept in an instance field for that reason either: onActivityResult reads the
        // mode and the payload back out of getIntent(), which survives both a
        // recreation and a process death.
        //
        // Rotation does not come through here at all — configChanges in the manifest
        // covers it — but this is what would hold if a configuration were ever added
        // that the manifest does not list.
        if (savedInstanceState != null) return;

        Intent from = getIntent();
        boolean export = from != null && from.getBooleanExtra(EXTRA_EXPORT, false);
        String name = from == null ? null : from.getStringExtra(EXTRA_NAME);

        try
        {
            Intent pick = export ? new Intent(Intent.ACTION_CREATE_DOCUMENT)
                                 : new Intent(Intent.ACTION_OPEN_DOCUMENT);

            pick.addCategory(Intent.CATEGORY_OPENABLE);

            // text/plain going out, so the file lands somewhere a phone can open it;
            // anything at all coming in, because the judge of whether a file is a
            // score is the format reader and not a MIME type. It fails loudly on the
            // first keyword it does not know, with the line it found it on, which is a
            // better answer than a picker that would not show the file.
            pick.setType(export ? "text/plain" : "*/*");

            if (export && name != null) pick.putExtra(Intent.EXTRA_TITLE, name);

            startActivityForResult(pick, PICK);
        }
        catch (ActivityNotFoundException error)
        {
            deliver(FAILED, null, null, "this device has no file picker");
            finish();
        }
        catch (Exception error)
        {
            deliver(FAILED, null, null, describe(error));
            finish();
        }
    }

    @Override
    protected void onActivityResult(int request, int result, Intent data)
    {
        super.onActivityResult(request, result, data);

        // Plain onActivityResult rather than registerForActivityResult, which is the
        // modern spelling and would pull AndroidX into a library that otherwise needs
        // nothing at all.
        if (request != PICK) return;

        Uri uri = data == null ? null : data.getData();

        if (result != RESULT_OK || uri == null)
        {
            // Backing out of a picker is a decision and not a failure, and the C# side
            // says nothing about it.
            deliver(CANCELLED, null, null, null);
            finish();
            return;
        }

        Intent from = getIntent();
        boolean export = from != null && from.getBooleanExtra(EXTRA_EXPORT, false);

        if (export) write(uri, from == null ? null : from.getStringExtra(EXTRA_TEXT));
        else read(uri);

        // finish() and never finishAndRemoveTask(): this activity is in the player's
        // task, so removing the task would take Unity's activity down with it.
        finish();
    }

    @Override
    protected void onDestroy()
    {
        super.onDestroy();

        // A transfer taken away rather than answered, which is the one hole the state
        // at the foot of this file cannot climb out of on its own. begin() sets
        // WAITING and nothing but a result moves it, so an activity that goes away
        // without delivering one leaves WAITING standing for good — and the C# side,
        // which drops a press while a transfer is out, is then two dead buttons for
        // the rest of the run.
        //
        // The path there is easier to walk than it looks. Leave the app with the
        // picker up and come back to it through the launcher: the player is
        // singleTask, so the return clears everything above it in the task, the picker
        // and this activity with it, and no result is on its way anywhere. Death of
        // the process under the picker is the same hole seen from outside, and that
        // one is harmless only because the statics die with it.
        //
        // Only while finishing, because a destroy that is not a finish is one this
        // activity comes back from — onCreate reads the saved bundle and waits — and
        // the result still has somewhere to arrive. configChanges covers every change
        // the player itself declares, so what is left really is the transfer being
        // gone, and cancelled is what an abandoned transfer is.
        //
        // The flag and not the state is what says whether a result landed. C# takes an
        // outcome in four calls and clears it in a fifth, and a line here that read
        // the state instead would sometimes fire in the middle of that and null out a
        // name and a text the caller is halfway through reading.
        if (isFinishing() && !_delivered) settle(CANCELLED, null, null, null);
    }

    // Private members

    private static final int PICK = 1;

    private static final String EXTRA_EXPORT =
      "jp.radiumsoftware.jacquard.scoretransfer.EXPORT";
    private static final String EXTRA_NAME =
      "jp.radiumsoftware.jacquard.scoretransfer.NAME";
    private static final String EXTRA_TEXT =
      "jp.radiumsoftware.jacquard.scoretransfer.TEXT";

    // A megabyte, which is three hundred times the largest score here and small enough
    // that the thing it is really for — somebody picking a photo or a video — fails on
    // the second chunk instead of becoming a multi-megabyte String on the UI thread.
    private static final int CAP = 1024 * 1024;

    private static final int CHUNK = 8 * 1024;

    private static int _state = WAITING;
    private static String _name;
    private static String _text;
    private static String _problem;

    // Whether this instance has settled anything, read by onDestroy above. An
    // instance field and not a static, and it does not need to survive anything: a
    // recreation is a transfer still in flight, and a process death takes the state
    // it guards with it.
    private boolean _delivered;

    // Everything this activity settles goes through here, which is what gives
    // onDestroy something to ask.
    private void deliver(int state, String name, String text, String problem)
    {
        _delivered = true;
        settle(state, name, text, problem);
    }

    private void write(Uri uri, String text)
    {
        String name = displayName(uri);

        try
        {
            // "wt" and not "w". The t is the truncate, and without it a shorter score
            // written over a longer one leaves the tail of the longer one behind — a
            // file that reads as a score and then carries the end of another.
            OutputStream stream = getContentResolver().openOutputStream(uri, "wt");

            if (stream == null) throw new java.io.IOException("nothing to write to");

            try
            {
                stream.write(text == null ? new byte[0]
                                          : text.getBytes(StandardCharsets.UTF_8));
                stream.flush();
            }
            finally
            {
                stream.close();
            }

            deliver(EXPORTED, name, null, null);
        }
        catch (Exception error)
        {
            deliver(FAILED, name, null, "could not write " + name + ": " +
                                        describe(error));
        }
    }

    private void read(Uri uri)
    {
        String name = displayName(uri);

        try
        {
            InputStream stream = getContentResolver().openInputStream(uri);

            if (stream == null) throw new java.io.IOException("nothing to read from");

            ByteArrayOutputStream all = new ByteArrayOutputStream();

            try
            {
                byte[] buffer = new byte[CHUNK];

                // != -1 and not > 0. A stream is allowed to hand back nothing from a
                // read that is not at the end, and a loop that stopped on that would
                // take the first part of a score and call it the whole file.
                for (int read; (read = stream.read(buffer)) != -1;)
                {
                    all.write(buffer, 0, read);

                    if (all.size() > CAP)
                    {
                        deliver(FAILED, name, null,
                                "that file is too big to be a score");
                        return;
                    }
                }
            }
            finally
            {
                stream.close();
            }

            deliver(IMPORTED, name, new String(all.toByteArray(),
                                               StandardCharsets.UTF_8), null);
        }
        catch (Exception error)
        {
            deliver(FAILED, name, null, "could not read " + name + ": " +
                                        describe(error));
        }
    }

    /**
     * What the provider actually called the file, asked on both sides rather than only
     * on the way in. A picker is free to make a name its own — appending .txt to a
     * text/plain document is what the platform's own does — so the name to report is
     * the one that came back and not the one that was offered.
     */
    private String displayName(Uri uri)
    {
        try
        {
            Cursor cursor = getContentResolver().query(
              uri, new String[] { OpenableColumns.DISPLAY_NAME }, null, null, null);

            if (cursor == null) return uri.getLastPathSegment();

            try
            {
                if (cursor.moveToFirst() && !cursor.isNull(0)) return cursor.getString(0);
            }
            finally
            {
                cursor.close();
            }
        }
        catch (Exception error)
        {
            // A name is not worth failing a transfer that otherwise worked.
        }

        return uri.getLastPathSegment();
    }

    // The message an exception carries, or its class where it carries none —
    // getMessage() is null often enough that a sentence ending in "null" is the
    // likelier outcome of trusting it.
    private static String describe(Throwable error)
    {
        String message = error.getMessage();
        return message == null || message.length() == 0
               ? error.getClass().getSimpleName() : message;
    }

    private static synchronized void settle(int state, String name, String text,
                                            String problem)
    {
        _state = state;
        _name = name;
        _text = text;
        _problem = problem;
    }

    private static synchronized void fail(String problem)
    {
        _state = FAILED;
        _name = null;
        _text = null;
        _problem = problem;
    }
}
