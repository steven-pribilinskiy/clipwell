using System.Reflection;

namespace Clipwell.Protocol.Plugins;

/// <summary>
/// Loads plugin assemblies from a directory and instantiates every public, concrete
/// type implementing <typeparamref name="T"/> with a parameterless constructor. The
/// daemon loads <see cref="IClipDetector"/>s; the picker loads <see cref="IClipAction"/>s.
/// A bad plugin is skipped, never fatal.
/// </summary>
public static class PluginLoader
{
    /// <summary>
    /// Plugins live in <c>&lt;data dir&gt;/plugins</c> by default (data dir from
    /// CLIPWELL_DATA_DIR, else the OS app-data Clipwell folder). Override the whole
    /// path with CLIPWELL_PLUGINS_DIR.
    /// </summary>
    public static string DefaultDir
    {
        get
        {
            var explicitDir = Environment.GetEnvironmentVariable("CLIPWELL_PLUGINS_DIR");
            if (!string.IsNullOrEmpty(explicitDir)) return explicitDir;
            var dataDir = Environment.GetEnvironmentVariable("CLIPWELL_DATA_DIR")
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clipwell");
            return Path.Combine(dataDir, "plugins");
        }
    }

    public static IReadOnlyList<T> Load<T>(string? dir = null)
    {
        dir ??= DefaultDir;
        var found = new List<T>();
        if (!Directory.Exists(dir)) return found;

        foreach (var dll in Directory.GetFiles(dir, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                foreach (var type in asm.GetTypes())
                {
                    if (!typeof(T).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface) continue;
                    if (type.GetConstructor(Type.EmptyTypes) is null) continue;
                    if (Activator.CreateInstance(type) is T instance) found.Add(instance);
                }
            }
            catch
            {
                // Unloadable / incompatible plugin — skip it.
            }
        }
        return found;
    }
}
