using UnityEditor;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RepositoryIdGenerationConfig",
    menuName = "Tools/Repository ID Generation Config")]
public sealed class RepositoryIdGenerationConfig : ScriptableObject
{
    [SerializeField] private string repositoryFilePath = "";
    [SerializeField] private MonoScript idTypeScript;
    [SerializeField] private MonoScript targetClassScript;

    public string RepositoryFilePath => repositoryFilePath;
    public MonoScript IdTypeScript => idTypeScript;
    public MonoScript TargetClassScript => targetClassScript;
}
