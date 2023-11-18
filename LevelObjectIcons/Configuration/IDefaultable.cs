namespace DanielWillett.LevelObjectIcons.Configuration;
/// <summary>
/// Interface for setting an object's defaults without a constructor.
/// </summary>
public interface IDefaultable
{
    /// <summary>
    /// Set all the default values for an object.
    /// </summary>
    void SetDefaults();
}
