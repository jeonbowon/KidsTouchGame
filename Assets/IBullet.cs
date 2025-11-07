// IBullet.cs  ← enum + interface를 이 파일 한 곳에만 둡니다.
using UnityEngine;

public enum BulletOwner { Player, Enemy }

public interface IBullet
{
    void SetOwner(BulletOwner owner);
    void SetDirection(Vector2 dir);
    void SetSpeed(float speed);
    void ActivateAt(Vector3 pos);
}
