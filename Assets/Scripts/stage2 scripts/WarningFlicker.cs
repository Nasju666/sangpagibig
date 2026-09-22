using UnityEngine;
using TMPro;
using System.Collections;

public class WarningFlicker : MonoBehaviour
{
    [Header("Flicker")]
    public float flickerInterval = 0.15f;

    [Header("Colors")]
    public Color colorA = Color.red;
    public Color colorB = new Color(1f, 0.5f, 0f); // orange
    public float colorLerpSpeed = 5f;

    TextMeshProUGUI text;
    Coroutine flickerRoutine;

    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        flickerRoutine = StartCoroutine(Flicker());
    }

    void OnDisable()
    {
        if (flickerRoutine != null)
            StopCoroutine(flickerRoutine);

        text.enabled = true;
        text.color = colorA;
    }

    IEnumerator Flicker()
    {
        bool visible = true;
        float t = 0f;

        while (true)
        {
            // Flicker on/off
            visible = !visible;
            text.enabled = visible;

            // Color lerp
            t += Time.deltaTime * colorLerpSpeed;
            text.color = Color.Lerp(colorA, colorB, Mathf.PingPong(t, 1f));

            yield return new WaitForSeconds(flickerInterval);
        }
    }
}
