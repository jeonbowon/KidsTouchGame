# KidsTouchGame 프로젝트 분석 (2026-04-26)

## 기본 정보

- **게임명**: Kids Touch Game
- **엔진**: Unity 6000.2.8f1
- **플랫폼**: Android (Google Play) / iOS
- **장르**: 2D 슈팅 (Galaga 스타일)
- **현재 버전**: v1.0.8

---

## 디렉토리 구조

```
KidsTouchGame/
├── Assets/
│   ├── Art/                        스프라이트, 이미지
│   ├── Audio/                      BGM, SFX
│   ├── Data/Background/            배경 ScriptableObject
│   ├── Editor/                     에디터 확장
│   ├── GoogleMobileAds/            Google Mobile Ads SDK
│   ├── ExternalDependencyManager/  의존성 관리
│   ├── Plugins/Android,iOS         플랫폼 플러그인
│   ├── Prefabs/
│   │   └── Background/BG_Root.prefab
│   ├── Resources/
│   │   ├── Cosmetics/
│   │   │   ├── CosmeticDatabase.asset
│   │   │   └── Items/ShipSkins/, Weapons/
│   │   └── UI/
│   │       ├── GameOverPanel.prefab
│   │       └── CosmeticUnlockPopup.prefab
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   └── Stage1.unity ~ Stage30.unity
│   ├── Script/
│   │   ├── Background/
│   │   └── DifficultyConfig.cs
│   ├── Settings/Scenes/
│   └── TextMesh Pro/
├── Packages/
├── ProjectSettings/
├── Build/
└── Logs/
```

---

## 전체 스크립트 목록 (54개)

### 게임 핵심
| 파일 | 줄 수 | 역할 |
|------|------|------|
| `GameManager.cs` | 1,073 | 싱글톤 `GameManager.I`, 생명/스코어/스테이지/Continue/코스메틱 해금 전체 관리 |
| `DifficultyConfig.cs` | — | ScriptableObject, 스테이지별 난이도 파라미터 |

### 플레이어
| 파일 | 줄 수 | 역할 |
|------|------|------|
| `PlayerMovement.cs` | 203 | Rigidbody2D, 터치·키보드 드래그, Camera 기반 화면 경계 제한 |
| `PlayerShoot.cs` | 298 | 자동/수동 발사, Twin 모드, 장착 무기 연동, 발사 각도 제한(90도) |
| `PlayerHealth.cs` | 196 | 충돌 데미지, 무적 상태(1.5초), 무적 시 깜빡임 |
| `PlayerPowerUp.cs` | 146 | Twin 모드 활성화(기본 8초), 메인/트윈 스프라이트 싱크 |
| `PlayerCosmeticApplier.cs` | — | 장착 코스메틱 시각 적용 |

### 총알
| 파일 | 줄 수 | 역할 |
|------|------|------|
| `Bullet.cs` | 332 | IBullet 구현, 관통(pierceRemain)/유도(0.1초 재탐색)/폭발(범위)/크리티컬, `_hitOnce` HashSet으로 적당 1회 히트 |
| `EnemyBullet.cs` | — | 플레이어 충돌 시 `PlayerHealth.Die()` 호출 |

### 적
| 파일 | 줄 수 | 역할 |
|------|------|------|
| `EnemyGalaga.cs` | 515 | ITakeDamage 구현, HP/피격 깜빡임/드랍/폭발, Two-Pip HP UI |
| `EnemySpawner.cs` | 236 | 자동 생성(상단+좌우), 스테이지별 스폰 간격·최대 수·탱커 확률 조정 |
| `EnemyShooter.cs` | — | 적 총알 발사 |
| `EnemyRandomMover.cs` | — | 자유 이동 패턴 |
| `EnemyBonusMover.cs` | — | 빠른 보너스 적 이동 |

### 아이템
| 파일 | 역할 |
|------|------|
| `ScoreItem.cs`, `ScoreItemDouble.cs` | 점수 드랍 아이템 |
| `CoinItem.cs`, `CoinItemDouble.cs` | 코인 드랍 → `CosmeticSaveManager.AddCoins()` |
| `BulletSpeedBonusItem.cs` | 총알 속도 시간제 버프 |

### 코스메틱
| 파일 | 줄 수 | 역할 |
|------|------|------|
| `CosmeticDatabase.cs` | 72 | ScriptableObject, `List<CosmeticItem>`, GetById/GetByCategory/GetUnlocksForStageClear |
| `CosmeticItem.cs` | 188 | ScriptableObject(무기·배스킨), 무기 속성, `CalculateWeight()`, `IsWeaponValid()` |
| `CosmeticSaveData.cs` | 71 | PlayerPrefs 저장 구조 (coins, unlockedIds, ownedIds, equipped) |
| `CosmeticSaveManager.cs` | 132 | 정적 매니저, GetCoins/AddCoins/TrySpendCoins/IsUnlocked/GrantUnlocked/Equip |
| `CosmeticUnlockPopup.cs` | — | 스테이지 클리어 해금 팝업 |
| `CosmeticBootstrap.cs` | — | 코스메틱 시스템 초기화 |

### 경제·수익화
| 파일 | 줄 수 | 역할 |
|------|------|------|
| `EconomyManager.cs` | 83 | 싱글톤 DontDestroyOnLoad, CosmeticSaveManager 래퍼, `OnCoinsChanged` 이벤트 |
| `IAPManager.cs` | 182 | Unity Purchasing SDK, `remove_ads`(Non-Consumable) + `coin_10000`(Consumable) |
| `MonetizationManager.cs` | 278 | AdManager + IAPManager 통합, `IsAdsDisabled` 캐시, Rewarded/Interstitial 코루틴 |
| `AdManager.cs` | — | Google Mobile Ads 래퍼 |

### UI
| 파일 | 줄 수 | 역할 |
|------|------|------|
| `HUDController.cs` | 138 | 인게임 HUD (코인/스테이지/스코어/생명), `EconomyManager.OnCoinsChanged` 구독 |
| `StoreController.cs` | 340 | 상점 UI, ShipSkin·Weapon·IAP 탭, 카드 정렬(장착>소유>해금>가격), ModalBlocker |
| `StoreItemCard.cs` | — | 개별 아이템 카드 |
| `StoreConfirmPopup.cs` | — | 구매 확인 팝업 |
| `GameOverPanel.cs` | 136 | 게임오버 UI, `Show(info, showButtons)`, `OnContinueClicked` / `OnMenuClicked` |
| `MainMenuController.cs` | 759 | 메인메뉴 전체 관리, Settings 패널, Store 패널, DevCheats(코너 탭 제스처) |

### 사운드
| 파일 | 역할 |
|------|------|
| `BGMManager.cs` | 배경음악 관리 (페이드 인/아웃) |
| `SfxManager.cs` | 효과음 재생 |
| `SceneBGM.cs` | 씬별 BGM 설정 |

### 유틸·기타
| 파일 | 역할 |
|------|------|
| `PoolManager.cs` (121줄) | 오브젝트 풀 Get/Return/WarmUp |
| `IBullet.cs`, `ITakeDamage.cs` | 인터페이스 |
| `DestroyAfterAnimation.cs` | 애니메이션 후 자동 파괴 |
| `SafeAreaTopOffset.cs` | Notch 대응 |
| `FitBackgroundToCamera.cs` | 배경 카메라 맞춤 |
| `DevCheats.cs` | 개발자 치트 명령 |

---

## 씬 구성

| 씬 | 내용 |
|----|------|
| `MainMenu.unity` | 시작 화면, Settings(BGM/SFX 슬라이더), Store(Additive 로드) |
| `Stage1.unity ~ Stage30.unity` | 30개 스테이지, Player 스폰 포인트, EnemySpawner, Canvas(HUD + GameOverPanel), Background Layer |

---

## 게임 로직 흐름

### 시작
```
MainMenu.unity
  → GameManager.Awake()  (싱글톤, DontDestroyOnLoad)
  → MonetizationManager, IAPManager 자동 생성
  → StartGame() 클릭
  → GameManager.NewRun() → Stage1 씬 로드
```

### 스테이지 진행
```
씬 로드
  → GameManager.OnSceneLoaded()
  → Co_StartStage()
  → "STAGE N" 메시지 표시 (1.2초)
  → Player 스폰
  → EnemySpawner 활성화
  → 플레이어 조작 + 적 생성 + 점수 획득
```

### 스테이지 클리어 (Score >= 100)
```
Co_StageClear()
  1. 모든 적·총알 제거
  2. 클리어 메시지 (1.5초)
  3. 코인 지급 (기본 30 + Stage×5)
  4. 새 아이템 해금 확인 → CosmeticUnlockPopup 표시
  5. CurrentStage++ / Score 초기화
  6. 다음 스테이지 씬 로드
```

### 게임 오버
```
PlayerHP = 0
  → Lives--
  → Lives > 0  →  Co_RespawnPlayerWithMessage()  (무적 1.5초)
  → Lives = 0  →  Co_GameOver()
                   → GameOverPanel 표시

  [CONTINUE 선택]
    광고제거 구매?  →  즉시 성공
    미구매?         →  Rewarded 광고 대기 (최대 8초)
                       성공  →  Lives=3, 재스폰
                       실패  →  "AD NOT AVAILABLE" 재시도 대기

  [MENU 선택]
    광고제거 구매?  →  즉시 MainMenu 이동
    미구매?         →  Interstitial 광고 대기 (최대 4초)  →  MainMenu
```

### Continue 성공 후
```
Co_PostContinueStep()
  → "VISIT SHOP?" 팝업
  → SHOP  →  MainMenu Additive 로드 → StoreController 표시
  → SKIP  →  즉시 게임 재개
```

---

## 난이도 파라미터 (DifficultyConfig ScriptableObject)

| 파라미터 | Stage1 기본값 | 스테이지당 변화 | 범위 |
|---------|-------------|--------------|------|
| enemyBulletSpeed | 3.5 | +0.6 | 0.5 ~ 10 |
| galagaMoveSpeed | 2.0 | +0.3 | 0.5 ~ 20 |
| spawnInterval | 2.0 | -0.3 | 0.4 ~ 4.0 |
| maxAliveEnemies | 25 | +2 | 1 ~ 200 |
| toughSpawnChance | 0.0 | +0.03 | 0 ~ 0.8 |
| stageClearCoins | 30 | +5 | 30 ~ 200 |

---

## 코스메틱 아이템 구조 (CosmeticItem ScriptableObject)

**공통 필드**
- `id`, `displayName`, `category` (ShipSkin / Weapon)
- `icon`, `shipSprite`, `bulletSprite`
- `unlockOnStageClear` (0 = 처음부터, N = N스테이지 클리어 시 해금)
- `priceCoins` (상점 구매 가격)

**무기 전용 필드**
- 코어: `damageMul`, `speedMul`, `fireIntervalMul`, `shotCount`, `spreadAngle`
- 주요 효과(Max 1): `usePierce/pierceCount`, `useHoming`, `useExplosion`
- 부가 효과: `hitRadiusMul`, `critChance`, `slowPercent`
- 밸런스: `CalculateWeight()`, `IsWeaponValid()` (Weight ≤ maxWeight)

---

## IAP 상품

| 상품 ID | 종류 | 내용 |
|---------|------|------|
| `remove_ads` | Non-Consumable | 광고 영구 제거 |
| `coin_10000` | Consumable | 코인 10,000개 즉시 지급 |

---

## 태그 / 소팅 레이어

**Tags**: Enemy, Bullet, Explosion, EnemyBullet, BGM, PlayerSpawn, Item

**Sorting Layers** (낮음 → 높음):
Background → Enemy → Player → Bullet → UI → Effects → Coin

---

## 패키지 의존성

| 패키지 | 버전 |
|--------|------|
| com.unity.2d.animation | 12.0.2 |
| com.unity.2d.aseprite | 2.0.2 |
| com.unity.inputsystem | 1.14.2 |
| com.unity.purchasing | 5.0.4 |
| com.unity.render-pipelines.universal | 17.2.0 |

---

## 주요 주의사항 / 알려진 버그 포인트

1. **Pierce + EnemyBullet 이중처리 방지**  
   `Bullet.cs`의 `_lastEnemyBulletHitFrame`으로 같은 프레임 내 중복 히트 무시

2. **결제 중 UI 재진입 차단**  
   `IAPManager.IsPurchaseInProgress` 플래그 → `StoreController.Update()`에서 ModalBlocker 활성화

3. **Additive Shop 오버레이 모드**  
   `MainMenuController._overlayStoreMode` 플래그, Canvas sorting order = 999

4. **광고제거 상태 이중 관리**  
   - `IAPManager.HasNoAds()` — PlayerPrefs 직접 확인  
   - `MonetizationManager.IsAdsDisabled` — 캐시값

5. **폭발음 스팸 방지**  
   `EnemyGalaga.cs`: 프레임당 최대 3개 재생 제한

---

## 리소스 경로

| 경로 | 내용 |
|------|------|
| `Resources/Cosmetics/CosmeticDatabase` | CosmeticDatabase ScriptableObject |
| `Resources/GameOverPanel` | GameOverPanel 프리팹 |
| `Resources/UI/CosmeticUnlockPopup` | 해금 팝업 프리팹 |

---

## 최근 커밋 히스토리

| 해시 | 내용 |
|------|------|
| fdee70d | keystore 경로변경 |
| 02d6202 | v1.0.8 성능 최적화 및 이동감 개선 |
| 1f0b1b4 | Update gamemanager |
| da7b0c6 | Update project settings |
| fe593d2 | Store popup update |
| fe48068 | Add IAPManager |
| fd48404 | Enemy prefab tuning |
| 27dd88b | Stage 11~30 tuning |
| fbe0e76 | Add Stage21 to Stage30 |
| 77e4927 | Enemy difficulty scaling |
| b28b030 | Add weapon sprites & cosmetic items |
