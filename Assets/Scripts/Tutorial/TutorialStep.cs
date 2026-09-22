using UnityEngine;

[System.Serializable]
public class TutorialStep
{
    [Header("Main Text")]
    [TextArea(2, 4)]
    public string text;

    public AudioClip audio;

    [Header("Typing")]
    public float typingSpeed = 0.03f;
    public float delayAfterText = 0.5f;

    [Header("Completion Message (Optional)")]
    [TextArea(2, 4)]
    public string completionText;
    public AudioClip completionAudio; 
    public float completionTypingSpeed = 0.03f;
    public float completionDelay = 0.5f;

    public TutorialCondition condition;
}

public enum TutorialCondition
{
    None,
    LookAround,
    Move,
    Jump,
    Crouch
}
