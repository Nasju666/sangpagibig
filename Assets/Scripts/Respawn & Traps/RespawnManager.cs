using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance;

    [Header("Spawn")]
    public Transform spawnPoint;

    [Header("Black Screen (Optional)")]
    public bool useBlackFade = true;
    public Image fadeImage;
    public float blackFadeDuration = 1f;
    public AnimationCurve blackFadeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Death Text (Optional)")]
    public bool useDeathText = true;
    public Text deathText;                  // Legacy UI Text
    public TextMeshProUGUI deathTMP;         // TMP
    public float textFadeDuration = 0.6f;
    public AnimationCurve textFadeCurve = AnimationCurve.Linear(0, 0, 1, 1);

    [TextArea]
    public string[] deathMessages;
    public Color[] deathColors;

    [Header("Death Sound (Optional)")]
    public bool useDeathSound = true;
    public AudioSource deathAudioSource;
    public AudioClip[] deathSounds;
    [Range(0f, 1f)] public float deathVolume = 1f;

    private bool isRespawning = false;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitUI();
    }

    void InitUI()
    {
        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        if (deathText != null)
            deathText.gameObject.SetActive(false);

        if (deathTMP != null)
            deathTMP.gameObject.SetActive(false);
    }

    public void Respawn(GameObject player)
    {
        if (isRespawning) return;
        StartCoroutine(RespawnRoutine(player));
    }

    IEnumerator RespawnRoutine(GameObject player)
    {
        isRespawning = true;

        // Enable black screen if used
        if (useBlackFade && fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);
            yield return null;
        }

        // Play sound
        if (useDeathSound)
            PlayRandomDeathSound();

        // Fade to black
        if (useBlackFade && fadeImage != null)
            yield return FadeBlack(0f, 1f);

        // Show death text
        if (useDeathText)
        {
            ShowRandomDeathMessage();
            yield return FadeText(0f, 1f);
        }

        // Reset world objects
        foreach (MovingPlatformTrigger p in FindObjectsOfType<MovingPlatformTrigger>())
            p.ResetPlatform();

        foreach (SpikeTrap trap in FindObjectsOfType<SpikeTrap>())
            trap.ResetTrap();

            // 🔁 RESET MONSTERS (THIS IS THE KEY ADDITION)
        foreach (ChaseCharacter monster in FindObjectsOfType<ChaseCharacter>())
            monster.ResetMonster();

        // Disable movement
        CharacterController cc = player.GetComponent<CharacterController>();
        Rigidbody rb = player.GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Teleport
        player.transform.position = spawnPoint.position;
        yield return null;

        if (cc != null) cc.enabled = true;
        if (rb != null) rb.isKinematic = false;

        // Hide death text
        if (useDeathText)
        {
            yield return FadeText(1f, 0f);
            HideDeathText();
        }

        // Fade back
        if (useBlackFade && fadeImage != null)
            yield return FadeBlack(1f, 0f);

        if (fadeImage != null)
            fadeImage.gameObject.SetActive(false);

        isRespawning = false;
    }

    // ================= BLACK SCREEN =================

    IEnumerator FadeBlack(float from, float to)
    {
        Color c = fadeImage.color;
        c.a = from;
        fadeImage.color = c;

        float t = 0f;
        while (t < blackFadeDuration)
        {
            t += Time.deltaTime;
            float n = t / blackFadeDuration;
            float curve = blackFadeCurve.Evaluate(n);
            c.a = Mathf.Lerp(from, to, curve);
            fadeImage.color = c;
            yield return null;
        }

        c.a = to;
        fadeImage.color = c;
    }

    // ================= DEATH TEXT =================

    void ShowRandomDeathMessage()
    {
        if (deathMessages == null || deathMessages.Length == 0) return;

        string msg = deathMessages[Random.Range(0, deathMessages.Length)];
        Color col = (deathColors != null && deathColors.Length > 0)
            ? deathColors[Random.Range(0, deathColors.Length)]
            : Color.white;

        if (deathText != null)
        {
            deathText.text = msg;
            deathText.color = new Color(col.r, col.g, col.b, 0f);
            deathText.gameObject.SetActive(true);
        }

        if (deathTMP != null)
        {
            deathTMP.text = msg;
            deathTMP.color = new Color(col.r, col.g, col.b, 0f);
            deathTMP.gameObject.SetActive(true);
        }
    }

    void HideDeathText()
    {
        if (deathText != null) deathText.gameObject.SetActive(false);
        if (deathTMP != null) deathTMP.gameObject.SetActive(false);
    }

    IEnumerator FadeText(float from, float to)
    {
        float t = 0f;
        while (t < textFadeDuration)
        {
            t += Time.deltaTime;
            float n = t / textFadeDuration;
            float curve = textFadeCurve.Evaluate(n);
            SetTextAlpha(Mathf.Lerp(from, to, curve));
            yield return null;
        }

        SetTextAlpha(to);
    }

    void SetTextAlpha(float a)
    {
        if (deathText != null)
        {
            Color c = deathText.color;
            c.a = a;
            deathText.color = c;
        }

        if (deathTMP != null)
        {
            Color c = deathTMP.color;
            c.a = a;
            deathTMP.color = c;
        }
    }

    // ================= AUDIO =================

    void PlayRandomDeathSound()
    {
        if (deathAudioSource == null || deathSounds == null || deathSounds.Length == 0)
            return;

        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        deathAudioSource.volume = deathVolume;
        deathAudioSource.PlayOneShot(clip);
    }
}
