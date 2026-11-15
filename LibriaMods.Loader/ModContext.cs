using System.IO;
using System.Linq;
using LibriaMods.Shared;
using MelonLoader;
using MelonLoader.Utils;

namespace LibriaMods.Loader
{
    /// <summary>MelonLoader-backed implementation of <see cref="IModContext"/>.</summary>
    internal sealed class ModContext : IModContext
    {
        readonly MelonLogger.Instance logger;
        MelonPreferences_Category category;

        public string Id { get; }
        public string Name { get; }

        static string Root => Path.Combine(MelonEnvironment.UserDataDirectory, "LibriaMods");

        public string DataFolder
        {
            get
            {
                var dir = Path.Combine(Root, SafeId);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        string SafeId => new string(Id.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' ? c : '_').ToArray());

        public ModContext(string id, string name)
        {
            Id = id;
            Name = name;
            logger = new MelonLogger.Instance(name);
        }

        public void Log(string message) => logger.Msg(message);
        public void Warning(string message) => logger.Warning(message);
        public void Error(string message) => logger.Error(message);

        MelonPreferences_Category Category
        {
            get
            {
                if (category != null) return category;
                // Reuse the category on hot reload, so values and the file binding are kept.
                var catId = "LibriaMods_" + SafeId.Replace('.', '_');
                category = MelonPreferences.GetCategory(catId);
                if (category == null)
                {
                    Directory.CreateDirectory(Root);
                    category = MelonPreferences.CreateCategory(catId, Name);
                    category.SetFilePath(Path.Combine(Root, SafeId + ".cfg"));
                }
                return category;
            }
        }

        public IConfigEntry<T> Config<T>(string key, T defaultValue, string description = null)
        {
            var e = Category.GetEntry<T>(key) ?? Category.CreateEntry(key, defaultValue, description: description);
            return new Entry<T>(e);
        }

        public void SaveConfig() => Category.SaveToFile(false);

        sealed class Entry<T> : IConfigEntry<T>
        {
            readonly MelonPreferences_Entry<T> e;
            public Entry(MelonPreferences_Entry<T> e) { this.e = e; }
            public T Value { get => e.Value; set => e.Value = value; }
        }
    }
}
