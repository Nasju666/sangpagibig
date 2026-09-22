using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.UI;
using System.Collections;

public class Portal : MonoBehaviour
{
    public enum PortalType
    {
        Real,
        Fake
    }

    [Header("Portal Type")]
    public PortalType portalType;

    // ================= REAL PORTAL =================
    [Header("Real Portal Settings")]
    public string sceneToLoad;

    // ================= FAKE PORTAL =================
    [Header("Fake Portal Settings")]
    public VideoPlayer videoPlayer;
    public Transform respawnPoint;
    public float holdToSkipTime = 1.5f;

    // ================= FADE =================
    [Header("Fade Settings")]
    public bool useFade;
    public Canvas fadeCanvas;
    public Image fadeImage;
    public float fadeInDuration = 0.15f;

    // ================= FLASHBANG SOUND =================
    [Header("Flashbang Sound (Optional)")]
    public bool useFlashbangSound;
    public AudioSource flashbangAudioSource;
    public AudioClip flashbangClip;
    public float audioFadeOutTime = 1.0f;

    // ================= CAMERA =================
    [Header("Camera")]
    public Camera targetCamera;

    private bool used;
    private Transform currentPlayer;
    private float holdTimer;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (used) return;

        used = true;
        currentPlayer = other.transform;

        if (portalType == PortalType.Real)
            StartCoroutine(RealPortalRoutine());
        else
            StartCoroutine(FakePortalRoutine());
    }

    // ================= REAL PORTAL =================
    IEnumerator RealPortalRoutine()
    {
        FreezePlayer();
        yield return FadeToBlack();
        SceneManager.LoadScene(sceneToLoad);
    }

    // ================= FAKE PORTAL =================
    IEnumerator FakePortalRoutine()
    {
        FreezePlayer();
        yield return FadeToBlack();

        SetupVideoPlayer();

        if (videoPlayer == null)
        {
            ResetPlayer();
            yield break;
        }

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
            yield return null;

        videoPlayer.Play();

        double videoLength = videoPlayer.length;
        float timer = 0f;
        holdTimer = 0f;

        while (timer < videoLength)
        {
            if (Input.GetKey(KeyCode.Space))
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdToSkipTime)
                    break;
            }
            else
            {
                holdTimer = 0f;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        videoPlayer.Stop();
        ResetPlayer();
    }

    // ================= FADE =================
    IEnumerator FadeToBlack()
    {
        if (!useFade || fadeCanvas == null || fadeImage == null)
            yield break;

        fadeCanvas.gameObject.SetActive(true);
        ForceFullScreenBlack(fadeImage);

        if (useFlashbangSound &&
            flashbangAudioSource != null &&
            flashbangClip != null)
        {
            flashbangAudioSource.volume = 1f;
            flashbangAudioSource.PlayOneShot(flashbangClip);
            StartCoroutine(FadeOutAudio());
        }

        yield return Fade(0f, 1f, fadeInDuration);
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        Color c = fadeImage.color;

        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / duration);
            fadeImage.color = c;
            yield return null;
        }

        c.a = to;
        fadeImage.color = c;
    }

    IEnumerator FadeOutAudio()
    {
        float startVolume = flashbangAudioSource.volume;
        float t = 0f;

        while (t < audioFadeOutTime)
        {
            t += Time.deltaTime;
            flashbangAudioSource.volume =
                Mathf.Lerp(startVolume, 0f, t / audioFadeOutTime);
            yield return null;
        }

        flashbangAudioSource.volume = 0f;
    }

    // ================= VIDEO =================
    void SetupVideoPlayer()
    {
        if (videoPlayer == null || targetCamera == null) return;

        videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        videoPlayer.targetCamera = targetCamera;
        videoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.playOnAwake = false;

        targetCamera.clearFlags = CameraClearFlags.SolidColor;
        targetCamera.backgroundColor = Color.black;
    }

    // ================= PLAYER =================
    void FreezePlayer()
    {
        CharacterController cc = currentPlayer.GetComponent<CharacterController>();
        Rigidbody rb = currentPlayer.GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    void ResetPlayer()
    {
        CharacterController cc = currentPlayer.GetComponent<CharacterController>();
        Rigidbody rb = currentPlayer.GetComponent<Rigidbody>();

        currentPlayer.position = respawnPoint.position;
        currentPlayer.rotation = respawnPoint.rotation;

        if (cc != null) cc.enabled = true;
        if (rb != null) rb.isKinematic = false;

        used = false;
    }

    void ForceFullScreenBlack(Image image)
    {
        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
