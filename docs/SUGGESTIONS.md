# SUGGESTIONS — Pre-existing bugs and rough edges

---

## ✅ ÇÖZÜLDÜ — 2026-04-29 (Checkpoint #1) — Cascade DestroySelf early-fire (timestamp shift)

**Status:** 🟢 **RESOLVED** · **Step 1 of 2** in fall regression fix sequence.

### Bulgu

ProcessCascade içinde `FallIteration → AdvanceTime → FindMatches → DestroySelf` sıralaması, fall animasyonu BİTMEDEN destroy callback'inin firmasına yol açıyordu. Numerik trace (FALL_TIME=TIME_STEP=0.1):

- iter girişinde `m_currentTimeStamp = T`
- `FallIteration` Fall cmd'i `startTimeStamp = T + 0.1` ile emit ediyordu (`+ fallTime` shift, FallManager:25,33,38,54)
- `AdvanceTime()` → `T += 0.1` → 0.1 daha (TIME_STEP)
- Cascade match → `DestroySelf` cmd'i `startTimeStamp = T + 0.1`'de
- View Sequence'inde Fall (T+0.1 → T+0.2) ile DestroySelf (T+0.1) **aynı anda** firmesi → tile mid-tween destroy.

### Fix (atomik, 2 dosya)

1. `Assets/Scripts/Managers/FallManager.cs` — 4 yerde `timeStamp + fallTime` → `timeStamp` (Fall, FallRight, FallLeft, Spawn cmd startTimeStamp'ı). Konvansiyon: command START'ı taşır.
2. `Assets/Scripts/Models/BoardModel.cs:247` — fall sonrası `AdvanceTime()` yerine `m_currentTimeStamp += FALL_TIME`. Semantik: model saati fall'un view'de tamamlandığı T'ye eşitleniyor.

### Test (Tests/PureLogic/CascadeTimingTests.cs)

İnvariant: `forall DestroySelf at C with t_d, forall Fall→C with t_f <= t_d : t_d >= t_f + FALL_TIME`. Pre-fix RED, post-fix GREEN. Toplam 84 → 86 test geçiyor.

### Revert için

Manuel revert: 2 dosyada 5 satırı geri al. `git diff` yok (proje repo değil). FallManager 4 satırda `timeStamp` → `timeStamp + fallTime`, BoardModel L247'de yeni satırı `AdvanceTime();` ile değiştir.

### Sonraki adım

Step 2: IsMoving fail-safe kalkanı (8 dosya) — bu fix tek başına ana early-destroy senaryosunu çözüyor; IsMoving daha geniş edge case'lerde (multi-iter cascade'de henüz settle olmamış tile'lar) ek güvenlik katmanı olarak ekleniyor.

---

## ✅ ÇÖZÜLDÜ — 2026-04-29 (Checkpoint #2) — Step 2 IsMoving + test'in fantom Purple kullanımı

**Status:** 🟢 **RESOLVED** · **Step 2 of 2** in fall regression fix sequence.

### Bulgu

**Step 2 plumbing (planlı):** `IMovable.IsMoving` flag + `MatchManager` mid-tween shield. Match-3 altın kuralı: bir taş hedefine oturmadan match'e dahil olamaz. Filter Model'de **kalmak zorunda**.

**Bug — Test "fantom Purple" kullanıyordu:**

`TestBoardBuilder.cs:57` `'p'` karakterini `new Matchable(TileType.Purple)` üretiyor. Ama production'da Purple **gerçek bir matchable değil**:

- `TileFactory.CreateTileFromType` switch'inde Purple yok → `default` branch'i `LogError + null` döner.
- `TileFactory.CreateRandomColorTile` sadece R/G/B/Y kullanıyor.
- `Matchable.cs` `isMatchSource` damage check'inde Purple yok.

Sonuç: bir Matchable instance Purple TileType ile yaratılınca **match'lenir** (MatchManager TileType eşitliği bakar) ama **damage almaz** — `isMatchSource=false` → `if (m_isMatched && source != ColorBomb) return Unaffected;` → match-cells immortal.

### Pre-Step 2 neden geçiyordu

IsMoving filter olmadan cascade fall sırasında match'leri yakalıyor, deterministik random seed=42 farklı pozisyonlarda consume ediliyordu. Purple match-cells yine ölmüyordu ama cascade 200 iter'a vurmadan konverj ediyordu. Step 2 mid-air match'i engelledi (doğru) → Purple bug'ı yüzeye çıktı → infinite loop.

### Fix

**1 dosya, atomik:** `Tests/PureLogic/BoardModelTests.cs:248-252` — `"ppp"` → `"yyy"`. Test gerçek matchable color (Yellow) kullanıyor; Purple "fantom" değil.

İlk yanlış denemem: `Matchable.cs`'e Purple ekledim — yanlış katman. Olmayan bir tile için damage logic. User uyardı: "purple matchable yok ki". Geri aldım.

### Step 2 plumbing — final hali

8 dosya, plan dahilinde:

- `IMovable.cs`: `bool IsMoving { get; set; }` (önceden boş marker)
- 5 implementer (`Matchable`, `Vase`, `Rocket`, `TNT`, `ColorBomb`): field + property
- `FallManager.FallIteration`: iter başında priming (tüm IMovable → IsMoving=false), her fiziksel relocation/spawn'da IsMoving=true
- `MatchManager.HorizontalMatchFinder` & `VerticalMatchFinder`: `if (tile is IMovable mv && mv.IsMoving) skip`

### Test

86/86 PureLogic geçiyor. CascadeTimingTests Step 1'i koruyor.

### Pre-existing tutarsızlıklar (cleanup önerisi — ayrı iş)

Production'da Purple yok ama 3 yerde referans var:

- `BoardModel.cs:456` — `GetRandomMatchableColor` array'ında Purple. **Bug:** ColorBomb + matchable combo bu fonksiyonu kullanıyor; Purple seçilirse `TileFactory.CreateTileFromType(Purple)` null + LogError döner.
- `PoolTypeMap.cs:13` — Purple → Matchable mapping. View pool'una bakıyor ama Purple tile asla üretilmiyor.
- `TileType` enum — Purple değeri kullanılmıyor.
- `TestBoardBuilder.cs:57` — `'p' → Purple` mapping. Test seam, gerçek production etkilemiyor ama yanıltıcı.

**Önerilen cleanup:** Purple'ı tamamen kaldır (4-color match-3 oyunu) VEYA TileFactory'ye 5. renk olarak ekle (Red/Green/Blue/Yellow/Purple, sprite + PoolType eklenir). Karar projenin tasarımına göre user'a ait.

Şimdi yapılmadı — Step 2 scope'u dışında, dokümante edildi.

### Süreç notu

İlk teşhisim filter'ı katman olarak yanlış kategorize etti, geri aldım — user "Match-3'ün altın kuralı" diye uyardı, doğru. Sonra Purple'a damage ekledim — user "purple matchable yok ki" diye uyardı, doğru. Üçüncü deneme test'in gerçek matchable kullanmasını sağladı.

**Ders:** Bir testin başarısızlığı için "production logic'i ekle" çekici çözüm gibi görünür ama önce **test'in gerçek state'i kullanıp kullanmadığını** doğrula. Bu test, oyunda olmayan bir tile'ı taklit eden bir test seam'i kullanıyordu.

---

## YENİ KEŞİF — 2026-04-28 (akşam) — Fall sırasında match leak (R1 regression)

**Status:** 🔴 **OPEN** — yarın ele alınacak.

**Severity:** yüksek (gameplay-breaking) · **Scope:** Phase-1 cascade · **Origin:** R1 refactor'ının yan etkisi

**User report:**
> "fall yanlış çalışıyor modelde sorun var. düşerken match olup olmadığı isMoving tile'lar için kontrol edilmemeli onun amacı buydu. şimdi her iterasyonda yaptığımız check taşlar settle olmadan matchlenmelerine sebep oluyor."

**Root cause:**
R1 (bu seans, yukarıda) `IMovable.IsMoving { get; set; }` flag'ini komple sildi. Eski semantik:
- `IsMoving = true` → "bu tile şu an düşüyor, match check'i skip et"
- `IsMoving = false` → "settled, match check'e dahil"

R1 **iki ayrı kavramı** birleştirip fazlasını sildi:
- "Capability — bu tip hareket edebilir mi?" (`tile is IMovable`) — doğru, marker'a bakmalı
- "State — bu iter'da düştü mü?" (`m_isMoving` field) — **yanlışlıkla silindi**

**Etki:** `BoardModel.ProcessCascade` (`:241-256`) her `FallIteration`'dan sonra `MatchManager.FindMatches(Middle, m_board)` çağırıyor. Artık "düşmekte" ile "settled" ayrımı olmadığı için, bir iter'da yeni inen ama henüz hedef pozisyonuna **animasyon olarak** ulaşmamış tile'lar match'e dahil oluyor. Görsel: tile'lar havada match'leniyor.

**R1'in çözmek istediği orijinal sorun (referans):**
> "iter N'de settle eden tile, iter N+1'de diagonal pull source olarak `IsTileMovable=false` döndüğü için skip ediliyordu."

Bu sorun gerçek — ama doğru fix `IsMoving`'ı silmek değil, `IsTileMovable()` capability check'inde **state'e bakmamak**tı. R1 ikisini birden yaptı, capability fix'i tutalım, state flag'i geri getirmek lazım.

**Önerilen yaklaşımlar (yarın değerlendirilecek):**

1. **`IsMoving` state flag'ini geri getir, `IsTileMovable`'dan ayır:**
   - `IMovable` interface'e `bool IsMoving { get; set; }` geri ekle
   - `NodeModel.IsTileMovable()` aynen kalır: `return tile is IMovable;` (state'e bakmıyor — R1'in çözdüğü kısım korundu)
   - `MatchManager.FindMatches` Middle iterasyonunda `if (tile is IMovable m && m.IsMoving) continue;` ekle
   - `FallIteration` her hareket eden tile'a `IsMoving = true`, hareket etmeyen IMovable'a `IsMoving = false` set etsin (tek iter sonu disipliniyle)
   - **Risk:** R1'in çözdüğü "iter sonrası priming" sorununun nüksetmesi → MatchManager'da kullan, FallManager'da kullanma (capability vs state ayrımı sıkı)

2. **Match check'i cascade-end'e ertele:**
   - `ProcessCascade` her iter sonrası match çağırmasın; `lastFallCount > 0` iken sadece düşmeye devam etsin
   - `lastFallCount == 0` (tüm tile'lar settle) → o zaman `FindMatches` çağrılsın → match olursa damage + yeni iter
   - **Risk:** Cascade chain match'leri (bir match patladıktan sonra düşen taşların yeni match yapması) için ek tur lazım — zaten safety counter ile dönüyor, ama semantik değişiyor
   - **Avantaj:** Flag-free; mimari daha sade

3. **Hibrit:** Match check her iter sonrası kalsın ama sadece `(x, y+1)` non-empty olan tile'lar dahil edilsin (yani tile altındaki dolu cell varsa "destek var, henüz düşmüyor" → match'e dahil)
   - **Risk:** Yan-fall ve diagonal-fall edge case'lerinde yanılır

**Yarın ilk iş:** `BoardModel.ProcessCascade` (line 221-260 civarı) ile `MatchManager.FindMatches` arasındaki sözleşmeyi okuyup hangi yaklaşım minimum invaziv olduğunu belirle. Test seam: `MatchManagerTests` + `FallManagerTests` zaten var, regression test ekle ("falling-but-not-yet-settled tile must not be in match").

**İlgili dosyalar:**
- `BoardModel.cs:221-260` — ProcessCascade
- `Managers/MatchManager.cs` — FindMatches body
- `Managers/FallManager.cs:22-40` — IsTileMovable kullanımı (R1 sonrası)
- `Models/Tiles/Matchable.cs`, `Vase.cs`, `Rocket.cs`, `TNT.cs`, `ColorBomb.cs` — IMovable implementer'lar (flag geri getirilirse hepsine field eklenir)

**Test stratejisi:**
- Yeni regression: 5×5 board, üst sıraya 1 tile düşürürken aynı sütunda 2 same-type tile dur. FallIteration tek iter'da tile bir aşağı iniyor; aynı renk hattına aday olduğu hücrede MatchManager onu match'lememeli.
- Mevcut R1 regression test (`FallIteration_DiagonalPullFromTileSettledInPriorIteration_StillFires`) korunacak — yaklaşım 1'de IsMoving sadece match'te kullanılırsa zaten geçer.

---

## Düzeltilen — 2026-04-28 (Rocket damage NRE fix)

A2 + A3 NRE'leri tek seansta düzeltildi. Test: 81 → 84 (3 yeni
regression). Detay: `DamagePatterns.DoubleRocket` /
`HorizontalRocketDamage` / `VerticalRocketDamage` — `toDamage.Invoke`
çağrıları `?.Invoke` ile null-safe yapıldı; `DoubleRocket`'ta result
discard'ı da düzeltildi (Top/Bottom layer komutları artık liste'ye
ekleniyor). Aşağıdaki "## A2" / "## A3" bölümlerine RESOLVED işareti
basıldı. Regression test'ler `DamagePatternsTests.cs`'te.

Yan iş: `Tests/PureLogic/DreamGamesCase.PureLogic.Tests.csproj`
restore edildi (kayıptı). Build artifact'lerinden (obj/dgspec.json,
FileListAbsolute.txt) reconstruct edildi: net8.0, NUnit 3.14,
NUnit3TestAdapter 4.5, MS Test SDK 17.10, Newtonsoft.Json 13.0.3,
UnityEngine refs Unity 6000.2.10f1 install path'inden,
Assets/Scripts'ten linked compile (MonoBehaviour bağımlı dosyalar
hariç tutuldu).

---

## Düzeltilen — 2026-04-28 (Fall engine + spawn pipeline)

Üç bağımsız bug/iyileştirme tek seansta atomic uygulandı. Test: 80 → 81 (yeni regression dahil), tümü yeşil.

### R1 — `IsMoving` flag kaldırıldı (cascade extra-iter bug)

**Status:** ✅ **RESOLVED**

**Sorun:** `FallIteration` her scan'de "settled" tile'ı `IsMoving=false`'a flip ediyordu. ProcessCascade bir kez `MarkAllMovableAsFalling()` ile priming yapıyordu ama iter'lar arası priming yoktu — iter N'de settle eden tile, iter N+1'de diagonal pull source olarak `IsTileMovable=false` döndüğü için skip ediliyordu. Final state safety=200 ile eninde sonunda doluyor ama gereksiz cmd ve iter atılıyor.

**Fix:**
- `IMovable` boş marker interface'e dönüştürüldü (`bool IsMoving { get; set; }` silindi)
- `Matchable`/`Vase`/`Rocket`/`TNT`/`ColorBomb` — `m_isMoving` field + property silindi
- `NodeModel.IsTileMovable()` → `return tile is IMovable;`
- `FallManager.Fall(...)` ölü wrapper silindi (BoardModel direkt FallIteration çağırıyordu)
- `FallIteration`'daki `else if(IsTileMovable) ... IsMoving=false` branch silindi
- Spawn loop'undaki `((IMovable)tile).IsMoving = true` silindi
- `BoardModel.MarkAllMovableAsFalling()` ve çağrısı silindi
- `TestBoardBuilder.MarkAllMovable` helper + 38 test caller silindi

**Regression test:** `FallManagerTests.FallIteration_DiagonalPullFromTileSettledInPriorIteration_StillFires` — Stone-based 4×4 layout, iter 1'de settle eden tile iter 2'de Stone üstünden FallLeft ile pull edilebilmeli.

---

### R2 — Right-to-left scan + side-fall obstacle gating

**Status:** ✅ **RESOLVED**

**Sorun 1 (gating):** Side-fall, `(x, y+1)` boş olduğunda da tetikleniyordu — tile'lar boş alan üzerinden yana akıyordu. Doğrusu: side-fall sadece `(x, y+1)` non-movable obstacle (Stone/Box) ise.

**Sorun 2 (scan order):** Soldan-sağa scan + right-diag-first kombinasyonu race yaratıyordu — `(x+1, y)` henüz taranmadan right-diag onun straight-fall hakkı olan tile'ı çalabilirdi. Asimetrik `(x+1, y).HasMiddle` defansif guard'ı bu yüzden vardı.

**Fix:** `FallManager.FallIteration` inner loop:
- Scan sağdan-sola: `x = W-1 → 0`. `(x+1, y)` zaten taranmış olur, race kalmaz, asimetrik guard silindi.
- Side-fall branch'ları `(x, y+1).HasMiddle` outer if'i altına alındı (gating).

**Test:** Mevcut diag testleri Stone-gated zaten (`FallIteration_DiagonalPullFromUpRight/UpLeft`) — yeni semantikle uyumlu, geçiyor.

---

### R3 — `Command.TileType` ile cascade-time spawn renk bug fix

**Status:** ✅ **RESOLVED**

**Sorun:** `BoardManager.HandleTileClicked`, `ProcessSwap` tüm cascade'i model'de bitiriyor sonra `ExecuteCommands(commands, _Model.GetTileTypeAt, null)` çağırıyordu. View Spawn cmd'leri için `tileTypeLookup(target, Middle)` ile **final state**'i sorguluyordu. Aynı sütundaki N spawn cmd'inin hepsi target=(x, h-1)'i sorguluyor → sadece son spawn'ın type'ı dönüyor → o sütundaki tüm spawn view'leri aynı sprite'ı alıyordu. User report: "yeni gelenlerin hepsi kırmızı bir Matchable".

**Fix:**
- `Command` struct'ına `TileType TileType` field'ı eklendi (constructor opt param, default `None` → backward-compatible).
- Üç spawn call site type taşıyor:
  - `FallManager.cs:54` (top-row refill) → `tile.TileType`
  - `BoardModel.cs:393` (ColorBomb conversion) → `triggerableType`
  - `BoardModel.cs:552` (match-merkezi special tile) → `specialType`
- `BoardView.HandleSpawn` `cmd.TileType` okuyor (lookup yerine).
- `BoardView.ExecuteCommands` ve `HandleSpawn` signature'larından `tileTypeLookup` parametresi silindi.
- `BoardManager.HandleTileClicked` `_Model.GetTileTypeAt` argümanı geçirmiyor.

**Not:** `BoardModel.GetTileTypeAt` test seam olarak duruyor; pure-logic testler ve ProcessSwap debug log'u kullanmaya devam ediyor.

---



Refactor sonrası (Command struct: `Position` → `StartPosition` + `TargetPosition`,
`CommandCombined` silindi, `Merge` enum eklendi) yapılan semantik review'da
tespit edilen **pre-existing** sorunlar. Hiçbiri refactor tarafından
introduce edilmedi — refactor öncesi de mevcuttular. Çoğu **Phase-3 scope**
(rocket / TNT / ColorBomb trigger'ları), şu anki test kapsamının dışında.

---

## A1 — CB+Triggerable Merge timing çakışması (DESCOPED)

**Status:** 🚫 **DESCOPED** — view phase'inde Merge implement
edilmeyecek; iki ITriggerable swap'ı şimdilik desteklenmiyor sayılır.
İlk problemde (NRE, visual bug, edge-case) `BoardModel.cs:77-113` merge
block'u **komple kesilecek**.

**Runtime detection:** `BoardModel.cs:78`'e `Debug.LogWarning` eklendi —
merge path hit edilirse console'da görünür. Test'lerde bu log
fail-trigger değil (Unity Debug; PureLogic test'lerinde sessiz).

**Eski içerik (referans için):**

**Severity (eski):** orta · **Scope:** Phase-3 · **Risk:** görsel bug, oynanışa
yansır

**Yer:** `Assets/Scripts/Models/BoardModel.cs:77-113` (merge block) +
`:313-391` (CB+Triggerable branch).

**Sorun:**
Merge block'ta her iki tile da `ITriggerable` ise `:98`'de
`Commands.Merge` (Start=pos1, Target=pos2, t=t1) emit ediliyor. Bu, View
açısından "iki tile'ın t1'de tek bir efektle birleşmesi" anlamına gelir.

Ama CB+Triggerable branch'inde `:322`'deki yorum şöyle:

> "Remove moved tile immediately (ColorBomb stays visible during spawn phase)"

Yani niyet: triggerable hemen kaybolacak, **CB ekranda kalacak** ki
spawn animasyonları boyunca görünür olsun. CB ancak `:364`'te
(`spawnTime` sonrasında, çok daha geç) ölmeli.

Ne olur:
- t=t1: Merge command CB+Trig'i fuse eder → View her iki visual'ı da
  yok eder.
- t=spawnTime (≫ t1): geç gelen `DestroySelf` at `colorBombPos` boşa
  düşer (zaten yok).

**Refactor'la ilişkisi:** Pre-refactor da aynıydı (önce 2× `DestroySelf`
at t1 emit ediliyordu, semantik aynı). Refactor sadece tek komuta
indirgedi.

**Önerilen yaklaşım:**
İki seçenek:
1. CB+Triggerable durumunda merge block, `Commands.Merge` emit etmesin,
   sadece slide command emit etsin; geri kalanı
   `ProcessTriggerCombination`'a bıraksın.
2. View tarafında `Merge` semantiği "Start tile yok ol, Target tile
   kalır" olarak tanımlansın — ama bu çoğu kombo (Rocket+Rocket,
   TNT+TNT, vb.) için yanlış olur.

(1) daha temiz. Phase-3 polish'inde değerlendirilmeli.

---

## A2 — DoubleRocket null-Invoke NRE (RESOLVED)

**Status:** ✅ **RESOLVED** — 2026-04-28.

`DamagePatterns.DoubleRocket`'taki `toDamage.Invoke(...)` çağrısı
`?.Invoke(...)` ile null-safe yapıldı. Ek olarak çağrının result'u
artık `list.AddRange` ile yakalanıyor (eskiden discard ediliyordu —
pre-existing pre-fix bug). Regression test:
`DamagePatternsTests.DoubleRocket_AtPositionWithNullMiddle_DoesNotThrow`.

**Eski içerik (referans için):**

**Severity:** yüksek (crash) · **Scope:** Phase-3 · **Risk:** Rocket+Rocket
combo'sunda exception

**Yer:** `Assets/Scripts/Models/DamagePatterns.cs:152-166`
(`DoubleRocket`).

```csharp
public static List<Command> DoubleRocket(Vector2Int position, ..., NodeModel[,] m_board, float timeStamp)
{
    ...
    Damage toDamage = m_board[position.x, position.y].DamageLayersWith(TileType.VerticalRocket, 1);
    toDamage.Invoke(position, damage, m_board, timeStamp);  // ← unsafe Invoke
    ...
}
```

**Çağrı zinciri:**
`BoardModel.ProcessTriggerCombination` Rocket+Rocket için
`specialDamage = DoubleRocket` set eder ve `:398-399`'da:

```csharp
ClearIfPresent(pos1, NodeLayer.Middle);   // pos1 Rocket — Middle null
ClearIfPresent(pos2, NodeLayer.Middle);   // pos2 Rocket — Middle null
var damageCommands = specialDamage.Invoke(pos2, ...);  // pos=pos2
```

`DoubleRocket(pos2, ...)` → `m_board[pos2].DamageLayersWith(...)` çağırır
ama `pos2.Middle` null. `DamageLayersWith` muhtemelen null döner →
`toDamage.Invoke(...)` **NullReferenceException**.

Diğer Rocket fonksiyonları (`RocketDamageRightCore` vb. `:54-110`)
`toDamage?.Invoke(...)` kullanıyor — DoubleRocket kullanmıyor.

**Önerilen düzeltme:** `toDamage?.Invoke(...)` veya null-check.
Tek satırlık fix.

---

## A3 — HorizontalRocket / VerticalRocket swap-trigger NRE riski (RESOLVED)

**Status:** ✅ **RESOLVED** — 2026-04-28.

Her iki damage pattern'inde `list.AddRange(toDamage.Invoke(...))`
çağrıları null-safe Invoke + null-check + AddRange formuna çevrildi
(zaten `RocketDamageRightCore` vb. core method'lar bu şekilde
yazılıydı — tutarlılık sağlandı). Regression testleri:
`DamagePatternsTests.HorizontalRocketDamage_AtPositionWithNullMiddle_DoesNotThrow`,
`DamagePatternsTests.VerticalRocketDamage_AtPositionWithNullMiddle_DoesNotThrow`.

**Eski içerik (referans için):**

**Severity:** yüksek (crash) · **Scope:** Phase-3 (normal rocket-via-swap
path — merge/CB değil) · **Risk:** swap'la trigger olan tek bir
rocket'te exception

**Yer:**
- `Assets/Scripts/Models/DamagePatterns.cs:114-126` (`HorizontalRocketDamage`)
- `Assets/Scripts/Models/DamagePatterns.cs:128-140` (`VerticalRocketDamage`)

```csharp
public static List<Command> HorizontalRocketDamage(...)
{
    ...
    Damage toDamage = m_board[position.x, position.y].DamageLayersWith(TileType.HorizontalRocket, 1);
    list.AddRange(toDamage.Invoke(position, damage, m_board, timeStamp));  // ← unsafe Invoke
    ...
}
```

**Çağrı zinciri:**
`BoardModel.CollectTriggerIfExists` `:456-467`'de **trigger'ı çağırmadan
önce** Middle layer'ı null'lar:

```csharp
private Damage CollectTriggerIfExists(Vector2Int pos)
{
    TileModel tile = m_board[pos.x, pos.y].GetLayer(NodeLayer.Middle);
    if (tile is ITriggerable triggerable)
    {
        m_board[pos.x, pos.y].SetLayer(NodeLayer.Middle, null);  // ← null'lanır
        return triggerable.GetTriggerEffect();
    }
    return null;
}
```

Sonra `:154-164`'te trigger çağrılır. `HorizontalRocketDamage(position=pos2)`
içinde `m_board[pos2.x, pos2.y].DamageLayersWith(...)` zaten null Middle'a
gider → null Damage → `toDamage.Invoke(...)` **NRE**.

A2 ile aynı pattern, farklı kombo path.

**Önerilen düzeltme:** `toDamage?.Invoke(...)`.
Veya: rocket'ın kendi tipinin damage'ını uygulamasına gerek yok zaten —
zaten konumdan kalkıyor; satır kaldırılabilir de.

---

## A9 — Match-explosion → Rocket self-trigger (RESOLVED)

**Status:** ✅ **RESOLVED** — yapısal olarak engelleniyor.
**Önceki severity:** kritik (StackOverflow) · **Scope:** Phase-3

**Çözüm yeri:** `Assets/Scripts/Models/Nodes/NodeModel.cs:64-69`
(`DamageLayerWith` ITriggerable branch).

```csharp
} else if(tile is ITriggerable triggerable)
{
    if (source != TileType.HorizontalRocket
     && source != TileType.VerticalRocket
     && source != TileType.TNT)
        return null;     // ← match-color (Red/Green/...) burada erken çıkıyor
    m_Layers[(int)layer] = null;
    return triggerable.GetTriggerEffect();
}
```

**Class hierarchy (doğrulandı):**
- `Rocket : TileModel, ITriggerable, IMovable` — IDamageable değil
- `TNT : TileModel, ITriggerable, IMovable` — IDamageable değil
- `ColorBomb : TileModel, ITriggerable, IMovable` — IDamageable değil

→ Rocket/TNT/CB IDamageable branch'ine girmez, ITriggerable branch'ine
girer. `CustomDamageMatchExplosion`'dan gelen source = `match.matchableType`
(Red/Green/Blue/Yellow/Purple) → `:66` filter geçemez → `return null` →
**Rocket trigger'lanmaz, splash damage almaz.**

**Stale referanslar:**
- `Tests/PureLogic/SwapTests.cs:11-16` yorumu A9'u "Blocker" olarak
  gösteriyordu; eski kod state'inden kalma. Phase-3 spawn test'leri
  artık A9 tarafından bloke edilmiyor (ama A2/A3 NRE'leri açık duruyor).
- Yorum güncellendi (A9 resolved işareti, A2+A3 yeni blocker'lar).

**Defense-in-depth seçeneği (uygulanmadı, kararla bırakıldı):**
`CustomDamageMatchExplosion` ApplyDamage closure'una redundant
`if (mid is ITriggerable) continue;` eklenebilirdi. Reddedildi: damage
policy NodeModel'da yaşamalı (separation of concerns), splash-site'a
guard koymak future drift'e yol açar ("biri zannediyor diğerinin
koruduğu" tuzağı).

---

## A8 — (boş kayıt)

`SwapTests.cs:15` "A9 + A8" diyor ama A8'in tanımı bu repoda hiçbir
yerde yok. Eski bir not, içeriği kayıp. Yeniden numaralandırılırsa
silinebilir; bu kayıt orphan referansı belgelemek için.

---

## Kapsam dışı / refactor'da bilinerek dokunulmadı

- `BoardManager.HandleTileClicked` adjacency check + `BoardModel.IsValidSwap`
  içinde de adjacency check var → **double-check**, ufak code-smell ama
  zarar yok. Phase-1 base.
- `ProcessCascade` safety counter 200, hard error log'la break — pratik
  bir guard. Rapor amaçlı not.
- `ColorBomb` ile ilgili Phase-3 path'leri (CB+Matchable, CB+CB,
  CB+Trig) test seam'lerinin dışında — mark-then-trigger guard'ı yok,
  potansiyel re-entrancy var ama A9 zinciriyle aynı root cause.

---

## Doğrulanmış invariant'lar (View geliştirilmeden önce güvenebilirsin)

### Triggered tile null'lama disiplini

**Yer:** `BoardModel.cs:456-467` (`CollectTriggerIfExists`),
`BoardModel.cs:398-399` (combo path), `NodeModel.cs:64-69` (chain path).

**Garanti:** Bir ITriggerable (Rocket/TNT/CB) trigger effect'i çağrılmadan
**ÖNCE** her yolda Middle layer null'lanır:

| Path | Null'lama yeri | Trigger çağrısı |
|---|---|---|
| Tek rocket swap-trigger | `:462 SetLayer(Middle, null)` | `:156 / :162 trigger.Invoke(...)` |
| Combo (Rocket+Rocket vb.) | `:398-399 ClearIfPresent` | `:402 specialDamage.Invoke(...)` |
| Chain (Rocket damages Rocket) | `NodeModel:67 m_Layers = null` | `:68 GetTriggerEffect()` (return) |

→ Trigger effect kendi cell'ini m_board'da null bulur. **Self-trigger
yapısal olarak imkansız.** Recursion derinliği O(special-tile-count),
StackOverflow değil — sonlu zincir.

---

## Refactor'la ilgili açık sorular (action item değil, gözlem)

- **`Merge` command view-side semantiği**: View bunu ne anlamıyla
  uygulayacak? "Start tile fly to Target, fuse there"? "Both destroyed,
  one effect at Target"? View phase başlamadan önce karara bağlanmalı.
  Bu semantik kararı A1'i çözer veya kalıcı kılar.
- **Static command convention**: `DestroySelf`, `TakeDamage`, `Trigger`
  artık `Start == Target == position` taşıyor. Convention ile pinned
  (test: `BoardModelTests.ProcessSwap_StaticCommands_HaveIdenticalStartAndTarget`).
  View tarafında bu invariant'a güvenebiliriz.
- **`Spawn` iki anlam**: top-row refill (`Start.y = h`) vs. in-place
  special spawn (`Start = Target = pos`). View bunları
  `Start.y == h` koşuluyla ayırt edebilir. Test:
  `SwapTests.ProcessSwap_AfterValidMatch_AllCommandPositionsAreInBounds`
  bu invariant'ı pinler.

---

## YARINA DEVAM — 2026-04-29 plan

### Öncelik 1 — Fall during motion match leak (yukarıdaki YENİ KEŞİF)

**Yarın ilk iş.** Fix yaklaşımları üstte listelendi (1, 2, 3). Karar
veriliyor → kod → regression test → commit.

### Öncelik 2 — Rocket implementasyonu (Phase-3)

Bu seansın sonunda gameplay netleşti, mimari plan kararlaştırıldı. Pool
starvation (B3) bugün düzeldi (`BoardPoolManager.actionOnRelease`'e
reparent satırı). A2/A3 NRE'leri de bugün kapandı. Roket için kod yazımı
**fall regression çözüldükten sonra** başlanacak (cascade güvenilir
olmadan trigger animasyonu doğrulanamaz).

**Gameplay (kararlaştırıldı):**
- Trigger: roket'e tıklama VEYA roket valid bir swap'a girmesi
- Roket-roket swap **invalid** sayılır (Merge descoped — `BoardModel.cs:77-113`
  block'u kesilecek, A1)
- Roket bulunduğu hücrede yok olur, yerine 2 yarım parça spawn
- Yatay roket → sol parça (sola uçar) + sağ parça (sağa uçar)
- Dikey roket → üst parça (yukarı) + alt parça (aşağı)
- Her parça arkasında particle trail bırakır
- Ekran sınırına varınca parça yok edilir, pool'a döner
- Parçalar board grid'inde yer kaplamaz; ama geçtikleri hücreleri
  damage'lar (model'de zaten cell-by-cell DestroySelf timestamp'li
  emit ediliyor — `DamagePatterns.HorizontalRocketDamage/VerticalRocketDamage`)

**Mimari (kararlaştırıldı, agent önerisinden trim edildi):**
- **Yeni Command yok.** Mevcut `Commands.Trigger` + `IAnimateTrigger.PlayTrigger`
  path'i kullanılacak. Roket parçaları **saf view-side spawn**.
- **Model değişikliği yok.** `DamagePatterns` zaten satır/sütun sweep
  command'lerini doğru emit ediyor.
- **Sweep timing:** Instant model + timestamped animation (mevcut sistem
  zaten böyle). Parça uçuş süresi = damage interval × hücre sayısı →
  parça hücreye varınca o cell'in DestroySelf'i ateşler (görsel senkron).

**Net iş listesi (yarınki sıra):**

1. **SpriteLibrary'ye 4 yeni label ekle** (kullanıcı kararı bekleniyor):
   - **Seçenek A:** `SpecialTile` kategorisine ek label'lar
     (`HorizontalRocket_PartLeft`, `HorizontalRocket_PartRight`,
     `VerticalRocket_PartTop`, `VerticalRocket_PartBottom`)
   - **Seçenek B:** Yeni kategori `RocketPart` + 4 label
   - Önerim: A (label sayısı az, kategori sayısı şişmez)

2. **`RocketPartView` prefab + script:**
   - `MonoBehaviour`, child `ParticleSystem` (trail particle)
   - `SpriteRenderer` runtime'da set (yön'e göre)
   - `Setup(Sprite, Direction, Pool)` + `FlyAndReturn(endPos, duration)`
   - DOTween Linear move; `OnComplete` → particle stop + pool return

3. **Effect pool (yeni, `BoardPoolManager`'dan ayrı):**
   - Sade tut: tek `RocketPart` prefab'ı, sprite runtime set
   - Particle prefab'ı RocketPart'ın **child'ı** (parça pool'a dönerken
     particle de onunla gelir — ayrı pool gereksiz)
   - Yeri: `Assets/Scripts/Managers/Pooling/EffectPoolManager.cs` (yeni)

4. **`RocketView` portu (B2 maddesi):**
   - Şu an `MonoBehaviour` stub — `TileView : IAnimateTrigger`'a port
   - `GetCategoryByType() => "SpecialTile"`, label kararına göre
     ToString eşlemesi (Horizontal_Rocket → HorizontalRocket cleanup
     **ayrı iş**, scope dışı)
   - `PlayTrigger`:
     - EffectPool'dan 2 parça çek (left/right ya da top/bottom)
     - Sprite'ları SpriteLibrary'den çek
     - Parçaların start pos'u = roketin world pos
     - End pos = ekran kenarı (board sınırından dışarıya kadar)
     - Duration = sweep cell count × FALL_TIME (ya da yeni `ROCKET_SWEEP_TIME` const)
     - Particle aktif
     - Sequence sonunda `ReturnToPool()` → pool'a döner

5. **Timing senkronu:**
   - `DamagePatterns`'ın emit ettiği DestroySelf timestamp'leri zaten
     hücre-bazlı offset taşıyor (mevcut)
   - PlayTrigger'da parça uçuş hızı bu offset'le aynı olacak şekilde set
   - Sonuç: parça `(x,y)` üstüne geldiği frame'de o cell'in
     DestroySelf'i ateşler

**Kararlar bekliyor (yarın user'dan istenecek):**
- Sprite library: Seçenek A mı B mi?
- ROCKET_SWEEP_TIME const ayrı mı, FALL_TIME'ı mı kullansın?
- Particle prefab: smoke mu, star mı, ikisi birden mi?
  (`Assets/Sprites/Rocket/Particles/` ikisi de var)

**Reddedilen agent önerileri (referans için, tekrar gündeme gelmesin):**
- ❌ Yeni `RocketPartFly` Command — model'i kirletir, gereksiz
- ❌ SpriteLibrary'yi yeniden kategorilemek (Vase ayrı kategori vb.) —
  scope dışı, asset YAML değişikliği
- ❌ Underscore label cleanup (`Horizontal_Rocket` → `HorizontalRocket`)
  — ayrı iş, roket için gerekli değil

### Öncelik 3 (opsiyonel, vakit kalırsa) — A1 merge block kesimi

`BoardModel.cs:77-113` merge block komple silinecek (Merge descoped).
Roket-roket swap'ı `IsValidSwap` filter'ında zaten elenir hâle gelmeli.
Bunu ya öncelik 2 öncesi ya sonrası halledelim — küçük, lokalize bir iş.
