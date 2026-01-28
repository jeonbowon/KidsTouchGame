using UnityEngine;

[CreateAssetMenu(menuName = "KidsTouchGame/Difficulty Config", fileName = "DifficultyConfig")]
public class DifficultyConfig : ScriptableObject
{
    [System.Serializable]
    public struct FloatByStage
    {
        public float stage1;
        public float perStage;
        public Vector2 clamp; // x=min, y=max

        public float Eval(int stage)
        {
            int s = Mathf.Max(1, stage);
            float v = stage1 + (s - 1) * perStage;
            return Mathf.Clamp(v, clamp.x, clamp.y);
        }
    }

    [System.Serializable]
    public struct IntByStage
    {
        public int stage1;
        public int perStage;
        public Vector2Int clamp; // x=min, y=max

        public int Eval(int stage)
        {
            int s = Mathf.Max(1, stage);
            int v = stage1 + (s - 1) * perStage;
            return Mathf.Clamp(v, clamp.x, clamp.y);
        }
    }

    [Header("Player Bullet (Mul)")]
    public FloatByStage playerBulletMul = new FloatByStage
    {
        stage1 = 1.0f,
        perStage = 0.0f,
        clamp = new Vector2(0.1f, 3.0f)
    };

    [Header("Enemy Bullet Speed (Absolute)")]
    public FloatByStage enemyBulletSpeed = new FloatByStage
    {
        stage1 = 3.5f,
        perStage = 0.6f,
        clamp = new Vector2(0.5f, 10f)
    };

    [Header("Enemy Galaga Move Speed")]
    public FloatByStage galagaMoveSpeed = new FloatByStage
    {
        stage1 = 2.0f,
        perStage = 0.3f,
        clamp = new Vector2(0.5f, 20f)
    };

    [Header("Enemy Random Mover - Down Speed")]
    public FloatByStage randomDownSpeed = new FloatByStage
    {
        stage1 = 1.5f,
        perStage = 0.2f,
        clamp = new Vector2(0.5f, 8.0f)
    };

    [Header("Enemy Random Mover - Horizontal Speed")]
    public FloatByStage randomHorizontalSpeed = new FloatByStage
    {
        stage1 = 2.0f,
        perStage = 0.15f,
        clamp = new Vector2(0.5f, 10.0f)
    };

    [Header("Enemy Bonus Mover - Speed Min/Max")]
    public FloatByStage bonusSpeedMin = new FloatByStage
    {
        stage1 = 1.5f,
        perStage = 0.12f,
        clamp = new Vector2(0.5f, 20.0f)
    };

    public FloatByStage bonusSpeedMax = new FloatByStage
    {
        stage1 = 2.5f,
        perStage = 0.15f,
        clamp = new Vector2(0.5f, 25.0f)
    };

    [Header("Spawner - Spawn Interval")]
    public FloatByStage spawnInterval = new FloatByStage
    {
        stage1 = 2.0f,
        perStage = -0.3f,
        clamp = new Vector2(0.4f, 4.0f)
    };

    [Header("Spawner - Max Alive Enemies (Optional)")]
    public IntByStage maxAliveEnemies = new IntByStage
    {
        stage1 = 25,
        perStage = 2,
        clamp = new Vector2Int(1, 200)
    };

    [Header("Spawner - Tough Spawn Chance (0..1)")]
    public FloatByStage toughSpawnChance = new FloatByStage
    {
        stage1 = 0.0f,
        perStage = 0.03f,
        clamp = new Vector2(0f, 0.8f)
    };
}
