using Newtonsoft.Json;
using SDG.Framework.Devkit;
using SDG.Unturned;
using System;
using System.IO;
using System.Text;
using System.Threading;
using Action = System.Action;

namespace DanielWillett.LevelObjectIcons.Configuration;

public class JsonConfigurationFile<TConfig> where TConfig : class, new()
{
    /// <summary>
    /// Any edits done during this event will be written.
    /// </summary>
    public event Action? OnRead;
    [JsonIgnore]
    public virtual TConfig? Default => null;
    [JsonIgnore]
    public TConfig Configuration
    {
        get => _config;
        set
        {
            lock (_sync)
            {
                TConfig old = Interlocked.Exchange(ref _config, value);
                if (old is IDisposable d)
                    d.Dispose();
            }
        }
    }

    [JsonIgnore]
    private TConfig _config = null!;
    [JsonIgnore]
    private readonly object _sync = new object();
    [JsonIgnore]
    private string _file = null!;
    [JsonIgnore]
    public JsonSerializerSettings? SerializerOptions { get; set; }
    [JsonIgnore]
    public string File
    {
        get => _file;
        set
        {
            lock (_sync)
            {
                _file = value;
            }
        }
    }
    /// <summary>
    /// Tells reloading to not make backups, copies, or save on read.
    /// </summary>
    public bool ReadOnlyReloading { get; set; }
    public JsonConfigurationFile(string file)
    {
        File = file;
    }
    protected virtual void OnReload() { }
    public void ReloadConfig()
    {
        lock (_sync)
        {
            TConfig old = ReadFromFile(File, Default, ReadOnlyReloading);
            old = Interlocked.Exchange(ref _config, old);
            if (old is IDisposable d)
                d.Dispose();
            OnRead?.Invoke();
            try
            {
                OnReload();
            }
            catch (Exception ex)
            {
                CommandWindow.LogError($"Exception in {nameof(OnReload)} after reading {typeof(TConfig).Name} config at {File}.");
                CommandWindow.LogError(ex);
            }
            if (!ReadOnlyReloading)
                WriteToFile(File, _config);
        }
    }
    public void SaveConfig()
    {
        lock (_sync)
        {
            WriteToFile(File, _config);
        }
    }
    private void WriteToFile(string path, TConfig config)
    {
        try
        {
            if (Path.GetDirectoryName(path) is { } dir)
                Directory.CreateDirectory(dir);
            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            if (config == null)
            {
                config = new TConfig();
                if (config is IDefaultable def)
                    def.SetDefaults();
                if (config is IDirtyable dirty)
                    dirty.isDirty = false;
            }

            using TextWriter tw = new StreamWriter(fs, Encoding.UTF8, 8192, leaveOpen: true);
            using JsonWriter writer = new JsonTextWriter(tw) { CloseOutput = false };
            JsonSerializer serializer = SerializerOptions == null ? JsonSerializer.CreateDefault() : JsonSerializer.Create(SerializerOptions);
            serializer.Serialize(writer, config, typeof(TConfig));
            writer.Flush();
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("[JSON CONFIG | " + typeof(TConfig).Name + "] Error writing config file: \"" + path + "\".");
            CommandWindow.LogError(ex);
        }
    }
    
    private TConfig ReadFromFile(string path, TConfig? @default = null, bool readOnly = false)
    {
        TConfig config;
        try
        {
            if (System.IO.File.Exists(path))
            {
                using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                long len = fs.Length;

                if (len > 0)
                {
                    using TextReader tr = new StreamReader(fs, Encoding.UTF8, false, 8192, true);
                    using JsonTextReader reader = new JsonTextReader(tr) { CloseInput = false };
                    JsonSerializer serializer = SerializerOptions == null ? JsonSerializer.CreateDefault() : JsonSerializer.Create(SerializerOptions);

                    config = serializer.Deserialize<TConfig>(reader) ?? throw new JsonException("Failed to read " + typeof(TConfig).Name + ": returned null.");

                    if (config is IDirtyable dirty2)
                        dirty2.isDirty = false;

                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("[JSON CONFIG | " + typeof(TConfig).Name + "] Error reading config file: \"" + path + "\".");
            CommandWindow.LogError(ex);

            string oldpath = path;
            if (!readOnly)
            {
                try
                {
                    int c = 0;
                    do
                    {
                        ++c;
                        path = Path.Combine(Path.GetDirectoryName(oldpath)!, Path.GetFileNameWithoutExtension(oldpath) + "_backup_" + c + Path.GetExtension(oldpath));
                    }
                    while (System.IO.File.Exists(path));
                    System.IO.File.Move(oldpath, path);
                }
                catch (Exception ex2)
                {
                    CommandWindow.LogError("[JSON CONFIG | " + typeof(TConfig).Name + "] Error backing up invalid config file from: \"" + oldpath + "\" to \"" + path + "\".");
                    CommandWindow.LogError(ex2);
                }
            }
        }

        if (@default != null)
            return @default;

        config = new TConfig();
        if (config is IDefaultable def)
            def.SetDefaults();
        if (config is IDirtyable dirty)
            dirty.isDirty = false;
        return config;
    }
}

