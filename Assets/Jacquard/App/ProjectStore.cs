using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Jacquard.App {

// File saving and loading.
//
// The format itself is engine-free; what is here is the part that genuinely needs a
// platform: where the files go, which of them the app comes up in, and what to say
// when one cannot be read.
//
// Every score this app has is a file in one folder, and the chooser is that folder
// read out. There are no names of its own in here any more: it used to offer "sketch"
// and three takes as slots to save into, which meant the list held names that were not
// files and a hand had to know which was which. What it offers now is a folder that is
// never empty — an install that has saved nothing gets nine scores written for it — so
// every name on the chooser is a score, and picking one and pressing Load always does
// something.

public sealed class ProjectStore
{
    // Which file Save and Load are pointed at, which is whatever the chooser is
    // showing. It is a name and not a path, since the folder is not a choice.
    public string Name { get; set; } = SlotName(1);

    public string Directory
      => Path.Combine(Application.persistentDataPath, "Scores");

    public string PathOf(string name)
      => Path.Combine(Directory, name + ProjectFormat.Extension);

    public string Save(Project project)
    {
        try
        {
            Write(Name, project);
            Remember();
            return "saved " + Name;
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            return "could not save: " + error.Message;
        }
    }

    public Project Load(out string message)
    {
        var path = PathOf(Name);

        if (!File.Exists(path))
        {
            message = "no file called " + Name;
            return null;
        }

        try
        {
            var project = ProjectFormat.Read(File.ReadAllText(path));
            Remember();
            message = "loaded " + Name;
            return project;
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            message = "could not read " + Name + ": " + error.Message;
            return null;
        }
    }

    // What the chooser holds: the folder, in alphabetical order.
    //
    // Sorted here rather than taken as the directory hands it over, which is whatever
    // order the filesystem happens to keep and so is a different chooser on a different
    // machine. Case-insensitively, because the order is being read by a person and a
    // person does not read "Take" as coming before "sketch".
    public List<string> Slots()
    {
        var names = new List<string>();

        if (!System.IO.Directory.Exists(Directory)) return names;

        foreach (var path in System.IO.Directory.GetFiles(Directory,
                                                          "*" + ProjectFormat.Extension))
            names.Add(Path.GetFileNameWithoutExtension(path));

        names.Sort(System.StringComparer.OrdinalIgnoreCase);
        return names;
    }

    // Writes the nine scores a fresh install starts with, and does nothing at all if
    // there is so much as one file there already.
    //
    // Nine because that is what the chooser can be walked around in a moment, and
    // because a numbered rack of slots is the thing a hand can hold in its head: what
    // is being made is a piece at a time, and the folder is where the pieces are kept.
    // They are written rather than offered as empty names for the reason the class note
    // gives — a name on that list is a score or it is a trap — and writing them costs
    // nine small files once.
    //
    // The first holds the sample, which is the one score in here that was made rather
    // than generated: an install that has never been opened comes up in a real piece of
    // work rather than in four notes, and the four notes are in the eight slots beside
    // it. A sample that cannot be produced is not worth stopping for, so that slot
    // falls back to the same initial score as the rest.
    //
    // The sample arrives as something to call rather than as a score, since the caller
    // has to read and parse an asset to make one and this is a folder that is nearly
    // always already filled.
    public bool Seed(System.Func<Project> sample)
    {
        if (Slots().Count > 0) return false;

        try
        {
            for (var slot = 1; slot <= SlotCount; slot++)
            {
                var project = slot == 1 ? sample() : null;
                Write(SlotName(slot), project ?? Project.CreateInitial());
            }

            return true;
        }
        catch (System.Exception error)
        {
            // The same road every other file failure here takes. What is left is a
            // folder holding however many of the nine were written, which the next
            // launch will not add to — Seed fills an empty folder and this one is no
            // longer empty — and which is still a chooser with scores on it.
            Debug.LogException(error);
            return false;
        }
    }

    // The score to come up in: the one the app was last left in, and the first on the
    // chooser when there is no such thing.
    //
    // Checked against the folder rather than trusted, so a score deleted from under the
    // app — or one written by a build that is no longer installed — comes up as the
    // first slot instead of as a failed load. On a fresh install nothing is remembered
    // and the folder has just been seeded, which is how the first launch of all comes
    // up in the sample: it is score1, and score1 is what sorts first.
    public string Opening()
    {
        var slots = Slots();
        var last = PlayerPrefs.GetString(LastKey, "");

        if (slots.Contains(last)) return last;

        return slots.Count > 0 ? slots[0] : SlotName(1);
    }

    // Private members

    // How many scores an install starts with, and the names they are given. One-based,
    // since they are read by whoever is choosing between them and not by anything here.
    const int SlotCount = 9;

    static string SlotName(int slot) => "score" + slot;

    // Which file the app was last in. It is a fact about this copy of the app and not
    // about any piece, so it lives where the rest of those do — see SystemPanel.
    const string LastKey = "Jacquard.Score";

    // Both ways a score is opened or closed record it: a load is the plain case, and a
    // save is the same thing said from the other end — the slot that has just been
    // written is the one holding the work, so it is the one to come back to.
    //
    // Written through rather than left for the quit, for the reason the visualizer's
    // setting is: a tablet app is put away and then killed off screen.
    void Remember()
    {
        PlayerPrefs.SetString(LastKey, Name);
        PlayerPrefs.Save();
    }

    // The write itself, which throws: what to say about a failure depends on what was
    // being done, so it is said by the caller.
    void Write(string name, Project project)
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(PathOf(name), ProjectFormat.Write(project));
    }
}

} // namespace Jacquard.App
