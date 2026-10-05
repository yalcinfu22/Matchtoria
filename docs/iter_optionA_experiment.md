# Option A Experiment — Stagger Destroys + Parallel Fall

**Tarih:** 2026-05-04
**Durum:** Deneme bitti, REVERTED. Karar bekleniyor.
**Branş yok** (proje git repo değil) — değişiklikler `.option_a_backup/` içinden geri yüklendi.

## Soru

User: "birinci taş kaymaya başlayıp tam geldiği an yok olmalı, soldaki taş durur biraz bekler eventi tetiklendiğinde yok olur, ve aynı zamanda SENKRON bir şekilde fall commandi de çalışmalı."

İki yaklaşım vardı:
- **A** — current mimaride timestamp aritmetiğiyle stagger ekle
- **B** — Peak Games tarzı, model "settle-sonrası state" tutsun, view interpolate etsin (major refactor)

Bu deneme **A**'yı doğrulamak için yapıldı. B denenmedi (çok büyük scope).

## Ne yapıldı

### Değişiklik 1 — `Configs/GameConfig.cs`
```csharp
public const float MATCH_TRIGGER_STAGGER = 0.05f;
```

### Değişiklik 2 — `Models/BoardModel.cs::ProcessSwap` (Phase 7)
Match damage commandlerini emit ettikten sonra walk et: `triggerPos = pos2` (swap destination = clicked tile arrival). Bu pozisyondaki commands aynı timestamp'te kalıyor; diğer hücrelerin (chain destroys + adjacent damage) timestamp'leri `+= MATCH_TRIGGER_STAGGER` ile bumplanıyor.

```csharp
Vector2Int triggerPos = pos2;
for (int i = 0; i < matchCommands.Count; i++)
{
    Command c = matchCommands[i];
    int cx = Mathf.RoundToInt(c.StartPosition.x);
    int cy = Mathf.RoundToInt(c.StartPosition.y);
    if (cx != triggerPos.x || cy != triggerPos.y)
    {
        c.startTimeStamp += GameConfig.MATCH_TRIGGER_STAGGER;
        matchCommands[i] = c;
    }
}
```

Sonrasında `BumpTimeStampPast(allCommands)` zaten max emitted timestamp + 1ms'e advance ediyor → spawn ve cascade fall doğal olarak chain destroy'lardan sonra geliyor.

### Değişiklik 3 — `Tests/PureLogic/OptionAStaggerDiagnostic.cs` (yeni)
İki diagnostic test:
- `ThreeMatchSwap_DumpTimings` — 3-match swap timing dump + stagger assertion
- `FourMatchSwap_RocketSpawn_DumpTimings` — 4-match Rocket spawn timing dump

## Sonuçlar

### 3-match swap (pre `rrgr` → swap (3,0)↔(2,0) → match (0,0),(1,0),(2,0); pos2=(2,0)=trigger)

```
t=0.0000  Swap         (3,0)→(2,0)
t=0.1000  DestroySelf  (2,0) [TRIGGER — clicked tile arrival]
t=0.1500  DestroySelf  (0,0) [chain — STAGGER]
t=0.1500  DestroySelf  (1,0) [chain — STAGGER]
t=0.1510  Fall         (2,1)→(2,0)  [parallel with chain destroys]
t=0.1510  Fall         (1,1)→(1,0)
t=0.1510  Fall         (0,1)→(0,0)
t=0.1520  Spawn        top-row refill ×3
... cascade-2 (yeni match formed by fall):
t=0.2510  DestroySelf  cascade chain (3 cells, NO stagger — cascade has no trigger)
t=0.2520  Fall + Spawn refill
```

### 4-match Rocket spawn (pre `rrrbr` → swap (4,0)↔(3,0))

```
t=0.0000  Swap         (4,0)→(3,0)
t=0.1000  DestroySelf  (3,0) [TRIGGER — clicked tile arrival]
t=0.1500  DestroySelf  (0,0), (1,0), (2,0) [chain — STAGGER]
t=0.1510  Fall         x6 [parallel with chain destroys]
t=0.1510  Spawn        HorizontalRocket at (0,0)  ← special spawn
t=0.1520  Spawn        top-row refill ×3
```

Rocket spawn position match shape'inden geliyor (`MatchManager` belirliyor); benim değişikliğim trigger position kavramından bağımsız çalışıyor.

## Test sonuçları

| Senaryo | Test count | Sonuç |
|---|---|---|
| Baseline (Option A öncesi) | 119 | All green |
| Option A uygulanmış | 120 (119 + diagnostic) | All green |
| Revert sonrası | 119 | All green |

**Sıfır regresyon.** Mevcut testlerin hiçbirisi exact destroy timestamp assert etmiyormuş — invariant testi (`CascadeTimingTests`) Fall→DestroySelf ordering'i test ediyor, exact değer değil. Stagger bu invariantı bozmuyor (trigger destroy 0.1, fall 0.151 → DestroySelf >= Fall start + FALL_TIME = 0.251 invariant'i sadece cascade match destroys için relevant ve onlar zaten 0.251'de doğru pozisyonda).

## Mimari etkisi

- **Model** kırılmadı: yalnızca timestamp post-process loop eklendi (~10 satır)
- **View** sıfır değişti: 4-phase batch zaten `Sequence.Insert` ile parallel commands at same/diff timestamp'i handle ediyor
- **DamagePatterns** sıfır değişti: stagger logic ProcessSwap-level'de uygulandı, generic damage delegate kontrat'ı korundu

## Hangi senaryolara DOKUNMADI

- **Cascade match'leri** — fall sonrası oluşan match'ler stagger almıyor (trigger position kavramı yok)
- **Trigger swap (rocket-into-color, TNT-into-color)** — Phase 6 trigger commands ayrı path, etkilenmedi
- **Combo (rocket+rocket, rocket+TNT, CB+CB, CB+matchable)** — `ProcessTriggerCombination` farklı path, etkilenmedi
- **TRIGGER_FALL_GAP=0.3** — special swap'larda hâlâ var, dokunulmadı

## Sınırlamalar / Open questions

1. **Çift-match swap:** swap hem pos1 hem pos2'de match yaratırsa, sadece pos2 trigger. pos1'deki match (varsa) chain stagger alır. Royal Match'te bu nadir bir case — şimdilik OK.

2. **STAGGER tuning:** 0.05f hardcoded. Royal Match reference videolarına bakarak 0.04-0.08 arası ince ayar gerekebilir. Constant izole olduğundan trivial.

3. **Spawn at trigger position:** 4-match pos2'yi içeriyorsa ve spawn position == pos2 ise: trigger destroy 0.1, spawn 0.151 — 51ms "boşluk." Visual'da eğer kötü görünürse spawn timing'i ayrı düşünülebilir. Test edilmedi (RNG ile spawn position'ı zorlamak gerekir).

4. **CascadeTimingTests** invariant'ı korundu — geçti ama edge case'lerde (örn. trigger destroy hücresine 0.151'de fall geliyor, destroy 0.1'de oldu) timing güvende.

## Tavsiye

A yaklaşımı **viable**:
- Tek dosyada ~15 satır değişiklik
- Sıfır test regresyonu
- Visual choreography istenen davranışa uyuyor (timestamp dump ile doğrulandı)
- Mimari riski yok

User'ın karar vermesi gerekenler:
- STAGGER değeri (default 0.05f mı?)
- Spawn at trigger-position case'inin gerçek runtime'da test edilmesi
- Cascade match'lerine de stagger uygulansın mı? (önerim: hayır)

## Revert kanıtı

```
.option_a_backup/BoardModel.cs.bak  → Assets/Scripts/Models/BoardModel.cs
.option_a_backup/GameConfig.cs.bak  → Assets/Scripts/Configs/GameConfig.cs
Tests/PureLogic/OptionAStaggerDiagnostic.cs → silindi
```

Tests baseline (119) yeşil → revert temiz.
