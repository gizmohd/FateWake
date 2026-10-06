namespace Fatewake.Web.Presentation;

/// <summary>Describes the default visibility of a carried visual item.</summary>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/CarryVisibility.md">CarryVisibility documentation</see>
public enum CarryVisibility
{
    /// <summary>Shown in all compatible scene contexts.</summary>
    AlwaysVisible,
    /// <summary>Shown unless the scene context excludes it.</summary>
    NormallyVisible,
    /// <summary>Hidden unless a scene explicitly reveals it.</summary>
    Concealed,
    /// <summary>Not currently equipped or carried visibly.</summary>
    Stored
}
