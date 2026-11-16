using System;

namespace LibriaMods.Unity.Shared
{
    /// <summary>
    /// A LibriaMods mod. Public, non-abstract classes implementing this (with a parameterless constructor)
    /// are found and instantiated by the LibriaMods loader, which also hot-reloads them when their DLL changes.
    /// Lifecycle: OnLoad → OnUpdate every frame → OnUnload (before a reload or when the game quits).
    /// OnUnload must undo everything the mod changed (Harmony patches, effects, objects), because a reload
    /// creates a fresh instance from a new copy of the assembly.
    /// </summary>
    public interface ILibriaMod
    {
        void OnLoad(IModContext context);
        void OnUpdate();
        void OnUnload();
    }

    /// <summary>Convenience base class: override only what you need.</summary>
    public abstract class LibriaMod : ILibriaMod
    {
        protected IModContext Context { get; private set; }

        public virtual void OnLoad(IModContext context) => Context = context;
        public virtual void OnUpdate() { }
        public virtual void OnUnload() { }

        protected void Log(string message) => Context?.Log(message);
    }

    /// <summary>Services the loader gives each mod. Implemented by the loader, so mods stay loader-agnostic.</summary>
    public interface IModContext
    {
        /// <summary>Stable id (from <see cref="LibriaModAttribute"/> or the type name), used for config/data names.</summary>
        string Id { get; }
        string Name { get; }

        /// <summary>Per-mod data folder: &lt;game&gt;\UserData\LibriaMods\&lt;Id&gt; (created on first use).</summary>
        string DataFolder { get; }

        void Log(string message);
        void Warning(string message);
        void Error(string message);

        /// <summary>A setting in the mod's own file, UserData\LibriaMods\&lt;Id&gt;.cfg. Survives hot reloads.</summary>
        IConfigEntry<T> Config<T>(string key, T defaultValue, string description = null);
        void SaveConfig();
    }

    public interface IConfigEntry<T>
    {
        T Value { get; set; }
    }

    /// <summary>Optional mod metadata.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class LibriaModAttribute : Attribute
    {
        public string Name { get; }
        public string Version { get; }
        public string Id { get; set; }
        public LibriaModAttribute(string name, string version = "1.0.0") { Name = name; Version = version; }
    }

    /// <summary>Restrict a mod to a game (Unity company/product name, as in the game's app.info).
    /// Repeat for several games. No attribute = any game.</summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class LibriaGameAttribute : Attribute
    {
        public string Developer { get; }
        public string Game { get; }
        public LibriaGameAttribute(string developer, string game) { Developer = developer; Game = game; }
    }
}
