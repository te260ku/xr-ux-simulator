using UnityEngine;

public class LogTest : MonoBehaviour
{
    int count;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartCoroutine(cor());
    }

    System.Collections.IEnumerator cor()
    {
        Debug.Log("StartCoroutine");
        while (true)
        {
            yield return new WaitForSeconds(1f);
            count++;
            Debug.Log("Waited 1 second, count: " + count);
        }
    }
}
