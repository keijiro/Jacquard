using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Jacquard.App {

// File saving and loading.
//
// The format itself is engine-free; what is here is the part that genuinely needs a
// platform: where the files go, and what to say when one cannot be read.

public sealed class ProjectStore
{
    public string Name { get; set; } = "sketch";

    public string Directory
      => Path.Combine(Application.persistentDataPath, "Scores");

    public string PathOf(string name)
      => Path.Combine(Directory, name + ProjectFormat.Extension);

    public string Save(Project project)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(PathOf(Name), ProjectFormat.Write(project));
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

    // The names Save and Load can be pointed at: whatever is already on disk, plus
    // a few empty slots to start from. Picking from a list rather than typing keeps
    // the chrome to widgets this project draws itself.
    public List<string> Slots()
    {
        var names = new List<string> { "sketch", "take-1", "take-2", "take-3" };

        if (System.IO.Directory.Exists(Directory))
            foreach (var path in System.IO.Directory.GetFiles(Directory,
                                                              "*" + ProjectFormat.Extension))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!names.Contains(name)) names.Add(name);
            }

        return names;
    }

    // What is on disk, for the status line.
    public string Listing()
    {
        if (!System.IO.Directory.Exists(Directory)) return "no saved scores";

        var text = new StringBuilder("saved: ");
        var first = true;

        foreach (var path in System.IO.Directory.GetFiles(Directory,
                                                          "*" + ProjectFormat.Extension))
        {
            if (!first) text.Append(", ");
            text.Append(Path.GetFileNameWithoutExtension(path));
            first = false;
        }

        return first ? "no saved scores" : text.ToString();
    }
}

} // namespace Jacquard.App
