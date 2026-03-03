using TMPro;
using UnityEngine;

/// <summary>
/// HUD 표시에 대한 책임을 전담합니다.
/// - 코인/스테이지/점수 텍스트 갱신
/// - 코인 변경 이벤트(EconomyManager.I.OnCoinsChanged) 구독
/// </summary>
[DisallowMultipleComponent]
public class HUDController : MonoBehaviour
{
    [Header("HUD Text")]
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text messageText;

    private void Awake()
    {
        // 인스펙터에 연결이 안 되어 있으면, 이름으로 1회만 찾아서 연결합니다.
        // (가능하면 인스펙터에 직접 연결하는 것을 권장합니다.)
        if (coinText == null)
        {
            var go = GameObject.Find("CoinText");
            if (go != null) coinText = go.GetComponent<TMP_Text>();
        }

        if (stageText == null)
        {
            var go = GameObject.Find("StageText");
            if (go != null) stageText = go.GetComponent<TMP_Text>();
        }

        if (scoreText == null)
        {
            var go = GameObject.Find("ScoreText");
            if (go != null) scoreText = go.GetComponent<TMP_Text>();
        }

        if (livesText == null)
        {
            var go = GameObject.Find("LivesText");
            if (go != null) livesText = go.GetComponent<TMP_Text>();
        }

        if (messageText == null)
        {
            var go = GameObject.Find("MessageText");
            if (go != null) messageText = go.GetComponent<TMP_Text>();
        }
    }

    private void OnEnable()
    {
        // EconomyManager가 없으면 자동 생성
        EconomyManager.EnsureExists();

        // 코인 변경 이벤트 구독
        if (EconomyManager.I != null)
        {
            EconomyManager.I.OnCoinsChanged -= HandleCoinsChanged;
            EconomyManager.I.OnCoinsChanged += HandleCoinsChanged;

            // 시작 시 1회 갱신
            RefreshCoins();
        }
        else
        {
            // 예외 상황: EconomyManager 생성 실패
            RefreshCoins();
        }
    }

    private void OnDisable()
    {
        if (EconomyManager.I != null)
            EconomyManager.I.OnCoinsChanged -= HandleCoinsChanged;
    }

    private void HandleCoinsChanged(int coins)
    {
        if (coinText != null)
            coinText.text = $"COINS: {coins}";
    }

    public void RefreshCoins()
    {
        int coins = (EconomyManager.I != null) ? EconomyManager.I.GetCoins() : CosmeticSaveManager.GetCoins();
        HandleCoinsChanged(coins);
    }

    // 아래 함수들은 GameManager가 필요할 때 호출합니다.
    public void SetStage(int stage)
    {
        if (stageText != null)
            stageText.text = $"STAGE: {stage}";
    }

    public void SetScore(int score, int max)
    {
        if (scoreText != null)
            scoreText.text = $"SCORE: {score} / {max}";
    }

    public void SetLives(int lives, int maxLives)
    {
        if (livesText != null)
            livesText.text = $"LIVES: {lives}/{maxLives}";
    }

    public void SetMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg ?? "";
    }

    public void ClearMessage()
    {
        if (messageText != null)
            messageText.text = "";
    }

    public System.Collections.IEnumerator ShowMessageFor(string msg, float time)
    {
        SetMessage(msg);
        yield return new WaitForSeconds(time);
        ClearMessage();
    }

    public System.Collections.IEnumerator ShowMessageForRealtime(string msg, float time)
    {
        SetMessage(msg);
        yield return new WaitForSecondsRealtime(time);
        ClearMessage();
    }

}