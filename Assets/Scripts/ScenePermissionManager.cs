using UnityEngine;
using UnityEngine.Android;

public class ScenePermissionManager : MonoBehaviour
{
    private const string ScenePermission = "com.oculus.permission.USE_SCENE";

    private void Start()
    {
        if (!Permission.HasUserAuthorizedPermission(ScenePermission))
        {
            Permission.RequestUserPermission(ScenePermission);
        }
    }
}