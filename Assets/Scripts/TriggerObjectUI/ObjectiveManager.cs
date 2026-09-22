using UnityEngine;
using TMPro;
using System.Collections;

public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private TextMeshProUGUI objectiveText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private AudioClip showSfx;
    [SerializeField] private AudioSource audioSource;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 3f;
    [SerializeField] private float visibleDuration = 4f;
    [SerializeField] private bool glitchEffect = true;

    private Coroutine currentRoutine;

    private void Awake()
    {
        // Singleton pattern (simple)
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Optionally: DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        ShowObjective(" ");
    }

    public void ShowObjective(string message)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(DisplayObjective(message));
    }

    private IEnumerator DisplayObjective(string message)
    {
        // set text
        if (objectiveText != null)
            objectiveText.text = "" + message.ToUpper();

        // audio cue
        if (audioSource != null && showSfx != null) audioSource.PlayOneShot(showSfx);

        // fade in
        yield return StartCoroutine(FadeCanvas(1f));

        // glitch a bit (optional)
        if (glitchEffect && objectiveText != null)
            yield return StartCoroutine(GlitchText());

        // visible for a while
        yield return new WaitForSeconds(visibleDuration);

        // fade out
        yield return StartCoroutine(FadeCanvas(0f));
        currentRoutine = null;
    }

    private IEnumerator FadeCanvas(float target)
    {
        if (canvasGroup == null) yield break;

        while (!Mathf.Approximately(canvasGroup.alpha, target))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, Time.deltaTime * fadeSpeed);
            yield return null;
        }
    }

    private IEnumerator GlitchText()
    {
        string original = objectiveText.text;
        int iterations = 8;
        for (int i = 0; i < iterations; i++)
        {
            objectiveText.text = RandomizeText(original);
            yield return new WaitForSeconds(0.04f);
        }
        objectiveText.text = original;
    }

    private string RandomizeText(string text)
    {
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Random.value > 0.88f && char.IsLetterOrDigit(chars[i]))
                chars[i] = (char)Random.Range(33, 126);
        }
        return new string(chars);
    }
}