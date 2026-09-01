# Feature Plan 01 — Cargo 2.5D Foundation and Migration Repair

Status: **Prototype Implemented — Remote Client Validation Pending**
Priority: **Immediate / Blocking**
Depends on: None
Blocks: Sprite Render Ordering, Weighted Multiplayer Holding
Last plan update: 2026-08-26

> AI instruction: ก่อน implement ให้เปลี่ยน Status เป็น `In Progress` และกรอก `Implementation Record` ท้ายไฟล์ หลัง implement แต่ละ phase ให้กลับมาอัปเดต checklist, รายการไฟล์ และผลทดสอบ ห้ามติ๊กงานที่ยังไม่ได้ verify

## Master Checklist

### Baseline and safety

- [x] ตรวจ `git status` และบันทึก unrelated user changes ก่อนเริ่ม
- [x] ยืนยัน Unity Editor connection และ active project
- [x] ทำ converter dry-run เฉพาะ Cargo path และแนบผลใน Implementation Record
- [x] เพิ่ม EditMode prefab validator และยืนยันผลผ่านหลัง migration *(เพิ่มหลัง migration ในรอบ prototype นี้)*
- [x] บันทึก baseline compile/test result

### Cargo prefab and 2.5D physics

- [x] Cargo prefab ไม่มี `Rigidbody2D`, `Collider2D`, `Joint2D` เหลืออยู่
- [x] Cargo root ใช้ 3D `Rigidbody`
- [x] Cargo ใช้ 3D compound solid colliders ใต้ root body และมี Z depth มากกว่า 0
- [x] Cargo root scale คงที่ `Vector3.one`
- [x] แยก `VisualRoot`, `ProximityTrigger`, และ `UIAnchor`
- [x] `cargoScale` ถูกใช้กับ Visual/physics dimensions โดยไม่ scale root
- [x] ล็อก Position Z และ Rotation X/Y
- [x] อนุญาต Rotation Z ตาม design
- [x] ตั้ง Interpolation, Collision Detection และ PhysicsMaterial
- [x] Cargo ใช้ `CargoGrip` แยกจาก `NoFriction` ของพื้น และหยุด horizontal slide ได้โดยไม่แก้ material กลาง
- [x] ตรวจ Layer Collision Matrix สำหรับ Cargo, floor, wall, door, player และ hand

### Network transform and authority

- [x] NetworkTransform sync Position X/Y เท่านั้น
- [x] NetworkTransform ไม่ sync Position Z
- [x] NetworkTransform sync Rotation Z เท่านั้น
- [x] NetworkTransform ไม่ sync Rotation X/Y
- [x] NetworkTransform ไม่ sync root scale หาก scale อยู่ที่ VisualRoot
- [x] Cargo physics simulation มี authority เดียว
- [x] Dedicated-server-compatible flow ไม่มี state ที่พึ่ง Host ClientRpc side effect

### Runtime controller and modules

- [x] Collider generation เป็น explicit initialization step และ fail ด้วย error ชัดเจนเมื่อ Sprite Physics Shape ใช้ไม่ได้
- [x] Missing required component ทำให้ validation/initialization fail ด้วย error ที่ชัดเจน
- [x] Room detection รองรับ trigger/collider ที่อยู่บน parent หรือ child
- [x] Cargo เปลี่ยนห้องโดยไม่ reset temperature ผิดจังหวะหรือเพิ่มรายการซ้ำ
- [x] Impact damage ถูกคำนวณโดย authority และไม่ double-apply
- [x] Temperature รองรับ `minTemp` ถึง `maxTemp`
- [x] Temperature state name ใช้ polymorphism ถูกต้อง
- [x] Pressure อ่าน room pressure และมี update path จริง
- [x] Freshness decay ทำงานเฉพาะ authority
- [x] Module state ถูก clamp ตามชนิดของ module
- [x] Module lookup ถูก cache และรองรับ null/duplicate validation
- [x] Cargo เริ่มต้นด้วย invincibility 3 วินาทีหลัง authoritative initialization และมี API สำหรับ temporary buff ในอนาคต
- [x] Damage path ไม่ลด Impact ระหว่าง invincibility และ timer ลดลงเฉพาะ simulation authority

### Data-driven cargo test variants

- [x] สร้าง `CargoItemData` ชุดทดสอบ 3 แบบจาก workflow prefab เดียว: Light Eggs, Balanced Core และ Heavy Nuke
- [x] ใช้ sprite และ status module assets ที่มีอยู่แล้ว โดยไม่สร้าง Cargo prefab แยกต่อชนิด
- [x] ทำให้ mass, price, cargo scale, collider depth, proximity padding และ module composition แตกต่างกันเพื่อทดสอบน้ำหนัก/แรง/สถานะ
- [x] วางทั้ง 3 ตัวใน `MainLevel` เป็น scene NetworkObject ที่มาจาก `CargoController (new).prefab` และยังอยู่ใน `DefaultNetworkPrefabs`
- [ ] ให้ tester ตรวจ PlayMode จริงเรื่องการ initialize sprite/collider, การยกตามน้ำหนัก และ status UI ของทั้ง 3 แบบ

### UI, VFX and presentation compatibility

- [x] Cargo proximity ใช้ trigger แยกจาก solid collider
- [x] Trigger ตรวจ `PlayerHand` component/reference แทนการพึ่ง tag เพียงอย่างเดียว
- [x] Cargo UI ไม่ scale ตาม Cargo visual
- [x] Cargo status UI รักษา world rotation เป็นศูนย์และใช้ world-space Y offset คงที่ แม้ Cargo หมุนรอบ Z
- [x] Cargo status UI ใช้ sorting order สูงสุดและ overlay shaders ที่ `ZTest Always` สำหรับทั้ง Image และ TextMeshPro จึงไม่ถูกโมเดล 3D บัง
- [x] UI แสดง Temperature แบบช่วง min/max ถูกต้อง
- [x] Sprite/VFX อัปเดตจาก replicated state
- [x] Particle/VFX parent อยู่ใต้ VisualRoot หรือ VFX anchor ที่กำหนด
- [x] Local Debug Mode toggle Status UI ของ Cargo ทุกตัวด้วยปุ่ม `=` โดยค่าเริ่มต้น OFF
- [x] Status UI แสดงเฉพาะเมื่อ Debug Mode เปิดและ local pointer กำลัง hover Cargo; ไม่ hover ต้องซ่อนเสมอ

### Verification and rollout

- [x] C# compilation ผ่านโดยไม่มี error ใหม่
- [x] EditMode tests ผ่าน
- [ ] PlayMode 2.5D invariant tests ผ่าน *(test ถูกเพิ่มแล้ว แต่ runner ผ่าน MCP ติด `com.unity.pipeline` domain-reload bug; manual Play Mode ผ่าน)*
- [x] Offline smoke test ผ่าน
- [x] Host smoke test ผ่าน
- [ ] Host + remote client state consistency test ผ่าน
- [ ] Cargo ไม่หลุด Z plane หลังตก ชน ลาก และเปลี่ยนห้อง *(ตก/ชน/ลากผ่าน; ยังไม่ได้ทำ remote room-transition pass)*
- [x] ไม่มี Physics 2D warning/error จาก Cargo
- [x] ตรวจ Scene/Prefab overrides หลัง migration
- [ ] อัปเดตเอกสารนี้และเปลี่ยน Status เป็น `Implemented`

## 1. Goal

ทำให้ Cargo เป็นวัตถุ 2.5D ที่ใช้ภาพ Sprite 2D แต่ทำงานบน Physics 3D อย่างถูกต้องและสอดคล้องกับระบบ Multiplayer ของโปรเจกต์

พฤติกรรมสุดท้ายที่ต้องได้:

```text
2D SpriteRenderer
  + 3D Rigidbody/Collider
  + movement only on X/Y
  + rotation only around Z
  + server-authoritative cargo state
  + deterministic room/module/UI behavior
```

### 1.1 Implemented prototype snapshot (2026-08-25)

- ยังคง workflow **Cargo prefab เดียว** โดย `CargoItemData` เป็นตัวกำหนด sprite, visual scale, mass, collider settings และ status modules
- Prototype data ใช้ Eggs sprite และ module 4 ชนิด: Impact, Temperature, Pressure และ Freshness
- หลังเปลี่ยน sprite แล้ว `CargoColliderBuilder` อ่าน Sprite Physics Shape, scale ตาม `cargoScale` และสร้าง convex triangular prisms 3 ชิ้นที่มี depth 0.5 บนแกน Z
- Root ใช้ 3D Rigidbody/Netcode เท่านั้น ส่วนภาพยังเป็น `SpriteRenderer` ใต้ `VisualRoot`
- Scene instance `[CARGO PROTOTYPE] Status Eggs` ถูกวางที่ `(0, -1.4, -3.24)` ใน `MainLevel` และอ้าง prefab GUID เดิมซึ่งลงทะเบียนอยู่ใน `DefaultNetworkPrefabs`
- Floor/Wall/BG ภายใน `MainShip - 3D` มี kinematic child Rigidbody แยกจาก dynamic frozen ship root เพื่อให้ dynamic Cargo ชนพื้น/ผนังภายในด้วย PhysX ได้แน่นอน
- Cargo collider ใช้ `CargoGrip` (`dynamicFriction 0.8`, `staticFriction 1.0`, combine แบบ `Maximum`) จึงยังมีแรงเสียดทานเมื่อพื้นเรือใช้ `NoFriction`
- Cargo status UI ยังคงเป็น World Space แต่ pose ตามตำแหน่ง Cargo ด้วย world offset คงที่โดยไม่รับ rotation ของ Cargo และใช้ depth-independent overlay shaders สำหรับ Image/TextMeshPro

### 1.2 Latest protection and debug UI update (2026-08-26)

- `CargoController` ให้ Cargo ทุกตัว invincible `3` วินาทีหลัง initialize ครั้งแรกบน simulation authority; `GrantInvincibility(float)` ใช้ต่อยอด temporary buff และ `ClearInvincibility()` ใช้ยกเลิก buff ได้
- `CargoDebugMode` อยู่บน `[DEBUG] Cargo Debug Mode` ใน `MainLevel`; กด `KeyCode.Equals` (ปุ่ม `=` แถวตัวเลขหลัก ไม่ใช่ numpad) เพื่อ toggle local gate ของ Status UI ให้ Cargo ทุกตัวใน scene/instance ปัจจุบัน
- Status UI ใช้เงื่อนไข `debugModeEnabled && localPointerHovering`: Debug OFF หรือ pointer ไม่ hover จะซ่อน panel เสมอ แม้มี Cargo อยู่ใน scene

## 2. Non-goals

งานต่อไปนี้ไม่อยู่ใน feature นี้:

- เปลี่ยน SpringJoint เป็น holding solver ตัวใหม่
- ออกแบบลำดับภาพ Player/Cargo/Hand ทั้งโปรเจกต์
- เพิ่ม Cargo content ใหม่หรือปรับ game balance ของราคา/น้ำหนัก
- เปลี่ยน art asset หรือ shader style
- ทำ inventory system

Feature นี้ต้องทำให้ Cargo รองรับระบบถือปัจจุบันได้ในระดับ compatibility แต่ไม่แก้อาการ wiggle เชิง design ซึ่งอยู่ใน Feature 03

## 3. Current State and Known Defects

### 3.1 Partial physics migration

Prefab ปัจจุบัน:

- `Assets/Prefab/Cargo/CargoController (new).prefab`
- มี `Rigidbody2D`
- ไม่มี 3D `Rigidbody`
- ไม่มี authored 3D solid collider
- มี `NetworkRigidbody`/Network components ที่คาดหวัง 3D physics

ผลกระทบ:

- `CargoController` หา `Rigidbody` ไม่พบ
- `CargoGrabController` ปฏิเสธการจับ
- `OnCollisionEnter(Collision)` ไม่ทำงานกับ Rigidbody2D
- fallback BoxCollider ถูกสร้างแต่ไม่มี dynamic 3D Rigidbody ที่ถูกต้อง

### 3.2 Wrong network axis configuration

Cargo NetworkTransform ปัจจุบัน sync:

- Position X/Y: เปิด
- Position Z: ปิด
- Rotation X/Y: เปิด
- Rotation Z: ปิด

ค่าการหมุนกลับด้านกับความต้องการ 2.5D ต้องเปลี่ยนเป็น Rotation Z เท่านั้น

### 3.3 Physics and visual scale are coupled

`CargoController.InitializeCargo()` ตั้ง `transform.localScale` จาก `cargoScale` ซึ่งทำให้:

- Rigidbody root และ NetworkTransform ถูก scale
- Collider ถูก scale แบบ implicit
- UI child ถูก scale ตาม Cargo
- Joint/anchor behavior เปลี่ยนตาม scale
- การทำ visual Z offset ภายหลังทำได้ยาก

### 3.4 Proximity UI cannot reliably fire

`CargoController.OnTriggerEnter` รอ collider ที่ tag `PlayerHand` แต่:

- Cargo fallback collider เป็น solid collider
- PlayerHand BoxCollider ปัจจุบัน `isTrigger = false`
- ไม่มี proximity trigger ที่แยกหน้าที่ชัดเจน

### 3.5 Cargo runtime state is local-only

ค่าปัจจุบันถูกเก็บใน `Dictionary<System.Type, float>` ภายใน `CargoController` ซึ่งไม่ replicate ผ่าน Netcode ทำให้แต่ละ client อาจเห็น durability, temperature, freshness, sprite และ VFX ต่างกัน

### 3.6 Module correctness defects

- Temperature ถูก clamp ที่ `0..max` ทำให้ค่าติดลบและ cold state ใช้ไม่ได้
- `TemperatureModule.GetStateName` ซ่อน base method ด้วย `new`
- Pressure มี configuration แต่ไม่มี runtime update
- Visual state lookup พึ่งลำดับ list โดยไม่มี validation
- Module list สามารถมี null หรือ duplicate type ได้

## 4. Target Prefab Architecture

```text
CargoRoot                     scale = (1,1,1), physics Z = gameplay plane
├── NetworkObject
├── NetworkTransform
├── NetworkRigidbody
├── Rigidbody                 dynamic 3D body
├── CargoController
├── CargoColliderBuilder
├── ParticleManager
│
├── VisualRoot                visual scale + optional visual-only Z
│   ├── SpriteRenderer
│   └── VFXAnchor
│
├── GeneratedColliders        compound children attached to root Rigidbody
│   └── Convex MeshCollider[] triangular prisms from Sprite Physics Shape
│
├── ProximityTrigger          no physical collision response
│   ├── BoxCollider           isTrigger = true
│   └── CargoProximitySensor
│
└── UIAnchor                  world-space status location, scale-independent
```

### Root rules

- Root scale ต้องเป็น `Vector3.one`
- Rigidbody position Z ต้องเท่ากับ configured gameplay plane
- VisualRoot เป็นที่เดียวที่รับ visual scale
- Collider dimensions ตั้งโดยข้อมูลและ Sprite Physics Shape ไม่พึ่ง root scale
- Collider child ทุกชิ้นไม่มี Rigidbody ของตัวเอง จึงเป็น compound collider ของ root Rigidbody
- UIAnchor และ ProximityTrigger ต้องมีขนาด/ตำแหน่งที่ชัดเจนใน local space

## 5. Data Model Changes

### 5.1 CargoItemData physics fields

เพิ่มหรือเทียบเท่ากับข้อมูลต่อไปนี้:

```csharp
bool autoSizeColliderFromSprite = true;
Vector2 colliderSizeOverride;
Vector2 colliderOffset;
float colliderDepth = 0.5f;
float visualScale = 1f;
```

Migration rule:

- `cargoScale` เดิม map ไป `visualScale`
- ถ้า auto size ให้คำนวณ collider size จาก sprite bounds × visual scale
- ถ้ามี override ให้ใช้ค่าที่กำหนด
- ห้ามแก้ root scale เพื่อให้ collider ใหญ่ขึ้น

ควรรักษา serialized compatibility ด้วย `FormerlySerializedAs` หาก rename field

### 5.2 Stable module identifiers

ห้ามใช้ `System.Type` เป็น network identity ให้เพิ่ม stable enum:

```csharp
public enum CargoModuleId : byte
{
    Impact,
    Temperature,
    Pressure,
    Freshness
}
```

แต่ละ `CargoModule` ต้องให้ข้อมูล:

```csharp
CargoModuleId ModuleId { get; }
float GetMinValue();
float GetMaxValue();
float GetInitialValue(in CargoEnvironment environment);
float ClampValue(float value);
string GetStateName(float currentValue);
```

`GetStateName` ต้องเป็น `virtual`/`override` จริง

### 5.3 Replicated runtime state

ใช้ state แบบ fixed fields เพราะระบบมี module ที่รู้จักจำนวนจำกัด:

```csharp
[Flags]
public enum CargoModuleMask : byte
{
    None = 0,
    Impact = 1 << 0,
    Temperature = 1 << 1,
    Pressure = 1 << 2,
    Freshness = 1 << 3
}

public struct CargoRuntimeState : INetworkSerializable, IEquatable<CargoRuntimeState>
{
    public CargoModuleMask ActiveModules;
    public float Durability;
    public float Temperature;
    public float Pressure;
    public float Freshness;
}
```

ข้อกำหนด:

- Server เป็นผู้เขียน state ใน networked session
- Offline mode ใช้ state structure เดียวกันผ่าน local store
- Client subscribe state change เพื่ออัปเดต Sprite/VFX/UI
- ไม่ replicate ทุก frame โดยไม่จำเป็น
- Temperature/freshness publish ด้วย fixed cadence หรือ epsilon threshold
- Impact publish ทันทีเมื่อเปลี่ยน

## 6. Physics Configuration

### 6.1 Rigidbody

Required constraints:

```csharp
RigidbodyConstraints.FreezePositionZ |
RigidbodyConstraints.FreezeRotationX |
RigidbodyConstraints.FreezeRotationY
```

Defaults ที่ต้องยืนยันด้วย playtest:

- `useGravity = true`
- `isKinematic = false` บน authority
- `interpolation = Interpolate`
- `collisionDetectionMode = ContinuousSpeculative` สำหรับ Cargo ที่ถูกลากเร็ว มิฉะนั้น Discrete
- mass มาจาก CargoItemData และต้องมากกว่า epsilon
- center of mass ใช้ auto ก่อน เว้นแต่ asset ต้อง override

### 6.2 Planar invariant safeguard

Constraints เป็น defense หลัก แต่ต้องมี validation/safeguard:

- เมื่อ spawn ให้ snap Rigidbody Z ไป gameplay plane
- หลัง teleport/network spawn ให้ snap อีกครั้ง
- ตรวจใน development build ว่า `abs(z - planeZ) <= epsilon`
- ถ้าหลุดเพราะ external transform write ให้ log source/context และแก้ที่ผู้เขียน transform ไม่ใช่ snap เงียบ ๆ ทุก frameใน production

### 6.3 Collider

- ใช้ solid 3D compound colliders ใต้ `GeneratedColliders` ซึ่งผูกกับ Rigidbody บน CargoRoot
- Z depth ต้องมากกว่า 0 เพื่อให้ PhysX contact/ClosestPoint ทำงาน
- ขนาดต้องสัมพันธ์กับ sprite และ visualScale
- ไม่ใช้ trigger collider เดียวทำทั้ง collision และ UI proximity
- PhysicsMaterial 3D ต้องระบุ explicit
- Workflow prefab เดียวใช้ Sprite Physics Shape เป็น source แล้ว triangulate เป็น convex prism MeshCollider หลายชิ้น เพื่อรักษารูปร่างเว้าโดยไม่ทำ prefab แยกต่อ Cargo

### 6.4 Layer collision audit

ตรวจอย่างน้อย:

- Cargo/Object ↔ GroundForObject
- Cargo/Object ↔ wall/door/platform
- Cargo/Object ↔ Player
- Cargo/Object ↔ other Cargo
- Cargo/Object ↔ PlayerHand
- ProximityTrigger ↔ PlayerHand
- Room trigger ↔ Cargo

บันทึก intentional ignores ใน code comment หรือเอกสาร ไม่พึ่ง collision matrix ที่ไม่มีคำอธิบาย

## 7. Network Configuration

### 7.1 Cargo authority

เลือก server-authoritative Cargo:

- Server จำลอง Rigidbody
- Server คำนวณ collision damage และ module state
- Client รับ transform/state
- Client ห้ามเขียน Cargo transform โดยตรง

Feature 03 จะส่ง cursor intent ไป Server แทนการ transfer ownership ของ Cargo

### 7.2 NetworkTransform axes

Target:

```text
Position X:  sync
Position Y:  sync
Position Z:  disabled
Rotation X:  disabled
Rotation Y:  disabled
Rotation Z:  sync
Scale X/Y/Z: disabled on root
```

### 7.3 Dedicated server compatibility

- Server-side state ห้ามถูกเซ็ตเฉพาะใน ClientRpc
- Grab/release guard ต้องมี state บน Server แม้ไม่มี local client
- `OnNetworkDespawn` ต้องล้าง room registration, update registration และ holder references
- Scene-placed และ dynamically spawned Cargo ต้องใช้ initialization path เดียวกัน

## 8. CargoController Refactor

### 8.1 Lifecycle

Recommended sequence:

```text
Awake
  -> cache required components
  -> validate prefab structure

Start / OnNetworkSpawn
  -> resolve data
  -> apply visual and authored collider configuration
  -> resolve current room
  -> initialize authoritative/local runtime state
  -> subscribe replicated state
  -> register update loop exactly once

OnNetworkDespawn / OnDestroy
  -> unsubscribe
  -> unregister room/update loop
  -> release VFX/UI resources
```

ต้องป้องกัน double registration ระหว่าง `Start` กับ `OnNetworkSpawn`

### 8.2 Room tracking

- Server เป็นผู้กำหนด current room ใน network session
- RoomMarker trigger ต้องใช้ `GetComponentInParent<CargoController>()`
- ป้องกัน duplicate list entry
- เมื่อออกจากห้อง ต้อง clear เฉพาะเมื่อออกจาก room ที่เป็น current จริง
- หาก collider overlap หลาย room ให้มีกฎ deterministic เช่น volume priority หรือ closest room center
- Initial room query ต้องใช้ radius/overlap ที่เหมาะกับ collider ไม่ใช้ 0.01 แบบเปราะบางโดยไม่มี fallback
- Oxygen drain count ต้องเพิ่ม/ลดครั้งเดียวและ cleanup ตอน despawn

### 8.3 Simulation cadence

- Physics/room-sensitive state ใช้ FixedUpdate หรือ centralized fixed update
- Freshness สามารถใช้ server elapsed time แต่ต้อง deterministic ต่อ pause/time scale policy
- Temperature เข้าหา room temperature ด้วย rate ที่ time-step independent
- Pressure เข้าหา room pressure ด้วย `pressureChangeRate`
- Visual/UI ไม่ควร rebuild hierarchy ทุก frame

### 8.4 Impact

- ใช้ mass ของ Rigidbody ที่ authoritative
- ระบุหน่วย damage formula และ threshold ให้ชัดเจน
- ใช้ collision impulse หากเหมาะกว่า mass × velocity / fixedDeltaTime
- กรอง collision ที่เกิดจาก spawn overlap หรือ micro-contact
- Audio/VFX เป็น presentation event; damage state เป็น authority event
- ป้องกัน client ทุกเครื่องเล่น impact audio ซ้ำจาก replicated/local collision

## 9. UI and VFX

### 9.1 Proximity sensor

สร้าง `CargoProximitySensor` หรือ equivalent:

- อยู่บน `ProximityTrigger`
- ใช้ trigger collider
- ตรวจ `PlayerHand`/owner information จาก component hierarchy
- รองรับมือหลายคนด้วย `HashSet` หรือ reference count
- UI แสดงเมื่อ local player's hand อยู่ใกล้ ไม่แสดงจาก remote hand โดยไม่ตั้งใจ
- OnDisable/OnDestroy ล้าง proximity state

### 9.2 UI values

- Durability/Freshness แสดง normalized 0–100%
- Temperature แสดงค่าจริงและช่วง min/max ไม่ใช้ `current/maxTemp` อย่างเดียว
- Pressure แสดงค่าจริงและช่วง min/max
- State label เรียก virtual module API
- UI instance อยู่ใต้ UIAnchor และมี world scale คงที่

### 9.3 VFX

- State particle อยู่ใต้ VFXAnchor
- Impact particle สามารถ detach ชั่วคราวได้ แต่ cleanup/return-to-pool ต้องแน่นอน
- VisualRoot scale ต้องถูกคำนึงเมื่อวาง particle
- Client update VFX จาก replicated state ไม่คำนวณ state ซ้ำ

## 10. Editor Tooling and Validation

### 10.1 Existing converter

ใช้ `Assets/Editor/Physics2DTo3DConverter.cs` ด้วยขั้นตอน:

1. Commit/snapshot worktree ก่อน
2. ตั้ง path filter แคบเฉพาะ Cargo prefab folder
3. Run scan only
4. ตรวจว่า dependency เช่น NetworkRigidbody ไม่ขวางการถอด Rigidbody2D
5. สร้าง/ยืนยัน 3D PhysicsMaterial
6. Convert ผ่าน Editor
7. เปิด Prefab ตรวจด้วย Inspector
8. ตรวจ scene instance overrides

ห้าม run convert ทั้ง project ใน feature นี้

### 10.2 Prefab validator

เพิ่ม EditMode validation ที่ assert:

- มี `SpriteRenderer` ใต้ VisualRoot
- มี 3D Rigidbody บน root และ solid compound colliders ใต้ `GeneratedColliders`
- ไม่มี Physics2D component
- root scale = one
- constraints ถูกต้อง
- network axes ถูกต้อง
- proximity collider เป็น trigger
- solid collider ไม่เป็น trigger
- required references ไม่ null
- layer ถูกต้อง

ควรทำ menu validation สำหรับ Cargo assets ทั้งหมดและ test ที่รันใน CI ได้

## 11. Implementation Phases

### Phase A — Baseline tests

1. เพิ่ม prefab invariant validator/test
2. เพิ่ม unit tests สำหรับ module clamp/state selection
3. บันทึก current failures
4. ยังไม่แก้ production behavior ใน phase นี้

Exit gate: Tests ตรวจเจอ Rigidbody2D/wrong axes/temperature defect ได้จริง

### Phase B — Prefab and physics root migration

1. แยก hierarchy CargoRoot/VisualRoot/Trigger/UIAnchor
2. Convert Rigidbody2D เป็น Rigidbody
3. Author solid collider และ proximity trigger
4. ตั้ง constraints/material/layers/network axes
5. รักษา prefab GUID เดิม
6. Apply/migrate scene overrides

Exit gate: Prefab validator ผ่านและ Cargo ตก/ชนบน XY plane ได้

### Phase C — Controller and module correctness

1. Refactor lifecycle/components
2. แก้ temperature/state polymorphism
3. เพิ่ม pressure simulation
4. แก้ room tracking/oxygen registration
5. แก้ UI/VFX bindings

Exit gate: Offline PlayMode scenarios ผ่าน

### Phase D — Network authority and replication

1. เพิ่ม replicated CargoRuntimeState
2. ย้าย simulation/damage ไป authority
3. ให้ clients consume state อย่างเดียว
4. เพิ่ม spawn/despawn cleanup
5. ทดสอบ Host/Client และ dedicated-compatible paths

Exit gate: Host และ remote client เห็น state/visual ตรงกัน

### Phase E — Scene rollout

1. เปิดใช้ Cargo ใน test scene ที่ควบคุมได้
2. ตรวจ MainLevel spawn path/room configs
3. ตรวจ NetworkPrefab registration
4. ทำ regression pass กับ door, room, water, floor และ player

Exit gate: Acceptance criteria ทั้งหมดผ่าน

## 12. Test Matrix

### EditMode

- Cargo prefab component invariant
- NetworkTransform axis configuration
- Collider sizing from sprite/data
- Temperature negative clamp
- Hot/cold state selection
- Pressure clamp/update formula
- Freshness clamp/decay formula
- Duplicate/null module validation
- CargoRuntimeState serialization round trip

### PlayMode offline

- Spawn in valid room
- Spawn outside room gives controlled error/recovery
- Fall onto floor
- Hit wall at speed
- Rotate around Z only
- Move between two rooms
- Hand enters/leaves proximity trigger
- Despawn/disable cleanup

### Network PlayMode/manual

- Host sees same state as remote client
- Impact applied once on Server
- Late join receives current Cargo state
- Cargo room transition replicates
- Cargo despawn cleans client UI/VFX
- Client cannot move Cargo by directly writing transform
- Dedicated server grab/release state does not depend on host callbacks

### Stress

- Multiple Cargo items in one room
- Multiple collisions in one FixedUpdate
- Several clients near one Cargo
- Cargo held while crossing room trigger
- Scene transition while Cargo UI/VFX active

## 13. Acceptance Criteria

Feature พร้อมปิดเมื่อ:

1. Cargo ยังคง render ด้วย SpriteRenderer 2D
2. Cargo ใช้ Physics 3D อย่างเดียว
3. Cargo position Z ไม่เปลี่ยนเกิน tolerance
4. Cargo ไม่หมุน X/Y
5. Cargo หมุนและ sync Z ได้
6. Collision/impact/room trigger ใช้ 3D callbacks ถูกต้อง
7. Temperature/Pressure/Freshness/Durability ถูกต้องตาม data
8. Host และ client เห็น state/visual เดียวกัน
9. UI proximity ทำงานกับ local hand
10. ไม่มี runtime fallback ที่ซ่อน Prefab configuration error
11. Compile, EditMode, PlayMode และ multiplayer smoke tests ผ่าน
12. เอกสาร checklist และ Implementation Record ถูกอัปเดต

## 14. Risks and Rollback

### Risks

- Scene instance overrides หายหลังแก้ prefab hierarchy
- Collider shape เปลี่ยน gameplay feel
- Server authority ทำให้ input/holding latency ชัดขึ้นก่อน Feature 03
- Root scale migration ทำให้ visual/collider size ไม่ตรงเดิม
- Particle/UI references ขาดจากการย้าย child
- Late-join state initialization ผิดลำดับ

### Mitigation

- รักษา prefab GUID
- ใช้ editor migration และ dry-run
- เพิ่ม prefab tests ก่อน conversion
- บันทึก before/after Inspector values
- ทำ one-cargo test scene ก่อน rollout
- แยก commit ต่อ phaseเพื่อ rollback ได้

### Rollback boundary

ถ้า Phase B ทำให้ Scene overrides เสีย ให้ rollback เฉพาะ prefab/migration commit โดยเก็บ tests จาก Phase A ไว้เพื่อใช้แก้รอบต่อไป

## 15. Expected Files

ไฟล์ที่คาดว่าจะถูกแก้หรือเพิ่ม:

- `Assets/Prefab/Cargo/CargoController (new).prefab`
- `Assets/Script/Cargo System/CargoController.cs`
- `Assets/Script/Cargo System/CargoDebugMode.cs`
- `Assets/Script/Cargo System/CargoItemData.cs`
- `Assets/Script/Cargo System/CargoModule.cs`
- `Assets/Script/Cargo System/CargoModuleBase.cs`
- `Assets/Script/Cargo System/TemperatureModule.cs`
- `Assets/Script/Cargo System/PressureModule.cs`
- `Assets/Script/Cargo System/ImpactModule.cs`
- `Assets/Script/Cargo System/RottenModule.cs`
- `Assets/Script/Cargo System/UI/UICargoInfo.cs`
- `Assets/Script/Cargo System/UI/SlotCargoModuleInfo.cs`
- `Assets/Script/Ship System/ShipRoom/RoomMarker.cs`
- `Assets/Script/GlobalManager/ParticleManager.cs`
- New cargo proximity/network state/validator scripts
- New EditMode and PlayMode tests

`CargoGrabController` และ `PlayerHand` แก้เฉพาะ compatibility ที่จำเป็นต่อ 3D Cargo ใน feature นี้ การ redesign เต็มอยู่ Feature 03

## Implementation Record

- Status: Prototype Implemented — Remote Client Validation Pending
- Started: 2026-08-25
- Updated: 2026-08-26
- Agent: Codex (GPT-5)
- Branch: `develop-cargo-revamp`

### Files changed

- `Assets/Prefab/Cargo/CargoController (new).prefab`
- `Assets/Prefab/Cargo/CargoUI/UICargoInfo.prefab`
- `Assets/Scenes/MainLevel.unity`
- `Assets/PhysicsMaterial/CargoGrip.physicMaterial`
- `Assets/Shaders/CargoUIOverlay.shader`
- `Assets/Data/Cargo/EggsCargo/EggsData.asset`
- `Assets/Data/Cargo/NukeCargo/NukeCargo.asset`
- `Assets/Data/Cargo/Prototype/Cargo Prototype.asset`
- `Assets/Data/Cargo/Prototype/Prototype Impact.asset`
- `Assets/Data/Cargo/Prototype/Prototype Temperature.asset`
- `Assets/Data/Cargo/Prototype/Prototype Pressure.asset`
- `Assets/Data/Cargo/Prototype/Prototype Freshness.asset`
- `Assets/Data/Cargo/Test Variants/Cargo Test Light Eggs.asset`
- `Assets/Data/Cargo/Test Variants/Cargo Test Balanced Core.asset`
- `Assets/Data/Cargo/Test Variants/Cargo Test Heavy Nuke.asset`
- `Assets/Script/Cargo System/CargoController.cs`
- `Assets/Script/Cargo System/CargoColliderBuilder.cs`
- `Assets/Script/Cargo System/CargoProximitySensor.cs`
- `Assets/Script/Cargo System/CargoRuntimeState.cs`
- `Assets/Script/Cargo System/CargoItemData.cs`
- `Assets/Script/Cargo System/CargoModule.cs`
- `Assets/Script/Cargo System/CargoModuleBase.cs`
- `Assets/Script/Cargo System/ImpactModule.cs`
- `Assets/Script/Cargo System/TemperatureModule.cs`
- `Assets/Script/Cargo System/PressureModule.cs`
- `Assets/Script/Cargo System/RottenModule.cs`
- `Assets/Script/Cargo System/UI/UICargoInfo.cs`
- `Assets/Script/Cargo System/UI/SlotCargoModuleInfo.cs`
- `Assets/Script/GlobalManager/ParticleManager.cs`
- `Assets/Script/Player/CargoGrabController.cs`
- `Assets/Script/Ship System/ShipRoom/RoomMarker.cs`
- `Assets/Editor/CargoPrototypeValidator.cs`
- `Assets/Tests/EditMode/CargoPrototypeEditModeTests.cs`
- `Assets/Tests/EditMode/PressureExpress.Cargo.EditModeTests.asmdef`
- `Assets/Tests/PlayMode/CargoPrototypePlayModeTests.cs`
- `Assets/Tests/PlayMode/PressureExpress.Cargo.PlayModeTests.asmdef`
- `Assets/Docs/Plan/Features/Artifacts/CargoPrototypeScene.png`
- `Assets/Docs/Plan/Features/Artifacts/CargoUIOverlayAndRotation.png`
- `Assets/Docs/Plan/Features/01-cargo-2-5d-foundation.md`
- `Assets/Docs/README.md`

### Verification evidence

- Pre-fix Git safety checkpoint: **PASS** — commit `0a6d14a8` (`Implement weighted multiplayer cargo holding`) ถูก push ไป `origin/develop-cargo-revamp` ก่อนเริ่มแก้ UI/ground sliding ตามคำสั่งผู้ใช้
- Compile: **PASS** — Unity 6000.3.10f1 recompile completed/up-to-date โดยไม่มี C# error; validator และ Cargo/UI runtime smoke ไม่มี exception จาก feature นี้ (RenderGraph error ของ third-party Haze และ game-over log เดิมไม่อยู่ใน scope นี้)
- Prefab validator: **PASS** — root scale one, Physics2D count zero, 3D Rigidbody settings valid, required hierarchy valid, layer matrix valid, server-authoritative network axes valid, Sprite Physics Shape generated 3 convex 3D prisms, explicit CargoGrip valid, Cargo UI overlay sorting/shaders valid, and prefab is registered in `DefaultNetworkPrefabs`.
- EditMode: **PASS 21/21** (`PressureExpress.Cargo.EditModeTests`) — รวม Cargo regression 11 tests และ Weighted Holding 10 tests; เพิ่ม protection API/default-duration, all-cargo Debug Mode defaults, debug+hover UI gate, fixed-world UI pose/overlay shaders และจำลอง CargoGrip บนพื้น `NoFriction` จริง
- PlayMode automated: **BLOCKED BY RUNNER** — test assembly and invariant test were added, but `com.unity.pipeline` lost its test tree during HTTP/domain reload (`Test tree is not available for PostbuildCleanupTask` / `ReloadScene cannot be used with InitTestScene`). This is recorded as an unchecked gate; it is not counted as a pass.
- PlayMode manual 2.5D: **PASS** — Cargo fell and rested at `(0, -2.020, -3.240)`, velocity/angular velocity reached zero, X/Y rotation stayed zero, Z drift was `0.000000`, and 3 generated MeshColliders were present.
- Offline: **PASS** — after the normal Bootstrap → MainMenu flow, MainLevel Cargo initialized from the prototype ScriptableObject, resolved `SpawnRoom`, simulated four statuses and produced no Cargo error.
- Host: **PASS** — `StartHost=True`, Netcode scene load `Started`, active scene `MainLevel`, one connected client, Cargo `IsSpawned=True`/`IsInitialized=True`; UI active ด้วย world offset `(0.150, 0.150, 0)`, world rotation `0`, Canvas `UI/32767` และ overlay shaders ทั้ง Image/TextMeshPro ถูกต้อง
- Host ground grip: **PASS** — ตั้ง horizontal velocity `1.5 m/s` ให้ scene NetworkObject แล้วหลัง `1.1 s` เดินทาง `0.0116 m`, velocity เป็น `(0,0,0)` และ Rigidbody เข้า sleep บนพื้นเรือที่ใช้ `NoFriction`
- Host impact authority: **PASS** — server-side drop changed Impact from `100.00` to `69.88` once, returned to `(0, -2.020, -3.240)`, and maintained zero Z drift with no error.
- Current holding compatibility: **PASS** — generated child MeshCollider resolved to the root NetworkObject; current weighted Cargo hold solver uses authoritative force-at-point without SpringJoint, and the previous `NetworkObjectReference` exception did not recur.
- Protection/debug regression: **PASS** — isolated initialize probe reported `IsInitialized=true`, `IsInvincible=true`, `InvincibilityRemaining=3.00`; Debug Mode defaulted OFF, targeted all Cargo controllers, and Status UI gate passed hover/debug truth-table checks.
- Host + remote client: **PENDING** — no second process/virtual player was connected in this session.
- Visual QA: **PASS** — `Assets/Docs/Plan/Features/Artifacts/CargoPrototypeScene.png` ยืนยัน Sprite 2D ในเรือ 3D และ `Assets/Docs/Plan/Features/Artifacts/CargoUIOverlayAndRotation.png` แสดง UI ที่ไม่หมุนและยังอยู่หน้า opaque 3D occluder
- Test baseline: Unity Test Runner reported 0 EditMode and 0 PlayMode tests before implementation.
- Converter dry-run: `Assets/Prefab/Cargo` reported the Cargo prefab `Rigidbody2D -> Rigidbody`; it also found an unrelated Map Navigation `BoxCollider2D`, so automatic conversion was not executed.
- Data-driven test variants: **PASS** — `CargoItemData.ValidateDefinition` ผ่านทั้ง 3 แบบ; Light `0.25 kg`/2 modules, Balanced `1.5 kg`/3 modules, Heavy `5 kg`/3 modules และทั้งหมดยังเปิด `autoSizeColliderFromSprite`.
- Scene test instances: **PASS** — `[CARGO TEST] Light Eggs`, `[CARGO TEST] Balanced Core` และ `[CARGO TEST] Heavy Nuke` มี `CargoController`, `NetworkObject`, prefab source และ data reference ถูกต้อง; วางใน `MainLevel` ที่ X `2.2`, `0`, `-2.2` ตามลำดับ

### Remaining work / deviations

- Production close-out still requires Host + remote client state/late-join verification and a remote room-transition/Z-plane pass; therefore the document is intentionally not marked fully `Implemented`.
- The implemented collider differs from the original draft's root BoxCollider: it is an explicit compound of convex triangular-prism MeshColliders under `GeneratedColliders`. This preserves the requested one-prefab workflow and follows each sprite's authored Physics Shape instead of approximating every Cargo with one box.
- SpringJoint limitation เดิมถูกแก้ภายหลังใน Feature 03 ด้วย weighted multi-holder solver; งานรอบนี้ไม่เปลี่ยน holding force model
- Centralized sprite ordering remains Feature 02 and was not implemented here.
- MainShip interior Floor/Wall/BG received kinematic child Rigidbodies because their colliders previously belonged to the frozen dynamic ship root and allowed dynamic Cargo to fall through the interior. The external ship root remains unchanged for obstacle collision behavior.
- Play Mode Start Scene was restored to `Assets/Scenes/Bootstrap.unity` after test attempts.
- A runtime-generated accidental change to `Assets/Scenes/MainLevel/MainMapConfig.asset` was detected after Play Mode and restored to its pre-work Git state.
- Unrelated dirty files detected before work were preserved: AllIn1SpriteShader Lit shaders, TMP Electronic Highway Sign material, Vivox settings, and `ProjectSettings.asset`.
