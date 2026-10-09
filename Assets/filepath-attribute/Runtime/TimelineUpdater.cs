using System;
using UnityEngine;
using VContainer.Unity;

public sealed class TimelineUpdater : ITickable
{
    private readonly TimelinePlayer _player;

    public TimelineUpdater(TimelinePlayer player)
    {
        _player = player
            ?? throw new ArgumentNullException(nameof(player));
    }

    public void Tick()
    {
        _player.Update(Time.deltaTime);
    }
}
