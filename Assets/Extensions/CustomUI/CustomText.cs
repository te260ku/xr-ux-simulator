using UnityEngine;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Text))]  
public class CustomText : MonoBehaviour  
{
    private TMP_Text _text;
    private JapaneseTextFormatter _formatter;
    private Color _color;    

    public void Initialize(JapaneseTextFormatter formatter)
    {
        _text = GetComponent<TMP_Text>();
        _formatter = formatter;
        _text.color = _color;
    }  


    public void SetText(string msg)  
    {
        _text.text = msg;  
    }

    public void SetFormattedText(string msg)  
    {
        if (_formatter == null)
        {
            Debug.LogError(
                $"{nameof(CustomText)} is not initialized.",
                this);

            return;
        }

        _text.text = _formatter.Format(msg);
    }

    public void ClearText()
    {
        _text.text = string.Empty;  
    }
}
