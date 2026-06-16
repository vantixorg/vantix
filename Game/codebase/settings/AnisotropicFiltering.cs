namespace Vantix.Config;

/// <summary>Anisotropic texture-filter level. Maps 1:1 to Godot's anisotropic_filtering_level (0..4).
/// Baked into samplers at startup, so changes need a restart.</summary>
public enum AnisotropicFiltering { Off, X2, X4, X8, X16 }
