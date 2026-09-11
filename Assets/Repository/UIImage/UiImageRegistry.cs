using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class UiImageRegistry : MonoBehaviour
{
    [SerializeField]
    private UiImageBinding[] bindings;

    private readonly Dictionary<UiImageTarget, UiImageBinding> _bindings = new();

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        _bindings.Clear();

        foreach (var binding in bindings)
        {
            if (binding == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(UiImageRegistry)} contains a null binding.");
            }

            if (!_bindings.TryAdd(binding.Target, binding))
            {
                throw new InvalidOperationException(
                    $"Duplicate UiImageTarget: {binding.Target}");
            }
        }
    }

    public UiImageBinding Get(UiImageTarget target)
    {
        if (!_bindings.TryGetValue(target, out var binding))
        {
            throw new KeyNotFoundException(
                $"UiImageTarget is not registered: {target}");
        }

        return binding;
    }

    public bool TryGet(
        UiImageTarget target,
        out UiImageBinding binding)
    {
        return _bindings.TryGetValue(target, out binding);
    }

    public bool Contains(UiImageTarget target)
    {
        return _bindings.ContainsKey(target);
    }
}