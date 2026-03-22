/// <summary>
/// SendMessage("TakeDamage") 대신 인터페이스 직접 호출로 성능 개선.
/// EnemyGalaga 등 데미지를 받는 컴포넌트에 구현.
/// </summary>
public interface ITakeDamage
{
    void TakeDamage(int amount);
}
