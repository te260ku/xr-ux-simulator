using System.IO;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        var dictionaryPath = Path.Combine(
            Application.streamingAssetsPath,
            "NMeCab/ipadic");

        builder.Register<JapaneseTextFormatter>(Lifetime.Singleton)
            .WithParameter("dictionaryPath", dictionaryPath);
    }
}