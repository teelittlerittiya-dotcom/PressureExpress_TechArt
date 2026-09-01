# Feature Plan 04 — Cargo Polish & Game Feel

> **Status:** In Progress — Phase A–D implement แล้ว; รอ PlayMode/Multiplayer และ Feature 02 integration
> **Priority:** เริ่ม core polish ได้บน Cargo/Holding prototype ปัจจุบัน; ปิดงานหลัง remote validation และ Sprite Ordering integration
> **Hard dependency:** Feature 01 prototype state/event contract และ local-hover contract ที่ implement ใน Feature 03
> **Integration dependency:** Feature 02 — Sprite Render Ordering; ไม่ block data/event/material pilot แต่ block final VFX ordering/visual acceptance
> **Last updated:** 2026-08-26

## คำสั่งสำหรับผู้พัฒนา/AI

- เอกสารนี้เป็นแผนเท่านั้น ห้ามเริ่มแก้ code, prefab, material หรือ asset จนกว่าแผนจะได้รับการยืนยัน
- รักษา server-authoritative gameplay เดิม Presentation มีหน้าที่แสดงผลจาก state/event เท่านั้น
- ใช้ Cargo prefab ร่วมกันหนึ่งตัวเป็นค่าเริ่มต้น ห้ามสร้าง prefab แยกต่อ Cargo เพื่อเปลี่ยนแค่เสียง, particle, FEEL หรือ material
- ห้ามให้ `CargoController` ผูกกับ API ของ FEEL หรือ All In 1 Sprite Shader โดยตรง
- รักษา stage visual contract ปัจจุบัน: Impact/Pressure เป็น sprite channel และ Temperature/Freshness เป็น particle-overlay channel ห้ามสร้าง threshold/sprite/particle source ซ้ำใน polish profile
- รักษา `UIAnchor` เป็น sibling ของ `VisualRoot` เพื่อไม่ให้ Status UI รับ `cargoScale` หรือ FEEL transform
- Feature 02 ยังไม่ implement: ห้ามกระจาย sorting order/Z workaround ใหม่ งานรอบนี้ต้องใช้ hierarchy เดิมและบันทึก VFX ordering เป็น integration gate
- ห้ามแก้ source shader ภายใน third-party All In 1 package เพื่อทำ feature นี้ ให้สร้าง material preset และ runtime property driver ภายนอก package
- หลังเริ่ม implementation ต้องอัปเดต checklist และ Implementation Record ในเอกสารนี้ทุกครั้ง

## Master Checklist

- [x] ยืนยัน architecture และขอบเขต v1
- [x] กำหนด presentation event contract จาก owner ของแต่ละ semantic event (state/hover/impact; holder cue ยังเป็น optional follow-up)
- [x] สร้าง `CargoPolishProfile` และ validation ขั้นพื้นฐาน
- [x] สร้าง `CargoPolishController` เป็น presentation orchestrator เพียงจุดเดียว
- [x] ทำ material instance control ด้วย `MaterialPropertyBlock`
- [x] ต่อ local hover highlight โดยไม่ส่ง network
- [x] ต่อ status material จาก replicated runtime state โดยไม่ทำ stage threshold ซ้ำ
- [x] ย้าย routing ของ stage sprite/loop particle เข้า presentation orchestrator โดยรักษา `CargoModule.VisualState` และ channel contract เดิม
- [x] ต่อ impact FEEL, one-shot particle และ spatial SFX จาก semantic impact event
- [x] ทำ FEEL template workflow ที่ designer แก้ได้จาก prefab template
- [x] ทดลองกับ Cargo สองแบบที่ต่างกันชัดเจน: Eggs/Soft และ Explosive/Nuke
- [ ] ทดสอบ Host + Remote Client + Late Join
- [x] ย้ายค่า hardcoded/dragged references เดิมออกหลัง pilot ผ่าน
- [x] เพิ่ม validator และเอกสาร designer workflow
- [ ] เชื่อม VFX sorting ตาม Feature 02 ก่อนปิด feature

---

## 1. เป้าหมาย

ทำให้ Cargo ทุกชนิดใช้ shared Cargo prefab เดียว แต่เปลี่ยน polish ตาม data ที่ init เข้ามาได้ เช่น:

- Eggs: impact แล้วเด้งนุ่ม, มีฝุ่น/ปุยนุ่ม และเสียงเบา
- Explosive cargo: ไม่เด้ง, มีสะเก็ดไฟ และเสียงโลหะ/ระเบิด
- Cargo ร้อน: เพิ่ม round-wave strength ตามระดับความร้อน
- Cargo เย็น: เพิ่ม hand-drawn/jitter look ตามระดับความเย็น
- Cargo ที่ผู้เล่นชี้อยู่: เปิด pixel highlight เฉพาะ client ของผู้เล่นคนนั้น

Designer ควรแก้ Cargo presentation จาก `CargoItemData` ที่ลิงก์ไป `CargoPolishProfile` เพียงทางเข้าเดียว และเปิด FEEL template จาก profile เมื่อต้องแก้ sequence ละเอียด

### Success criteria

- Cargo ต่างชนิดกันใช้ prefab เดียวกันได้จริง
- ไม่มีการลาก impact SFX/particle แยกลง Cargo prefab ต่อชนิด
- ไม่มีการแก้ shared material runtime จน Cargo ทุกชิ้นเปลี่ยนพร้อมกัน
- hover เป็น local-only และไม่สร้าง network traffic
- persistent status ถูก restore ถูกต้องเมื่อ late join โดยไม่ replay one-shot feedback
- designer หาและแก้ presentation ของ Cargo หนึ่งชนิดได้โดยเริ่มจาก asset เดียว

## 2. สิ่งที่ไม่ทำใน v1

- ไม่สร้าง visual scripting/event graph ใหม่มาแข่งกับ FEEL
- ไม่สร้าง generic rule engine ที่แก้ shader property ใดก็ได้ด้วย string
- ไม่ทำ profile inheritance/base-override ตั้งแต่รอบแรก
- ไม่สร้าง custom Editor Window ก่อน workflow ปกติพิสูจน์ว่าใช้ไม่ได้
- ไม่ย้าย gameplay state, damage หรือ network authority เข้า polish layer
- ไม่สร้าง Cargo prefab variant ต่อชนิดสำหรับความต่างที่เป็น data เท่านั้น

## 3. สภาพปัจจุบันที่ต้องคำนึงถึง

- `CargoController` มี replicated `CargoRuntimeState` และ `RuntimeStateChanged` อยู่แล้ว
- `CargoGrabController` เป็นผู้คำนวณ local pointer hover จาก ray + `initialGrabRange` แล้วเรียก `CargoController.SetLocalPointerHover`; ตำแหน่ง Actual Hand ไม่ใช่ hover source
- `[DEBUG] Cargo Debug Mode` gate เฉพาะ Status UI เท่านั้น ค่า local hover ยังคงมีอยู่แม้ Debug Mode ปิด
- impact ถูกตัดสินฝั่ง server และส่ง semantic result มาทาง ClientRpc
- impact feedback ปัจจุบันเล่นจาก physical collision ก่อน damage check ดังนั้นยังเล่นได้ระหว่าง invincibility 3 วินาทีแม้ Impact state ไม่ลด
- impact SFX ยังเป็น serialized field ใน `CargoController`
- `ParticleManager` ยังพึ่ง dragged `defaultImpactVFX` และจัดการ status loop เอง
- stage visual ปัจจุบันมี regression contract แล้ว: `CargoModule.VisualState` เป็น source ของ stage threshold; Impact/Pressure เลือก sprite ส่วน Temperature/Freshness เลือก loop particle
- Cargo renderer ใช้ material ร่วมกัน การแก้ material asset หรือ `sharedMaterial` จึงกระทบทุก renderer ที่อ้าง asset เดียวกัน
- FEEL ถูกใช้ใน project อยู่แล้ว และ `MMF_Player` รองรับ prefab/template, runtime copy และ `MMF_ReferenceHolder`
- `SpatialAudioManager` มี logic ระยะ, ห้อง/ประตู และ underwater ที่ `MMF_Sound` ไม่ได้ผ่านโดยอัตโนมัติ

ข้อสรุป: ควรต่อยอด event/state เดิม และรวบ routing ของ presentation ไว้จุดเดียว ไม่ควรสร้าง network state หรือ event bus ชุดใหม่

## 4. Architecture เป้าหมาย

```text
CargoItemData
  ├─ CargoModule[]
  │    └─ VisualState[]          stage threshold + sprite/loop particle เดิม
  └─ CargoPolishProfile
       ├─ Material preset + typed response curves
       ├─ Hover settings
       ├─ Event cues: Impact / optional Pickup / Release
       ├─ FEEL template references
       └─ Spatial SFX cues

Shared Cargo Prefab
  ├─ CargoController            gameplay + network authority
  ├─ CargoPolishController      presentation orchestrator
  ├─ ParticleManager            internal runtime helper; designer ไม่ต้องลากค่า
  ├─ VisualRoot                 รับ persistent cargoScale ตาม Feature 01
  │    ├─ FeedbackRoot          รับ visual-only squash/animation; ห้ามมี Collider/Rigidbody
  │    │    └─ SpriteRenderer
  │    └─ VFXAnchor             รับ cargoScale แต่ไม่รับ transient FEEL transform
  ├─ ProximityTrigger
  ├─ GeneratedColliders
  └─ UIAnchor                   sibling ของ VisualRoot; scale/rotation independent
```

### หน้าที่ของแต่ละส่วน

`CargoController`

- ดูแล gameplay, replicated state และ semantic events
- ไม่รู้จัก `MMF_Player`, shader property หรือ particle prefab
- เปิด event/API ที่ presentation layer ต้องใช้เท่านั้น

`CargoPolishController`

- เป็น facade จุดเดียวของ Cargo presentation
- subscribe state/event จาก `CargoController`
- resolve `CargoPolishProfile` จาก `CargoItemData` ตอน init
- route งานไป stage Sprite, Sound, Particle, FEEL และ Material โดยอ่าน stage จาก `CargoModule.VisualState` เดิม
- cache runtime objects และ reset ทุกอย่างเมื่อ disable/despawn/reinitialize
- ใช้ helper class ภายในได้ แต่ไม่ควรแตกเป็น MonoBehaviour manager 4 ตัวโดยไม่มีเหตุผล

`CargoPolishProfile`

- เป็น ScriptableObject สำหรับเลือกและปรับ presentation ของ Cargo
- reuse ร่วมกันได้ เช่น `SoftCargoPolish`, `ExplosiveCargoPolish`, `FragileCargoPolish`
- Cargo ที่ต้องต่างจริงจึง duplicate profile แล้วปรับค่า
- ไม่เก็บ runtime state หรือ network state
- ไม่ทำสำเนา stage threshold, stage sprite หรือ status loop particle ที่ module มีอยู่แล้ว

## 5. Data model ที่เสนอ

ตัวอย่างโครงสร้างเชิงแนวคิด ไม่ใช่ API ที่ล็อกแล้ว:

```csharp
CargoItemData
  CargoPolishProfile polishProfile;

CargoPolishProfile
  Material spriteMaterialPreset;
  CargoHoverPolish hover;
  CargoStatusMaterialPolish statusMaterial;
  CargoEventPolish impact;
  CargoEventPolish pickup;
  CargoEventPolish release;

CargoEventPolish
  MMF_Player feelTemplate;
  CargoSpatialSfxCue spatialSfx;
  AnimationCurve intensityRemap;

CargoStatusMaterialPolish
  AnimationCurve heatRoundWaveStrength;
  AnimationCurve coldHandDrawnAmount;
  // เพิ่มเฉพาะ typed response ที่มี use case จริง
```

### Stage visual source of truth

- `CargoModule.VisualState` และ Temperature hot/cold lists ยังคงเป็นผู้เลือก stage ตาม threshold
- รักษา priority/channel ที่มี regression test แล้ว: Impact ก่อน Pressure สำหรับ sprite; Temperature และ Freshness เป็น loop particle overlays
- `CargoPolishController` รับผิดชอบการ apply แต่ไม่คัดลอก threshold/sprite/particle เข้า `CargoPolishProfile`
- material response อ่านค่าปัจจุบันของ module แล้วแปลงเป็น normalized input/curve จึงไม่ต้องสร้าง stage ranges รอบสอง
- หากภายหลังต้องมี FEEL เฉพาะตอนเข้า stage ให้เพิ่ม stable resolved-stage contract จาก module ก่อน ห้าม map ด้วย list index หรือสร้าง threshold ซ้ำใน profile

### Material composition v1

รองรับเฉพาะ effect ที่ Cargo ใช้จริงก่อน:

- Hover outline/glow: local layer
- Heat round wave: strength curve ตามค่า hot-normalized จาก `idealTemp` ถึง `maxTemp`
- Cold hand-drawn: amount curve ตามค่า cold-normalized จาก `idealTemp` ถึง `minTemp`
- Impact hit flash/pulse: transient layer

ค่าที่ชน property เดียวกันให้ระบุ policy ใน code แบบ typed เช่น `Max`, `AddClamped` หรือ `HighestPriority` ต่อ property ไม่เปิดให้ designer พิมพ์ชื่อ shader property เอง

## 6. Presentation event contract

| Event/State | แหล่งข้อมูล | Network policy | ผลลัพธ์ |
|---|---|---|---|
| Runtime state changed | Replicated `CargoRuntimeState` | ไม่ส่ง VFX RPC เพิ่ม | stage sprite/loop particle เดิม + status material |
| Local hover changed | `CargoGrabController` pointer ray + reach | local-only | outline/glow เฉพาะผู้เล่นที่ hover |
| Impact | server decision + ClientRpc | เล่นหนึ่งครั้งต่อ event | FEEL, one-shot particle, spatial SFX, hit pulse |
| Holder changed | authoritative holder state | derive จาก state ที่ sync แล้ว | pickup/release cue |
| Spawn/Late join | current replicated state | ห้าม replay historical event | restore persistent look และ loops เท่านั้น |

ถ้า event ใดยังไม่มี public contract ที่สะอาด ให้เพิ่ม typed C# event ที่ owner ของ semantic นั้น: state/impact/hover อยู่ฝั่ง Cargo ส่วน holder transition อยู่ฝั่ง holding system ห้ามยัดทุก event เข้า `CargoController`, polling private fields หรือสร้าง global event bus ใหม่

## 7. แผนของแต่ละ channel

### 7.1 Sound

แนวทาง v1:

- เก็บ clip set, volume และ intensity curve ใน `CargoPolishProfile`
- ให้ `CargoPolishController` เรียก `SpatialAudioManager` เพื่อรักษา room/door/underwater behavior เดิม
- random clip เกิดฝั่ง client ได้ ไม่ต้อง network clip index หากเสียงไม่กระทบ gameplay
- ไม่เพิ่ม `AudioSource` ที่ designer ต้องตั้งค่าต่อ Cargo

`SpatialAudioManager.PlaySFXAtPosition` ปัจจุบันรองรับ clip/position/volume แต่ยังไม่รองรับ pitch และยังสร้าง temporary GameObject ต่อเสียง จึงไม่เพิ่ม pitch field ที่ใช้จริงไม่ได้ใน v1 หาก profiler พบ allocation สูง ให้แก้ pooling ที่ spatial-audio service กลาง ไม่สร้าง pool เฉพาะ Cargo

ยังไม่ใช้ `MMF_Sound` กับ world Cargo เพราะจะข้าม audio policy ของ project หากภายหลัง designer ต้องวาง timing เสียงไว้กลาง FEEL timeline จริง ค่อยสร้าง FEEL feedback bridge ขนาดเล็ก เช่น `MMF_CargoSpatialSfx` ที่เรียก `SpatialAudioManager`

### 7.2 Particles

- one-shot เช่น impact poof/sparks ใช้ `MMF_ParticlesInstantiation` ใน FEEL template
- persistent loop เช่น ไอร้อน/ไอเย็นยังเลือก prefab จาก `CargoModule.VisualState` และใช้ lifecycle owner ใน Cargo polish layer
- reuse pooling ที่มีอยู่ อย่าสร้าง pool ซ้อนกันสำหรับ particle channel เดียว
- เปลี่ยน `ParticleManager` ให้เป็น internal helper สำหรับ status loops; ย้ายเฉพาะ serialized `defaultImpactVFX` ไป FEEL impact template
- `VFXAnchor` แยกจาก `FeedbackRoot` เป็นค่าเริ่มต้น เพื่อไม่ให้ particle ถูก squash/stretch ตาม sprite
- อนุญาตให้ cue ระบุ parent เป็น `FeedbackRoot` เฉพาะ effect ที่ตั้งใจให้ deform ไปกับ Cargo

### 7.3 FEEL / MMF Feedbacks

- ใช้ `MMF_Player` prefab เป็น reusable feedback template เช่น `SoftImpact`, `ExplosiveImpact`, `FragilePickup`
- profile เลือก template ที่ต้องใช้ Designer เปิดแก้ sequence ด้วย FEEL inspector เดิม
- ตอน init หรือ first-use ให้ instantiate/cache template ใต้ runtime feedback container
- ใช้ `MMF_ReferenceHolder` ชี้ target ไป `FeedbackRoot` แล้ว initialize หนึ่งครั้ง
- เรียก `PlayFeedbacks(position, normalizedIntensity)` เพื่อให้ impact strength ขับความแรงของ effect
- frequent cue เช่น impact อาจ warm ตอน init; cue อื่น lazy-load แล้ว cache ได้
- ต้อง stop/reset player และคืน pooled objects เมื่อ Cargo despawn หรือเปลี่ยน data
- world-Cargo FEEL template ห้ามใส่ `MMF_Sound` โดยตรง และ persistent material state ห้ามใช้ `MMF_MaterialSetProperty`; validator ต้องเตือนให้ route ผ่าน spatial SFX cue/material composer
- common impact squash ใช้ค่าจาก `CargoPolishProfile` และขยับเฉพาะ `FeedbackRoot` จาก authored scale ทุกครั้ง เพื่อไม่ให้การโดนซ้ำสะสมสเกล; FEEL template ห้ามใส่ `MMF_SquashAndStretch` ซ้ำ

ไม่ควร copy/เขียนทับ feedback list ของ player ตัวเดียวขณะที่มันกำลังเล่น และไม่ควรพยายาม serialize graph ของ FEEL ลง custom ScriptableObject เพราะ FEEL ใช้ component/prefab เป็น authoring container อยู่แล้ว

### 7.4 All In 1 Sprite Shader

- สร้าง material preset เป็น `.mat` asset ผ่าน All In 1 inspector/tool ของ package แล้วอ้างจาก profile ไม่ต้อง copy SpriteRenderer/GameObject เพื่อสร้าง material
- duplicate material preset เฉพาะเมื่อชุด shader keywords ต่างกันจริง; ความต่างที่เป็นตัวเลขต่อ Cargo ใช้ property block ไม่สร้าง `.mat` ต่อ Cargo
- profile อ้าง `Material preset` ที่เปิด shader keywords ที่จำเป็นไว้แล้วใน Editor
- assign preset ให้ renderer ตอน init ได้ แต่ห้ามแก้ค่าบน asset นั้น runtime
- ค่าที่ต่างต่อ Cargo instance ใช้ `MaterialPropertyBlock`
- cache property IDs และรวม hover/status/transient values ก่อน set block
- All In 1 `SpritePropertiesSync` เขียน `_SpriteFlip` ผ่าน property block ใน `LateUpdate`; material driver ต้อง `GetPropertyBlock` แล้ว merge ก่อน `SetPropertyBlock` และทดสอบ script order/flip เพื่อไม่ล้างค่ากัน
- effect ที่ inactive ใช้ strength/amount เป็น `0` แทนการเปิดปิด keyword runtime
- ห้ามใช้ `renderer.material` ใน update loop เพราะจะสร้าง material instance โดยไม่ตั้งใจ
- ห้ามใช้ `sharedMaterial.SetFloat/SetColor` เพราะจะเปลี่ยน Cargo ทุกชิ้นที่ใช้ material เดียวกัน
- ไม่ใช้ `MMF_MaterialSetProperty` สำหรับ persistent Cargo state เพราะมันทำงานผ่าน material instance และอาจชนกับ material driver

ตัวอย่าง material preset สำหรับ profile ที่ต้องมีทั้งร้อนและเย็น สามารถเปิด `ROUNDWAVEUV_ON` และ `DOODLE_ON` ไว้ล่วงหน้า แล้วควบคุม `_RoundWaveStrength` กับ `_HandDrawnAmount` ราย renderer ผ่าน property block

## 8. Hover highlight แบบ local

Flow ที่ต้องการ:

```text
Local CargoGrabController
  -> pointer ray hit Cargo และ CursorIntentProvider.IsPointerWithinInteractionReach
  -> CargoController.SetLocalPointerHover(bool)
  -> LocalHoverChanged event
  -> CargoPolishController
  -> MaterialPropertyBlock: outline/glow amount
```

- ห้ามใช้ `NetworkVariable` หรือ RPC สำหรับ hover
- Cargo ชิ้นเดียวกันจึง highlight บน client A ได้ โดย client B ไม่เห็น
- highlight ใช้ค่า hover เดียวกับ Hand Preview/Status UI reach policy แต่ไม่ถูก gate ด้วย `[DEBUG] Cargo Debug Mode`; Debug Mode คุมเฉพาะ panel
- ค่า color, width และ intensity อยู่ใน profile
- ต้อง compose กับ heat/cold/hit values โดยไม่ล้าง property block ของ status

## 9. Designer workflow

### สร้าง Cargo ใหม่ด้วย style ที่มีอยู่

1. สร้าง/แก้ `CargoItemData`
2. เลือก `CargoPolishProfile` เช่น Soft หรือ Explosive
3. ตั้ง gameplay modules และ sprite/collider ตาม workflow Cargo เดิม
4. รัน validator/preview

ไม่ต้องสร้าง Cargo prefab และไม่ต้องลาก sound/particle ลง scene object

### ปรับ Cargo เฉพาะตัว

1. duplicate profile ที่ใกล้ที่สุด
2. เปลี่ยน SFX cue, FEEL template หรือ material response values
3. assign profile ใหม่กลับเข้า `CargoItemData`

### ปรับ feedback sequence

1. เปิด FEEL template จาก field ใน profile
2. แก้ particle, shake หรือ timing เฉพาะ Cargo ด้วย FEEL inspector
3. แก้ common impact squash ที่ `Impact Visual Squash` ใน profile เพื่อให้ threshold/cleanup/collider isolation ใช้ contract เดียวกัน
4. preview template และทดสอบผ่าน Cargo sandbox

ดังนั้น designer เริ่มจาก `CargoItemData` จุดเดียว: module assets เดิมกำหนด gameplay + stage sprite/particle และ `CargoPolishProfile` กำหนด material/event feel จากนั้นจึงเปิด FEEL template เฉพาะเมื่อต้องแก้ sequence ละเอียด วิธีนี้ไม่ทำ data เดิมซ้ำและไม่ฝืนยัด FEEL graph ลง SO

## 10. Base + override policy

v1 ใช้การ share profile และ duplicate เมื่อจำเป็น ยังไม่ทำ inheritance เพราะ:

- Unity serialized inheritance แกะยากเมื่อ field เพิ่ม/เปลี่ยน
- designer มองไม่ชัดว่าค่าสุดท้ายมาจาก base หรือ override
- merge/list override มักต้องสร้าง custom editor และ validation เพิ่มมาก

พิจารณาเพิ่ม base/override ภายหลังเมื่อมีข้อมูลจริงว่าหลาย profile ซ้ำกันมาก และต้องมีอย่างน้อย 3 use cases ที่การ duplicate ทำให้ maintenance มีปัญหา

สิ่งที่ควร reuse ตั้งแต่ v1 คือ FEEL templates, SFX cue assets/material presets หากพบว่าถูกแชร์จริง ไม่ใช่ inheritance ของทั้ง Cargo profile

## 11. Lifecycle และ Multiplayer rules

- persistent look คำนวณจาก state ปัจจุบันเสมอ จึงรองรับ late join
- ตอน subscribe/init ต้อง apply current state ทันทีด้วย path เดียวกับ state-change event เพื่อรองรับกรณี replicated state มาก่อน presentation component พร้อม
- one-shot feedback เล่นจาก event เท่านั้นและไม่บันทึกเป็น state
- local hover ถูก clear ตอน cursor ออก, disable, despawn และเปลี่ยน target
- v1 ยังไม่เพิ่ม status enter/exit FEEL; หากเพิ่มภายหลังต้องใช้ resolved stage เดิมและมี hysteresis/cooldown
- impact strength normalize เพียงครั้งเดียวก่อนส่งเข้า FEEL/SFX curves
- physical impact FEEL ยังเล่นได้ระหว่าง Cargo invincibility; invincibility block damage/state change ไม่ใช่ collision feedback
- presentation failure ห้ามทำให้ gameplay/network logic ล้ม
- missing profile ใน development ให้ validator/error ชัดเจน ไม่ใช้ runtime fallback เงียบๆ จน asset ที่ตั้งค่าผิดหลุดไป production
- dedicated server/non-client path ต้องไม่ initialize, instantiate หรือ play presentation runtime; ไม่กำหนดว่า asset จะไม่ถูก include/load ใน build เว้นแต่มี dedicated asset-loading strategy แยก

## 12. Implementation phases

### Phase A — Contract และ data skeleton

- เพิ่ม `CargoPolishProfile` และ typed serializable cue/settings
- เพิ่ม field อ้าง profile ใน `CargoItemData`
- เพิ่ม Neutral/Default profile และ editor migration เพื่อ assign Cargo data เดิมทั้งหมดในครั้งเดียว ห้ามเปิด runtime fallback เงียบๆ
- กำหนด typed events สำหรับ local hover, impact และ optional holder transition ที่ owner ของ event เท่าที่ขาด
- เพิ่ม validator สำหรับ profile/material/FEEL/particle references

### Phase B — Shared prefab runtime

- เพิ่ม `CargoPolishController` ใน shared Cargo prefab
- แทรก `FeedbackRoot` ใต้ `VisualRoot`, คง `VFXAnchor` ใต้ `VisualRoot` และคง `UIAnchor` เป็น root sibling
- ต่อ profile initialization, cleanup และ cached FEEL templates
- ทำ MaterialPropertyBlock composer
- ย้ายการ apply stage sprite/loop particle ออกจาก gameplay controller โดยยังอ่าน `CargoModule.VisualState` และรักษา channel behavior เดิม

### Phase C — Pilot สองบุคลิก

- Eggs/Soft: bounce + soft particle + soft SFX + local hover
- Explosive/Nuke: no bounce + sparks + explosive/metal SFX
- ทั้งสองใช้ shared Cargo prefab เดียว
- เพิ่ม heat/cold material state อย่างน้อยหนึ่ง Cargo เพื่อพิสูจน์ composition

### Phase D — Migration

- ย้าย `impactSFX` และค่าที่เกี่ยวข้องออกจาก `CargoController`
- ย้าย serialized `defaultImpactVFX` ออกจาก Cargo prefab/`ParticleManager`
- รักษา stage sprite/particle data ใน `CargoModule.VisualState`; migration ย้ายเฉพาะ runtime routing ออกจาก `CargoController`
- Neutral profile ต้องรักษา material/impact behavior เดิมของ Cargo ที่ยังไม่ใช่ Soft/Explosive pilot
- ลบ compatibility fallback หลัง assets ถูก migrate และ validator ผ่าน

### Phase E — Designer polish

- เพิ่ม custom inspector เฉพาะสิ่งที่พิสูจน์แล้วว่าจำเป็น เช่นปุ่มเปิด FEEL template และ preview cue
- เพิ่ม preset creation menu หาก profile เริ่มมีจำนวนมาก
- เขียน workflow สั้นและตัวอย่าง Soft/Explosive

## 13. Validation และ test matrix

### EditMode/Asset validation

- [x] `CargoItemData` ทุกตัวมี valid polish profile
- [x] existing module visual-state ordering/channel regression ยังผ่านและไม่มี threshold copy ใน polish profile
- [x] material preset ใช้ shader ที่รองรับและเปิด keywords ที่จำเป็น
- [x] shader properties แบบ typed มีอยู่จริง
- [x] FEEL template มี `MMF_Player` และ reference target policy ถูกต้อง
- [x] world-Cargo FEEL template ไม่มี `MMF_Sound`/persistent `MMF_MaterialSetProperty` ที่ข้าม project policy
- [x] particle prefab มี particle system/ชนิด component ที่รองรับ
- [x] ไม่มี Cargo prefab variant ที่สร้างเพื่อเปลี่ยน data-only polish

### PlayMode

- [ ] Eggs และ Explosive spawn จาก shared prefab แล้วได้ feedback คนละแบบ
- [ ] impact หลายครั้งไม่เพิ่ม material instance หรือ orphaned GameObject
- [ ] loop particle เปลี่ยน stage, stop และคืน pool ถูกต้อง
- [ ] Impact/Pressure ยังเลือก sprite และ Temperature/Freshness ยังเลือก loop particle ตาม regression contract เดิม
- [ ] hover เข้า/ออกไม่ล้าง heat/cold material values
- [ ] Debug Mode OFF ยังเห็น hover highlight แต่ Status UI ยังคงซ่อน
- [ ] disable/despawn ระหว่าง FEEL เล่นแล้ว cleanup ครบ

### Multiplayer

- [ ] client A hover แล้วมีเพียง A เห็น highlight
- [ ] impact หนึ่งครั้งเล่นหนึ่งครั้งต่อ client ที่ควรเห็น/ได้ยิน
- [ ] host และ remote เห็น persistent status ตรงกัน
- [ ] late join เห็นสถานะ material/loop ปัจจุบัน แต่ไม่ replay impact/transition เก่า
- [ ] dedicated server/non-client path ไม่ initialize/instantiate/play presentation และ gameplay ไม่พึ่ง presentation callback

### Performance

- [x] ไม่มี `renderer.material` allocation ระหว่าง runtime loop
- [x] ไม่มี instantiate/destroy particle ทุก impact เมื่อระบบ pool รองรับ
- [x] ไม่มี polling shader/status ทุก frame หาก state ไม่เปลี่ยน
- [x] FEEL template ที่ใช้บ่อยถูก cache และไม่มี player ซ้ำโดยไม่จำเป็น

## 14. Acceptance criteria

งานถือว่าเสร็จเมื่อ:

1. Cargo สองประเภทที่ presentation ต่างกันมากใช้ shared prefab เดียวกัน
2. designer เริ่มแก้ได้จาก `CargoItemData` โดย stage visuals เดิมไม่ถูก duplicate และ event/material style อยู่ใน `CargoPolishProfile`
3. Sound ผ่าน spatial audio policy เดิม, particles มี lifecycle ชัดเจน, FEEL ใช้ template และ material เป็น per-instance
4. local hover, persistent status, one-shot event และ late join ทำงานตาม network policy
5. ค่า hardcoded/dragged impact presentation เดิมถูก migrate ออกโดยไม่มี fallback ซ่อน asset ที่ตั้งค่าผิด
6. Feature 01/03 regressions, validator และ test matrix สำคัญผ่าน
7. VFX/material sorting เชื่อมตาม Feature 02 หรือบันทึกเป็น gate ที่ยังไม่อนุญาตให้ปิด Feature 04

## 15. ความเสี่ยงและทางลดความเสี่ยง

| ความเสี่ยง | วิธีลดความเสี่ยง |
|---|---|
| Profile กลายเป็น SO ขนาดใหญ่ | แยก nested sections ให้ชัด และ reuse FEEL template/cue เฉพาะเมื่อแชร์จริง |
| FEEL target ผิด Cargo | ใช้ `FeedbackRoot` + `MMF_ReferenceHolder` และ validate ก่อน play |
| หลายระบบเขียน material block ทับกัน | ให้ Cargo material composer เป็นเจ้าของค่าที่อนุญาตและ merge ก่อน set |
| stage threshold มี source ซ้ำ | เก็บ threshold/sprite/particle ใน `CargoModule.VisualState` เท่านั้น; profile ใช้ normalized material curves |
| `SpritePropertiesSync` ล้าง material values | merge current property block และทดสอบ LateUpdate/flip regression |
| particle pool ซ้ำกับ FEEL | กำหนด one-shot ให้ FEEL, persistent loop ให้ Cargo lifecycle owner |
| audio behavior ไม่ตรงระบบห้อง | route world SFX ผ่าน `SpatialAudioManager` เท่านั้นใน v1 |
| abstraction เยอะเกินก่อนมี use case | pilot เพียง Soft + Explosive แล้วค่อยขยายจากปัญหาจริง |

## 16. Expected files เมื่อเริ่ม implementation

รายชื่อเป็นแนวทางและปรับได้หลัง audit รอบ implementation:

- `Assets/Script/Cargo System/Presentation/CargoPolishController.cs`
- `Assets/Script/Cargo System/Presentation/CargoPolishProfile.cs`
- `Assets/Script/Cargo System/Presentation/CargoPolishTypes.cs`
- `Assets/Editor/CargoPolishProfileValidator.cs`
- FEEL template prefabs สำหรับ Soft/Explosive
- All In 1 material presets ที่เปิด keyword ล่วงหน้า
- shared `CargoController (new).prefab` ที่มี presentation hierarchy

ไม่ควรสร้าง `CargoAudioManager`, `CargoFeelManager`, `CargoMaterialManager` และ `CargoParticleManager` เป็น component แยกทั้งหมดตั้งแต่แรก ถ้า internal helper ธรรมดาใน orchestration layer เพียงพอ

## Implementation Record

- Status: In Progress — Phase A–D implemented
- Started: 2026-08-26
- Updated: 2026-08-26
- Agent: Codex (GPT-5)
- Branch: `develop-cargo-revamp`
- Approved plan commit: `06c0cfdf`

### Files changed

- เพิ่ม `CargoPolishProfile` และ `CargoPolishController` ใต้ `Assets/Script/Cargo System/Presentation`
- เพิ่ม profile validator และ one-click migration ที่ `Tools/Cargo/Validate Polish Profiles` / `Tools/Cargo/Setup Polish Pilot`
- เพิ่ม Neutral, Soft และ Explosive profiles, AllIn1 material preset และ FEEL impact templates
- migrate `CargoItemData` ทั้ง 6 asset และ shared `CargoController (new).prefab`
- ย้าย stage visual routing ออกจาก `CargoController`; `ParticleManager` เหลือ persistent status loop helper
- อัปเดต Cargo EditMode regressions สำหรับ profile, stage channels และ per-renderer hover isolation

### Verification evidence

- Unity Editor connection: PASS — Unity CLI `1.0.0-beta.6`, Editor `6000.3.10f1`, project state `ready`
- Script compile: PASS — ไม่มี compiler error
- Cargo polish/profile validator: PASS — Cargo data 6 ตัว, profile 3 แบบ, shared Cargo prefab หนึ่งตัว
- Cargo production prefab validator: PASS
- Cargo EditMode test assembly: PASS — 25/25 รวม below-threshold, repeated-squash, exact restore และ collider-bounds isolation
- Static material path: PASS — runtime ใช้ `sharedMaterial` เฉพาะ assign preset และเขียนค่าผ่าน merged `MaterialPropertyBlock`; ไม่มี `renderer.material`
- Conservative retune: PASS — Soft material peak: heat `0.05`, cold `0.6`, hit `0.08`; Explosive: heat `0.07`, cold `0.35`, hit `0.1`
- Hover visibility correction: PASS — คืน AllIn1 hover alpha/glow เป็น `1/1.5` ตามเวอร์ชันก่อน conservative material retune; material status/hit values ที่ลดแล้วยังคงเดิม
- Impact squash regression fix: PASS — แยก minimum/full strength ของ squash ออกจาก SFX/particle cue เพื่อรองรับ Soft Cargo มวลต่ำ; Neutral/Soft/Explosive max deformation `0.035/0.055/0.025`
- Play Mode runtime probe: PASS — Soft impact strength `0.2` ได้ peak deformation `0.0198`, คืน authored scale exact และ physics/generated-collider root scale ไม่เปลี่ยน
- PlayMode test attempt: BLOCKED BY TEST PIPELINE — async runner เข้า Play Mode/domain reload แล้วค้างที่ `running`; ออกจาก Play Mode แล้ว ไม่มี test result จึงยังไม่นับว่าผ่านหรือ fail

### Remaining work / deviations

- ยังต้องทำ PlayMode/manual visual acceptance ของ Soft/Explosive, cleanup ระหว่าง FEEL และ status composition ในฉากจริง
- ยังต้องทดสอบ Host + Remote Client + Late Join และ dedicated server/non-client path
- holder pickup/release fields และ public cue methods เตรียมไว้แล้ว แต่ยังไม่ต่อ transition event เพราะเป็น optional contract และไม่ควรยัด owner event เข้า Cargo gameplay controller
- Feature 02 VFX/material sorting ยังเป็น final integration gate
- ยังไม่เพิ่ม custom inspector เพราะ workflow ปกติจาก `CargoItemData -> CargoPolishProfile -> FEEL prefab` ใช้งานได้และยังไม่มีหลักฐานว่าต้องเพิ่ม tool
- implementation รวม typed settings/event ไว้ใน `CargoPolishProfile.cs` แทนการสร้าง `CargoPolishTypes.cs` เพิ่มโดยไม่มีประโยชน์

### Plan re-review — 2026-08-26

- อ่าน README และ Feature Plans 01–03 จาก revision `f70164d5` รวมถึง stage-visual regression จาก `c2b6bf58`
- แก้ hierarchy ให้ `UIAnchor` เป็น sibling ของ `VisualRoot` ตาม Feature 01
- ยกเลิกแนวคิด status ranges/sprite/particle ซ้ำใน polish profile และรักษา `CargoModule.VisualState` เป็น stage source of truth
- แก้ hover source ให้ตรง `CargoGrabController` + reach policy และแยก pixel highlight ออกจาก Debug Mode gate
- เปลี่ยน Feature 02 เป็น final integration dependency แทน hard blocker ของ data/event/material pilot
- จำกัด spatial SFX v1 ให้ตรง API ปัจจุบัน และแก้ dedicated-server acceptance ให้เป็นสิ่งที่ runtime รับประกันได้จริง
