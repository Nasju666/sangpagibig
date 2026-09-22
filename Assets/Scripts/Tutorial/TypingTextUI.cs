using UnityEngine;
using TMPro;
using System.Collections;

public class TypingTextUI : MonoBehaviour
{
    public TextMeshProUGUI textUI;

    Coroutine typingRoutine;
    public bool IsTyping { get; private set; }

    bool isPaused;

    string currentText;
    float currentSpeed;
    int charIndex;

    void Awake()
    {
        // Ensure component itself is active
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        // Safety check
        if (textUI == null)
        {
            Debug.LogError("TypingTextUI: TextMeshProUGUI reference is missing.");
            enabled = false;
            return;
        }

        // Ensure TMP object is active
        if (!textUI.gameObject.activeSelf)
            textUI.gameObject.SetActive(true);
    }

    // 🔑 REQUIRED BY TutorialManager
    public void ForceEnable()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (!textUI.gameObject.activeSelf)
            textUI.gameObject.SetActive(true);
    }

    public void PlayText(string text, float typingSpeed)
    {
        ForceEnable();

        StopTyping();

        currentText = text ?? "";
        currentSpeed = Mathf.Max(typingSpeed, 0.001f);

        typingRoutine = StartCoroutine(TypeRoutine());
    }

    IEnumerator TypeRoutine()
    {
        IsTyping = true;
        textUI.text = "";
        charIndex = 0;

        while (charIndex < currentText.Length)
        {
            if (!isPaused)
            {
                textUI.text += currentText[charIndex];
                charIndex++;
            }

            yield return new WaitForSeconds(currentSpeed);
        }

        IsTyping = false;
    }

    public void Pause()
    {
        isPaused = true;
    }

    public void Resume()
    {
        isPaused = false;
    }

    public void StopTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        IsTyping = false;
        isPaused = false;
    }

    // ✅ Clear text safely (do NOT disable objects)
    public void Clear()
    {
        StopTyping();
        textUI.text = "";
    }

    // Optional visual hide without disabling GameObjects
    public void SetVisible(bool visible)
    {
        textUI.alpha = visible ? 1f : 0f;
    }
}
