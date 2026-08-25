using UnityEngine;

[CreateAssetMenu(
    fileName = "LightingConfig",
    menuName = "Config/Lighting")]
public sealed class LightingConfigAsset
    : ConfigAsset<LightingConfigData>
{
    public override string ConfigId
        => "lighting";
}