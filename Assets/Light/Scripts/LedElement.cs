using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LedElement : MonoBehaviour
{
    [SerializeField]
    [HideInInspector]
    private string _id;

    public string Id => _id;

    public Vector3 WorldPosition =>
        transform.position;


#if UNITY_EDITOR

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_id) || HasDuplicateId())
        {
            _id = Guid.NewGuid().ToString("N");

            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    private bool HasDuplicateId()
    {
        var leds = FindObjectsByType<LedElement>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var led in leds)
        {
            if (led == this)
            {
                continue;
            }

            if (led._id == _id)
            {
                return true;
            }
        }

        return false;
    }

#endif
}