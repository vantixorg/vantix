namespace Vantix.Config;

/// <summary>Volumetric fog quality; Low/Medium/High = 64/96/160 voxels/side (cubic cost). Don't use Off at
/// runtime — smoke grenades render into the same fog texture, so Off makes smokes invisible.</summary>
public enum VolumetricFogQuality { Off, Low, Medium, High }
