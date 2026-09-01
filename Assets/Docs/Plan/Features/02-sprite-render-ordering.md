# Feature Plan 02 — ระบบลำดับการแสดงผล Sprite แบบแน่นอน

Status: **In Progress**
Priority: **ทำภายหลัง Cargo 2.5D Foundation**  
Depends on: `01-cargo-2-5d-foundation.md`  
Last plan update: 2026-08-26

> AI instruction: ก่อน implement ให้เปลี่ยน Status เป็น `In Progress` และกรอก `Implementation Record` ท้ายไฟล์ หลังจบแต่ละ phase ต้องกลับมาอัปเดต checklist, รายการไฟล์, ผลทดสอบ และหลักฐานภาพ ห้ามติ๊กงานที่ยังไม่ได้ verify จริง

> Dependency note (2026-08-25): Cargo status UI เฉพาะตัวถูกแก้ใน Feature 01 แล้วด้วย fixed world offset/rotation และ depth-independent overlay shaders; Feature 02 ต้องรักษาพฤติกรรมนี้ไว้ แต่ยังต้อง implement ordering contract ของ Player/Cargo/Hand และ world visuals ส่วนที่เหลือ

## Master Checklist

### ก่อนเริ่มงาน

- [x] อ่าน `Assets/Docs/README.md` และแผนนี้ครบทั้งไฟล์
- [ ] ยืนยันว่า `01-cargo-2-5d-foundation.md` ผ่าน acceptance gate แล้ว หรือบันทึกเหตุผลหากจำเป็นต้องเริ่มก่อน
- [x] เปลี่ยน Status เป็น `In Progress` และลงวันที่ใน Implementation Record
- [x] บันทึก `git status` และห้ามทับ unrelated changes ของผู้ใช้
- [x] Audit Sorting Layer, `SpriteRenderer`, `SortingGroup`, Material และโค้ดที่เขียน sorting order ตอน runtime ของ Player, Hand, Cargo และ VFX ทั้งหมด
- [ ] ถ่าย baseline screenshot ของทุกกรณี overlap ใน Test Matrix
- [ ] ยืนยันทิศทางกล้อง, URP transparency sorting, render queue/ZWrite ของ sprite shader และจำนวนผู้เล่นสูงสุดที่รองรับ

### Data และ architecture

- [ ] กำหนดรายการ Sorting Layer สุดท้ายพร้อม semantic order ใน Unity Tags and Layers
- [x] สร้าง source of truth เดียวสำหรับ player render slot/order band และลบการคำนวณ `+10` ที่กระจายอยู่
- [ ] กำหนด `ActorSortIndex` แบบ deterministic สำหรับ host, remote client, late join และ respawn
- [x] เพิ่ม `SortingGroup` หรือ visual owner ที่เทียบเท่าให้ Player, Hand และ Cargo ที่ประกอบด้วยหลาย sprite
- [ ] กำหนด child-local order ของ body parts, cargo details, particles, shadows และ interaction indicators
- [x] อนุญาต visual Z เฉพาะใต้ `VisualRoot`; physics/network root ต้องไม่ออกจาก gameplay plane
- [ ] เพิ่ม validator สำหรับ layer ผิด, order band ชนกัน และ renderer ที่อยู่นอก visual owner

### Migration

- [ ] Migrate Player prefab และ renderer ที่ spawn ตอน runtime
- [ ] Migrate Player Hand ออกจาก UI Sorting Layer ไป Hand layer โดยเฉพาะ
- [ ] Migrate Cargo prefab และ cargo VFX ทั้งหมด
- [ ] Migrate held-item/interaction indicators ให้สัมพันธ์กับ Hand และ Cargo ถูกต้อง
- [ ] ลบ runtime order writer และ Z-offset workaround แบบเดิมที่ไม่ใช้แล้ว
- [ ] ตรวจ scene/prefab override ที่ขัดกับ policy ใหม่

### Verification

- [ ] Unity compile ผ่านโดยไม่มี error/warning ใหม่จาก feature นี้
- [ ] EditMode validation tests ของ layer/order ผ่าน
- [ ] PlayMode overlap tests แบบผู้เล่นหนึ่งคนและ cargo หลายชิ้นผ่าน
- [ ] Host + remote client อย่างน้อยสองผู้เล่นเห็นลำดับตรงกัน
- [ ] Hand อยู่บน Player, Cargo และ gameplay sprite อื่นเสมอ
- [ ] Cargo อยู่หน้า Player body แต่หลัง Hand เสมอ
- [ ] UI อยู่บน world sprite โดยไม่ถูกนำมาใช้แก้ลำดับ world sprite แบบผิดหน้าที่
- [ ] Particle/VFX อยู่ตาม semantic layer และไม่ flicker/หาย
- [ ] Movement, collision, grab และ network sync ยังอยู่บน physics plane เดิม
- [ ] ถ่าย after screenshot ครบทุกกรณีใน Test Matrix
- [ ] กรอก Implementation Record, changed files, tests, known limitations และ evidence
- [ ] เปลี่ยน Status เป็น `Complete` เมื่อ acceptance criteria ผ่านครบเท่านั้น

## 1. เป้าหมาย

ทำให้ลำดับการซ้อนของ sprite ในภาพแบบ 2.5D แน่นอนและเหมือนกันทุก client โดยมี contract หลักดังนี้:

1. Player body อยู่หลัง interactable world sprites
2. Cargo อยู่หน้า Player body
3. Hand อยู่หน้า Player, Cargo และ gameplay sprites ที่เกี่ยวข้องทั้งหมด
4. UI อยู่หน้า world-space gameplay visuals

ระบบต้องรองรับหลายผู้เล่นโดยไม่ต้องกระจายสูตร `+10` ไปตาม renderer หรือ gameplay script และการแก้ภาพต้องไม่ย้าย physics object ตามแกน Z

## 2. สิ่งที่ไม่อยู่ใน scope

- ไม่แก้ cargo physics หรือแรงของระบบถือของใน feature นี้
- ไม่ย้าย Rigidbody/Collider root ตามแกน Z เพื่อแก้ปัญหาภาพ
- ไม่ใช้ UI Sorting Layer เป็น layer รวมสำหรับ world-space hand
- ไม่ redesign character art, sprite rig หรือ animation content
- ไม่ทำ perspective depth sorting อัตโนมัติ; โปรเจกต์นี้ใช้ semantic order ที่กำหนดชัดเจน

## 3. ปัญหาปัจจุบันที่ต้อง Audit

- Player ใช้ sorting-order offset หลายจุด รวมถึง workaround `+10` ต่อผู้เล่น
- Player Hand ใช้ UI Sorting Layer ทั้งที่เป็น world-space gameplay object
- Cargo, Player และ Hand ยังไม่มี render contract ร่วมที่บันทึกไว้
- หลังเปลี่ยนเป็น 3D ค่า transform Z, shader depth test, transparency sorting และ Sorting Layer อาจขัดกัน
- Actor ที่มีหลาย sprite อาจถูก sprite ของ actor อื่นแทรกกลาง หาก child renderer คำนวณ order แยกกัน
- Raw client ID หรือ join order อาจทำให้ allocation ไม่เหมือนกันทุก peer
- Particle และ renderer ที่ spawn ตอน runtime อาจไม่รับค่าจาก prefab

ตอน implement ต้องแทนรายการนี้ด้วยผล audit จริง โดยระบุ script, prefab, material และ component ใน Implementation Record

## 4. Render Contract

### 4.1 Semantic Sorting Layers

ลำดับที่แนะนำจากหลังสุดไปหน้าสุด:

| ลำดับ | Sorting Layer | หน้าที่ |
|---:|---|---|
| 1 | `WorldBackground` | ฉากหลังที่อยู่หลัง actor เสมอ |
| 2 | `Player` | ตัวผู้เล่น เสื้อผ้า และ sprite ที่ติดกับลำตัว |
| 3 | `Cargo` | Cargo sprite และ visual ที่ติดกับ Cargo |
| 4 | `GameplayVFX` | Effect ที่ต้องอยู่หน้า body/cargo แต่หลังมือ |
| 5 | `Hand` | มือ, grip indicator และ hand-attached sprite |
| 6 | `WorldUI` | ป้ายและ interaction UI ในโลกเกม |
| 7 | `UI` | Screen-space UI เท่านั้น |

หาก layer เดิมมีความหมายเดียวกันให้ reuse และบันทึกแทนการสร้างซ้ำ สิ่งที่ห้ามเปลี่ยนคือ semantic order: `Player < Cargo < Hand < UI`

### 4.2 SortingGroup และ local order

Actor ที่มีหลาย sprite ต้องมี visual owner เดียว:

```text
PhysicsOrNetworkRoot (Z = 0)
└── VisualRoot
    └── SortingGroup
        ├── BackParts      local order -10..-1
        ├── MainSprite     local order 0
        ├── FrontParts     local order 1..9
        └── LocalEffects   local order ตามที่ประกาศไว้
```

กฎ:

- Group เป็นผู้กำหนด Sorting Layer และ base order ของ actor
- Child renderer ใช้ offset ขนาดเล็กและมีเอกสารกำกับเท่านั้น
- Child ห้ามคำนวณ global player offset เอง
- Runtime-spawned visual ต้องรับ owner/group ก่อนเปิด renderer
- หากยังต้องใช้ order band ต้องเผื่อช่วงให้ child ของ actor สองตัวไม่แทรกกัน
- Dedicated server ต้องทำงานได้แม้ไม่มี renderer

### 4.3 ลำดับระหว่างผู้เล่นหลายคน

ใช้ `ActorSortIndex` ที่ session/server allocator เป็นผู้แจก:

- ช่วงค่าตามจำนวนผู้เล่นสูงสุด ไม่ใช้ raw client ID โดยตรง
- Replicate mapping หรือ reconstruct แบบ deterministic เหมือนกันทุก peer
- Index คงเดิมระหว่าง disable/enable และ respawn ปกติ
- คืน slot เมื่อ despawn/disconnect แบบถาวร
- Late join ต้องรับ mapping เดิมก่อนเปิด visuals

หากจำเป็นต้องใช้ order band ให้มีสูตรอยู่ที่ configuration เดียว เช่น:

```text
playerGroupOrder = ActorSortIndex * PLAYER_ORDER_STRIDE
```

ค่า `PLAYER_ORDER_STRIDE` ต้องคำนวณจาก min/max child-local offset ที่ audit แล้ว และมี assertion ว่า band ข้างเคียงไม่ชนกัน ห้ามเหลือ literal `+10` ใน gameplay scripts

### 4.4 Visual Z Policy

Sorting Layer และ SortingGroup เป็นวิธีหลัก ส่วน Z offset ใช้เป็น fallback ที่ควบคุมจากส่วนกลางเท่านั้น หาก shader/camera ต้องใช้ depth จริง

```text
Player VisualRoot  -> ไกลกล้องที่สุด
Cargo VisualRoot   -> อยู่กลาง
Hand VisualRoot    -> ใกล้กล้องที่สุด
```

เครื่องหมายบวก/ลบต้องยืนยันจากทิศกล้องจริงใน Unity โดยมีข้อบังคับ:

- Physics/network root Z ต้องอยู่ gameplay plane เสมอ
- Visual Z อยู่ใน profile/component กลาง ห้ามกระจายใน prefab
- Collider, grab point, room detection และ query ไม่ตาม visual Z
- Camera clipping planes ต้องครอบคลุม offset ทั้งหมด
- ถ้ามีทั้ง orthographic/perspective camera ต้องได้ semantic order เดียวกัน

### 4.5 Material และ Render Pipeline

Audit renderer/material ที่เกี่ยวข้องทั้งหมด:

- Render queue และ transparent surface type
- ZWrite และ ZTest
- URP 2D/3D renderer compatibility
- Transparency Sort Mode และ Transparency Sort Axis
- Particle Renderer Sorting Layer/order
- Batching/instancing behavior ที่อาจกระทบลำดับ

ห้ามแก้ด้วย shader queue เพียงอย่างเดียว เว้นแต่มีเหตุผล, เอกสาร และ test ครบทุก sprite type

## 5. Runtime/Editor Components ที่เสนอ

ชื่อเปลี่ยนตาม convention ของโปรเจกต์ได้ แต่ responsibility ต้องไม่ปนกัน

### `SpriteRenderOrderProfile`

เก็บ configuration กลาง:

- Semantic Sorting Layer names/IDs
- Default group order ของแต่ละ actor type
- Player order stride และ local-order bounds
- Optional visual Z values
- Particle/VFX ordering defaults

### `ActorRenderOrderController`

- รับ `ActorSortIndex`
- Apply group layer/order เมื่อข้อมูลเปลี่ยน ไม่ rewrite child ทุก frame
- ไม่ให้ child renderer คำนวณ player offset เอง
- Validate missing group/renderer และ apply ค่าก่อน visual เปิด

### `RenderOrderValidator`

Editor validation ต้องรายงาน:

- Sorting Layer หายหรือสะกดผิด
- Hand ยังใช้ UI layer
- Physics/network root มี Z ไม่เป็นศูนย์
- Child renderer อยู่นอก SortingGroup ที่คาดไว้
- Child-local order เกิน reserved range
- Particle renderer ใช้ default ที่ไม่ปลอดภัย
- Runtime script อื่นยังเขียน sorting values นอก central controller

## 6. ขั้นตอน Implementation

### Phase A — Audit และล็อก contract

1. Inventory renderer, material, Sorting Layer, group, script, prefab และ scene override
2. บันทึกทิศกล้องและ render pipeline settings
3. ยืนยันจำนวนผู้เล่นสูงสุดและช่วง order ของ body parts
4. เลือก layer names/order และ local-order bounds สุดท้าย
5. เพิ่ม validation/test ที่ตรวจพบ setup เดิมที่ผิด

**Exit gate:** สามารถทำนายลำดับของ renderer ทุกตัวที่ audit จาก contract นี้ได้

### Phase B — Shared ordering infrastructure

1. เพิ่ม profile/configuration กลาง
2. เพิ่ม deterministic actor slot allocation และ replication
3. เพิ่ม runtime controller และ editor validator
4. แก้ Sorting Layers ผ่าน Unity-supported workflow
5. ยืนยันว่า ordering system ไม่แก้ physics transform

**Exit gate:** Test actor รับ slot แล้วทุก peer apply group order ตรงกัน

### Phase C — Prefab migration

1. Migrate Player visual hierarchy และ normalize child-local orders
2. Migrate Hand ไป dedicated Hand layer
3. Migrate Cargo `VisualRoot` จาก Cargo 2.5D plan
4. Migrate particles, VFX, shadows, outlines และ indicators
5. ลบ `+10`, UI-layer และ transform-Z workaround เดิม

ใช้ Unity Editor/Unity CLI สำหรับ prefab/project settings ห้ามแก้ serialized YAML ด้วยมือ

**Exit gate:** Prefab validator ไม่พบ invalid layer หรือ renderer ที่ไม่มี owner

### Phase D — Multiplayer และ edge cases

1. Test host, remote client, late join, disconnect/reconnect และ respawn
2. Test จำนวนผู้เล่นสูงสุดและ slot reuse
3. Test dynamic spawn และ object pooling ของ cargo/VFX
4. ยืนยัน dedicated server ไม่พึ่ง renderer
5. เก็บ screenshot จากทุก overlap combination

**Exit gate:** ทุก peer เห็นลำดับตรงกันและไม่มี slot collision

### Phase E — Cleanup และบันทึกผล

1. ลบ field และ order writer ที่ไม่ใช้แล้ว
2. เพิ่มคำแนะนำว่า sprite ใหม่ต้องเลือก layer/local order อย่างไร
3. กรอก Implementation Record และ evidence paths
4. เปลี่ยน Status เป็น Complete เมื่อ acceptance criteria ผ่านครบ

## 7. Test Matrix

| กรณี | ผลที่ต้องได้ |
|---|---|
| Player หนึ่งคนซ้อน Cargo หนึ่งชิ้น | Cargo บัง body และ Hand บัง Cargo |
| Hand ตัดผ่าน body ตัวเอง | Hand อยู่หน้า body parts ทั้งหมด |
| Player สองคนซ้อนกัน | แต่ละ actor ยังเป็นกลุ่มเดียว ไม่ถูก child ของอีกตัวแทรก และทุก peer เห็นตรงกัน |
| สอง Hand จับ Cargo เดียวกัน | ทั้งสอง Hand อยู่หน้า Cargo โดยไม่ขึ้นกับ ownership |
| Cargo ผ่าน gameplay VFX | VFX อยู่ตาม layer ที่ประกาศ และ Hand ยังเป็น gameplay sprite หน้าสุด |
| World-space label แสดง | Label อยู่หน้า gameplay sprites โดยไม่ย้าย physics Z |
| Late join ตอน actor ซ้อนกัน | Client ใหม่เห็น mapping เหมือน host |
| Respawn หรือ pool re-enable | Actor ได้ slot ที่ถูกต้อง ไม่มีค่า child เก่าค้าง |
| จำนวนผู้เล่นสูงสุด | Order band ไม่ชนและไม่ใช้ค่าที่เกินขอบเขต |
| เปลี่ยน resolution/camera zoom | Semantic order ไม่เปลี่ยน |

ทุก visual test ต้องเปรียบเทียบ screenshot ของ host กับ remote client ใน state เดียวกัน

## 8. Acceptance Criteria

- Hand อยู่หน้า Player, Cargo และ ordinary gameplay sprites เสมอ
- Cargo อยู่หน้า Player body และหลัง Hand เสมอ
- UI layer ไม่ถูกใช้เป็น workaround ของ world-space hand
- Gameplay Rigidbody, Collider, NetworkTransform และ grab calculation อยู่บน physics Z plane เดิม
- Multi-sprite actor ไม่ถูก sprite ของ actor อื่นแทรกกลางอย่างไม่ตั้งใจ
- ทุก peer ใช้ player sort-slot mapping เดียวกัน
- ไม่มี per-player `+10` กระจายนอก central ordering configuration
- Spawned/pooled sprites และ particles ได้ ordering ก่อนเปิดให้เห็น
- Validation และ Test Matrix ผ่านพร้อม evidence ครบ

## 9. ความเสี่ยงและ Rollback

| ความเสี่ยง | วิธีลดความเสี่ยง |
|---|---|
| Shader depth ไม่ทำตาม Sorting Layer | Audit queue/Z settings และใช้ visual-root Z เล็กน้อยเฉพาะเมื่อพิสูจน์ว่าจำเป็น |
| SortingGroup เปลี่ยน child behavior | เก็บ baseline child orders และ migrate ทีละ prefab family |
| Player slot ไม่ตรงกันระหว่าง peer | Server แจก compact index และ test late join/reconnect |
| VFX ใหม่หลุด policy | Validator scan prefab/scene และ shared spawn helper apply profile |
| UI ปนกับ world sprites | แยก `WorldUI` และ screen `UI` ชัดเจน |

Rollback เฉพาะไฟล์ของ feature นี้ และเก็บ baseline screenshots/mapping audit ไว้เปรียบเทียบ ห้าม revert งาน Cargo foundation ที่ผ่านแล้ว

## 10. Expected Files/Assets to Touch

ต้องยืนยัน path จริงระหว่าง audit ก่อนแก้:

- Player และ Player Hand prefabs
- Cargo prefab และ visual child prefabs
- Sprite/particle materials เฉพาะที่ audit ว่าต้องแก้
- Tags and Layers project settings ผ่าน Unity-supported editing
- Player spawn/network identity หรือ session slot allocation code
- Render profile/controller/validator scripts และ tests ใหม่
- Scene ที่มี prefab override เกี่ยวข้อง

## 11. Implementation Record

### Work session

- **Started:** 2026-08-26
- **Completed:** Not completed
- **Implementer:** Codex
- **Final status:** In Progress

### Decisions and deviations

- Final Sorting Layers/order: Player < Cargo < UI; Hand uses a dedicated `Hand` layer when present, otherwise Cargo order 100 as a non-UI fallback
- Actor slot strategy: N/A
- Local-order bounds/stride: Existing child-local orders preserved; visual-owner groups use order 0 and Hand fallback uses order 100
- Visual Z values และ camera direction: Player 0, Cargo -0.05, Hand -0.15; camera looks toward increasing Z from the negative-Z side
- Player facing: the physics/network root stays positive-scale; `Anim-Body` mirrors with a 180-degree Y rotation, which preserves transform handedness and keeps URP lighting valid. Authored child Z offsets are counter-mirrored each frame so eye/body depth does not reverse.
- Player eyeballs: depth is measured from the rendered `Sprite-Eye` surface rather than the non-rendering `EyePos` pivot; legacy `zOffsetInFront` values are clamped and the final world Z keeps `0.005` clearance behind the Cargo plane
- Deviations from plan: The project currently has no serialized Hand sorting layer and the live Unity CLI is unavailable, so no Tags and Layers/prefab YAML was edited. Runtime fallback removes Hand from UI and preserves the ordering contract.

### Changed files/assets

- `Assets/Script/Rendering/SpriteRenderOrderPolicy.cs`
- `Assets/Script/Player/CharacterController2D.cs`
- `Assets/Script/Player/PlayerEyeballs.cs`
- `Assets/Script/Player/Hand Handle/PlayerHand.cs`
- `Assets/Script/Cargo System/CargoController.cs`
- `Assets/Script/UI/CursorVisibilityController.cs` and UI integrations for menu, machine, settings and session panels

### Tests and evidence

- Compile result: `dotnet build Assembly-CSharp.csproj --no-restore` passed with 0 errors; only pre-existing warnings remain
- EditMode result: 25 tests completed: 24 passed, 1 pre-existing unrelated failure in `WeightedHoldingEditModeTests.Prefabs_HaveCoordinatorRigidHandSolverAndNoJoint` because the dirty `PlayerHand.prefab` collider width is 0.73 instead of the test's 0.99127316 expectation
- PlayMode result: Hosted a local MainLevel session and forced facing `+1`/`-1`. Both frames remained lit, the face/bulb/eye sockets mirrored together, and every rendered eye/body sprite retained the same world Z in both directions.
- Eyeball/cargo overlap result: Forced the Heavy Nuke cargo directly over both pupils for facing `+1` and `-1`. Cargo stayed at world Z `-3.290`; both pupils stayed at `-3.285` (behind cargo by `0.005`) and were visually occluded in both directions.
- Host + client result: Not run
- Screenshot/evidence paths: N/A

### Known limitations/follow-ups

- Dedicated Hand sorting layer should be added through Unity Editor Tags and Layers, then rerun the overlap and host/client matrix with screenshots.
