using UnityEngine;
using System.Collections;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("Intro")]
    public TutorialStep[] introSteps;

    [Header("Tutorial Steps")]
    public TutorialStep[] tutorialSteps;

    [Header("Final Completion")]
    [TextArea(2, 4)]
    public string finalCompletionMessage = "Tutorial complete!";
    public float finalCompletionTypingSpeed = 0.03f;
    public float finalCompletionDelay = 1f;
    public AudioClip finalCompletionAudio;

    [Header("Skip Tutorial")]
    public bool allowSkip = true;
    public KeyCode skipKey = KeyCode.LeftShift;
    public float holdToSkipTime = 1.5f;

    [TextArea(1, 2)]
    public string skipIndicatorText = "Skipping tutorial";
    public TextMeshProUGUI skipIndicatorTextUI;

    [Header("References")]
    public TypingTextUI typingUI;
    public AudioSource audioSource;
    public BackgroundMusicDucker backgroundMusic;
    public PlayerMovement playerMovement;
    public MouseLook mouseLook;

    int stepIndex;
    bool canCheckCondition;
    bool tutorialSkipped;

    bool isSkipping;
    float skipTimer;

    // Ability locks
    bool allowMove;
    bool allowJump;
    bool allowCrouch;
    bool allowLook;

    // Cached values
    float baseWalkSpeed;
    float baseSprintSpeed;
    float baseCrouchSpeed;
    float baseJumpHeight;
    float baseCrouchHeight;

    void Start()
    {
        if (MainMenuManager.IsMultiplayer)
        {
            enabled = false;
            return;
        }

        if (playerMovement == null || typingUI == null)
        {
            if (playerMovement == null)
                playerMovement = FindObjectOfType<PlayerMovement>();
            if (typingUI == null)
                typingUI = FindObjectOfType<TypingTextUI>();

            if (playerMovement == null || typingUI == null)
            {
                enabled = false;
                return;
            }
        }

        baseWalkSpeed = playerMovement.walkSpeed;
        baseSprintSpeed = playerMovement.sprintSpeed;
        baseCrouchSpeed = playerMovement.crouchSpeed;
        baseJumpHeight = playerMovement.jumpHeight;
        baseCrouchHeight = playerMovement.crouchHeight;

        if (skipIndicatorTextUI != null)
        {
            skipIndicatorTextUI.text = "";
            skipIndicatorTextUI.gameObject.SetActive(false);
        }

        typingUI.ForceEnable();
        ApplyAbilityLocks();
        StartCoroutine(PlayIntro());
    }

    void Update()
    {
        if (!allowSkip || tutorialSkipped)
            return;

        if (Input.GetKey(skipKey))
        {
            skipTimer += Time.deltaTime;

            if (!isSkipping)
                BeginSkip();

            UpdateSkipIndicator();

            if (skipTimer >= holdToSkipTime)
                ConfirmSkip();
        }
        else if (isSkipping)
        {
            CancelSkip();
        }
    }

    // ================= SKIP =================

    void BeginSkip()
    {
        isSkipping = true;
        skipTimer = 0f;

        typingUI.Pause();
        audioSource?.Pause();

        if (skipIndicatorTextUI != null)
        {
            skipIndicatorTextUI.gameObject.SetActive(true);
            UpdateSkipIndicator();
        }
    }

    void CancelSkip()
    {
        isSkipping = false;
        skipTimer = 0f;

        typingUI.Resume();
        audioSource?.UnPause();

        if (skipIndicatorTextUI != null)
        {
            skipIndicatorTextUI.text = "";
            skipIndicatorTextUI.gameObject.SetActive(false);
        }
    }

    public void ConfirmSkip()
    {
        tutorialSkipped = true;

        StopAllCoroutines();
        typingUI.Clear();
        audioSource?.Stop();

        backgroundMusic?.UnregisterSound();

        if (skipIndicatorTextUI != null)
        {
            skipIndicatorTextUI.text = "";
            skipIndicatorTextUI.gameObject.SetActive(false);
        }

        allowMove = allowJump = allowCrouch = allowLook = true;
        ApplyAbilityLocks();
    }

    void UpdateSkipIndicator()
    {
        if (skipIndicatorTextUI == null)
            return;

        float remaining = Mathf.Max(0f, holdToSkipTime - skipTimer);
        skipIndicatorTextUI.text =
            $"{skipIndicatorText} ({remaining:F1}s)";
    }

    // ================= CORE =================

    void ApplyAbilityLocks()
    {
        if (playerMovement == null)
            return;

        playerMovement.walkSpeed = allowMove ? baseWalkSpeed : 0f;
        playerMovement.sprintSpeed = allowMove ? baseSprintSpeed : 0f;
        playerMovement.crouchSpeed = allowMove ? baseCrouchSpeed : 0f;

        playerMovement.jumpHeight = allowJump ? baseJumpHeight : 0f;
        playerMovement.crouchHeight =
            allowCrouch ? baseCrouchHeight : playerMovement.standingHeight;

        if (mouseLook != null)
            mouseLook.enabled = allowLook;
    }

    IEnumerator PlayIntro()
    {
        foreach (var step in introSteps)
            yield return PlayStepRoutine(step, false);

        stepIndex = 0;
        StartCoroutine(PlayTutorialStep());
    }

    IEnumerator PlayTutorialStep()
    {
        if (stepIndex >= tutorialSteps.Length)
        {
            yield return PlayFinalCompletion();
            yield break;
        }

        yield return PlayStepRoutine(tutorialSteps[stepIndex], true);
    }

    IEnumerator PlayStepRoutine(TutorialStep step, bool waitForCondition)
    {
        if (tutorialSkipped)
            yield break;

        canCheckCondition = false;
        typingUI.ForceEnable();

        typingUI.PlayText(step.text, step.typingSpeed);

        if (step.audio)
        {
            backgroundMusic?.RegisterSound();
            audioSource?.PlayOneShot(step.audio);
        }

        while (typingUI.IsTyping && !tutorialSkipped)
            yield return null;

        backgroundMusic?.UnregisterSound();
        yield return new WaitForSeconds(step.delayAfterText);

        if (!waitForCondition)
            yield break;

        switch (step.condition)
        {
            case TutorialCondition.LookAround: allowLook = true; break;
            case TutorialCondition.Move: allowMove = true; break;
            case TutorialCondition.Jump: allowJump = true; break;
            case TutorialCondition.Crouch: allowCrouch = true; break;
        }

        ApplyAbilityLocks();
        canCheckCondition = true;

        while (!CheckCondition(step.condition) && !tutorialSkipped)
            yield return null;

        canCheckCondition = false;

        if (!string.IsNullOrEmpty(step.completionText))
        {
            typingUI.PlayText(step.completionText, step.completionTypingSpeed);

            if (step.completionAudio)
            {
                backgroundMusic?.RegisterSound();
                audioSource?.PlayOneShot(step.completionAudio);
            }

            while (typingUI.IsTyping)
                yield return null;

            backgroundMusic?.UnregisterSound();
            yield return new WaitForSeconds(step.completionDelay);
        }

        stepIndex++;
        StartCoroutine(PlayTutorialStep());
    }

    IEnumerator PlayFinalCompletion()
    {
        typingUI.PlayText(finalCompletionMessage, finalCompletionTypingSpeed);

        if (finalCompletionAudio)
        {
            backgroundMusic?.RegisterSound();
            audioSource?.PlayOneShot(finalCompletionAudio);
        }

        while (typingUI.IsTyping)
            yield return null;

        backgroundMusic?.UnregisterSound();
        yield return new WaitForSeconds(finalCompletionDelay);
        typingUI.Clear();
    }

    bool CheckCondition(TutorialCondition condition)
    {
        if (!canCheckCondition)
            return false;

        return condition switch
        {
            TutorialCondition.LookAround =>
                Mathf.Abs(Input.GetAxis("Mouse X")) > 0.2f ||
                Mathf.Abs(Input.GetAxis("Mouse Y")) > 0.2f,

            TutorialCondition.Move =>
                Input.GetAxisRaw("Horizontal") != 0 ||
                Input.GetAxisRaw("Vertical") != 0,

            TutorialCondition.Jump =>
                Input.GetKeyDown(KeyCode.Space),

            TutorialCondition.Crouch =>
                Input.GetKey(KeyCode.LeftControl),

            _ => false
        };
    }
}
