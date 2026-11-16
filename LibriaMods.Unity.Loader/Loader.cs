using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LibriaMods.Unity.Shared;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(LibriaMods.Unity.Loader.Loader), "LibriaMods Unity Loader", "1.1.1", "Ita")]
[assembly: MelonGame(null, null)]

namespace LibriaMods.Unity.Loader
{
    /// <summary>
    /// The only MelonMod of LibriaMods. Loads every DLL in UserData\LibriaMods\Mods, instantiates the classes
    /// implementing <see cref="ILibriaMod"/>, and hot-reloads a DLL when it changes (or on the reload key).
    /// DLLs are loaded from memory, so they're never locked and can be rebuilt while the game runs.
    /// </summary>
    public class Loader : MelonMod
    {
        MelonPreferences_Entry<string> modsFolder;
        MelonPreferences_Entry<bool> autoReload;
        MelonPreferences_Entry<KeyCode> reloadKey;

        string folder;
        float nextScan;
        readonly Dictionary<string, LoadedDll> dlls = new Dictionary<string, LoadedDll>(StringComparer.OrdinalIgnoreCase);

        sealed class LoadedDll
        {
            public DateTime WriteTime;
            public readonly List<ModInstance> Mods = new List<ModInstance>();
        }

        sealed class ModInstance
        {
            public ILibriaMod Mod;
            public ModContext Context;
            public bool Faulted;
        }

        public override void OnInitializeMelon()
        {
            var cat = MelonPreferences.CreateCategory("LibriaModsUnityLoader", "LibriaMods Unity Loader");
            cat.SetFilePath(Path.Combine(MelonEnvironment.UserDataDirectory, "LibriaMods.Unity.Loader.cfg"));
            modsFolder = cat.CreateEntry("ModsFolder", "", description: "Folder with LibriaMods DLLs. Empty = UserData\\LibriaMods\\Mods");
            autoReload = cat.CreateEntry("AutoReload", false, description: "Reload a mod automatically when its DLL changes (for mod development)");
            reloadKey  = cat.CreateEntry("ReloadKey", KeyCode.F5, description: "Reload all LibriaMods mods");

            folder = string.IsNullOrEmpty(modsFolder.Value)
                ? Path.Combine(Path.Combine(MelonEnvironment.UserDataDirectory, "LibriaMods"), "Mods")
                : modsFolder.Value;
            Directory.CreateDirectory(folder);

            Fx.Log = s => LoggerInstance.Msg("[Fx] " + s);

            Scan(force: true);
            int n = dlls.Values.Sum(d => d.Mods.Count);
            LoggerInstance.Msg($"{n} mod(s) loaded from {folder}" + (autoReload.Value ? " (auto-reload on)" : ""));
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(reloadKey.Value)) ReloadAll();

            if (autoReload.Value && Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 1f;
                Scan(force: false);
            }

            foreach (var dll in dlls.Values)
                foreach (var m in dll.Mods)
                {
                    if (m.Faulted) continue;
                    try { m.Mod.OnUpdate(); }
                    catch (Exception e) { m.Faulted = true; m.Context.Error($"OnUpdate failed, mod disabled until reload: {e}"); }
                }

            Fx.Update();
        }

        public override void OnApplicationQuit()
        {
            foreach (var path in dlls.Keys.ToList()) Unload(path);
            Fx.Shutdown();
        }

        // ------------------------------------------------------------------ loading

        void ReloadAll()
        {
            foreach (var path in dlls.Keys.ToList()) Unload(path);
            Scan(force: true);
        }

        void Scan(bool force)
        {
            var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.dll") : new string[0];

            foreach (var gone in dlls.Keys.Where(p => !files.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList())
                Unload(gone);

            foreach (var file in files)
            {
                var wt = File.GetLastWriteTimeUtc(file);
                if (dlls.TryGetValue(file, out var loaded))
                {
                    if (!force && loaded.WriteTime == wt) continue;
                    if ((DateTime.UtcNow - wt).TotalSeconds < 0.5) continue; // still being written by the build
                    Unload(file);
                }
                Load(file, wt);
            }
        }

        void Load(string file, DateTime writeTime)
        {
            var entry = new LoadedDll { WriteTime = writeTime };
            dlls[file] = entry;
            try
            {
                var bytes = File.ReadAllBytes(file);
                var pdb = Path.ChangeExtension(file, ".pdb");
                var asm = File.Exists(pdb) ? Assembly.Load(bytes, File.ReadAllBytes(pdb)) : Assembly.Load(bytes);

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types.Where(IsModType))
                {
                    if (!ForThisGame(t)) continue;

                    var info = t.GetCustomAttributes(typeof(LibriaModAttribute), false).Cast<LibriaModAttribute>().FirstOrDefault();
                    string name = info?.Name ?? t.Name;
                    string id = info?.Id ?? t.FullName;
                    var ctx = new ModContext(id, name);

                    var inst = new ModInstance { Context = ctx };
                    try
                    {
                        inst.Mod = (ILibriaMod)Activator.CreateInstance(t);
                        inst.Mod.OnLoad(ctx);
                        entry.Mods.Add(inst);
                        LoggerInstance.Msg($"Loaded {name} {info?.Version} ({Path.GetFileName(file)}, built {writeTime.ToLocalTime():HH:mm:ss})");
                    }
                    catch (Exception e)
                    {
                        ctx.Error($"OnLoad failed: {(e is TargetInvocationException ti ? ti.InnerException : e)}");
                    }
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Error($"Can't load {Path.GetFileName(file)}: {e.Message}");
            }
        }

        void Unload(string file)
        {
            if (!dlls.TryGetValue(file, out var entry)) return;
            foreach (var m in entry.Mods)
            {
                try { m.Mod.OnUnload(); }
                catch (Exception e) { m.Context.Warning($"OnUnload failed: {e.Message}"); }
            }
            dlls.Remove(file);
        }

        static bool IsModType(Type t) =>
            t.IsClass && !t.IsAbstract && t.IsPublic &&
            typeof(ILibriaMod).IsAssignableFrom(t) &&
            t.GetConstructor(Type.EmptyTypes) != null;

        static bool ForThisGame(Type t)
        {
            var games = t.GetCustomAttributes(typeof(LibriaGameAttribute), false).Cast<LibriaGameAttribute>().ToList();
            return games.Count == 0 || games.Any(g =>
                string.Equals(g.Developer, Application.companyName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(g.Game, Application.productName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
