using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class ApplicationInitializer : MonoBehaviour
{
    [SerializeField]
    private List<CustomText> texts;
    [SerializeField]
    private string dictionaryPath;

    private JapaneseTextFormatter formatter;
    [SerializeField] private string text;

    private void Awake()
    {
        string dicPath = Path.Combine(
        Application.streamingAssetsPath,
        dictionaryPath);
        formatter = new JapaneseTextFormatter(dicPath);

        foreach (var text in texts)
        {
            text.Initialize(formatter);
        }
    }

    private void OnDestroy()
    {
        formatter.Dispose();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.U))
        {
            foreach (var t in texts)
            {
                t.SetFormattedText(text);
            }
        }
    }
}