# Pressure Express — AI Implementation Guide

เอกสารในโฟลเดอร์นี้เป็น source of truth สำหรับงาน feature ที่มีการวางแผนไว้แล้ว โดยเฉพาะงานที่แก้ Unity Prefab, Physics, Networking และ visual hierarchy ซึ่งไม่ควรเริ่มจากการเดาโครงสร้างใหม่ทุกครั้ง

## เอกสารแผนปัจจุบัน

อ่านตามลำดับ dependency นี้:

1. [Cargo 2.5D Foundation](Plan/Features/01-cargo-2-5d-foundation.md) — ต้องทำก่อน เพราะเป็นฐาน Physics, runtime state และ network authority ของ Cargo
2. [Sprite Render Ordering](Plan/Features/02-sprite-render-ordering.md) — ทำหลัง Cargo มี `VisualRoot` และ Physics root แยกจากภาพแล้ว
3. [Weighted Multiplayer Holding](Plan/Features/03-weighted-multiplayer-holding.md) — ทำหลัง Cargo 2.5D เสถียร และใช้แทนระบบ `SpringJoint`
4. [Cargo Polish & Game Feel](Plan/Features/04-cargo-polish-and-game-feel.md) — core data/event/material pilot เริ่มบน prototype ปัจจุบันได้ แต่ final VFX ordering และ multiplayer acceptance ต้องเชื่อม Feature 02 และ remote validation

## สถานะล่าสุดของ Cargo Prototype

- Feature 01 อยู่สถานะ `Prototype Implemented — Remote Client Validation Pending`
- ใช้ `CargoController (new).prefab` เพียง prefab เดียวเหมือน workflow เดิม
- Cargo แต่ละชนิดกำหนดผ่าน `CargoItemData` และ module ScriptableObjects: sprite, scale, mass, ราคา, collider depth/material และ status ranges
- Sprite ต้องมี Physics Shape; ตอน initialize ระบบจะเปลี่ยน sprite ก่อน แล้วแปลง Physics Shape เป็น compound convex 3D colliders ที่มีความลึกบน Z
- `CargoItemData.physicsMaterial` ต้องระบุ explicit; Cargo ที่ใช้งานจริงใช้ `Assets/PhysicsMaterial/CargoGrip.physicMaterial` แยกจาก `NoFriction` ของพื้น เพื่อไม่ให้เกิด residual ground sliding
- Cargo status UI ยังคงเป็น World Space แต่ตาม Cargo ด้วย world offset คงที่และ world rotation เป็นศูนย์เสมอ; Image/TextMeshPro ใช้ overlay shaders ที่ `ZTest Always` และ Canvas `UI/32767` เพื่ออยู่หน้าโมเดล 3D
- Cargo protection เริ่มทำงานเมื่อ initialize authoritative state โดยให้ invincible 3 วินาที; `CargoController.GrantInvincibility()` และ `ClearInvincibility()` เปิดทางให้เพิ่ม buff ชั่วคราวในอนาคต และ damage จะไม่ถูกใช้ระหว่างช่วงคุ้มกัน
- `[DEBUG] Cargo Debug Mode` เป็น local-only gate สำหรับ Status UI ของ Cargo ทุกตัว กด `=` บนแป้นพิมพ์หลักเพื่อ toggle และค่าเริ่มต้นเป็น OFF; เมื่อเปิดแล้ว panel จะแสดงเฉพาะ Cargo ที่ local cursor กำลัง hover อยู่ และจะซ่อนเสมอเมื่อไม่ได้ hover
- ห้ามสร้าง prefab แยกต่อ Cargo เว้นแต่ Cargo นั้นต้องมี hierarchy/behavior พิเศษที่ data อธิบายไม่ได้
- Prototype ที่พร้อมทดสอบอยู่ใน `MainLevel` ชื่อ `[CARGO PROTOTYPE] Status Eggs` และเป็น scene NetworkObject ที่ลงทะเบียน prefab แล้ว
- ชุดทดสอบ data-driven 3 แบบใช้ prefab เดียวกันใน `MainLevel`: `[CARGO TEST] Light Eggs` (`0.25 kg`), `[CARGO TEST] Balanced Core` (`1.5 kg`) และ `[CARGO TEST] Heavy Nuke` (`5 kg`); แต่ละตัวใช้ sprite เดิม, collider depth/proximity ต่างกัน และชุด status modules ต่างกัน
- Data ของชุดทดสอบอยู่ที่ `Assets/Data/Cargo/Test Variants/`; ทุกตัวคง `autoSizeColliderFromSprite=true` จึงยังใช้ workflow เปลี่ยน Sprite แล้วสร้าง Physics Shape collider อัตโนมัติ
- รัน `Tools > Cargo > Validate 2.5D Cargo Prototype` เพื่อตรวจ prefab หรือรัน test assemblies `PressureExpress.Cargo.EditModeTests` / `PressureExpress.Cargo.PlayModeTests`
- Regression ล่าสุด: `PressureExpress.Cargo.EditModeTests` ผ่าน `21/21` (Cargo protection, Debug Mode, hover-gated Status UI, stage visuals, Cargo physics และ weighted holding); Host Cargo slide probe จาก `1.5 m/s` หยุดภายใน `0.0116 m` และเข้า sleep โดยไม่มี Cargo/UI exception
- ก่อนปิด Feature 01 แบบ production ต้องทดสอบ Host + remote client, late join และ room transition อีกครั้ง

## สถานะล่าสุดของ Weighted Holding Prototype

- Feature 03 อยู่สถานะ `Prototype Implemented — Remote Client Validation Pending`; เริ่มก่อน Feature 02 ตามคำสั่งผู้ใช้ และยังไม่ถือว่า production-complete
- ระบบไม่มี `SpringJoint`: Cursor/OS pointer เป็น `Cursor Intent` ส่วน Hand sprite เป็น `Actual Hand` ที่ติด Cargo-local grab point แบบแข็ง
- Client จะไม่ส่งและ server จะไม่รับ grab หาก Hand trigger ยังไม่แตะ/overlap solid generated Cargo collider; การ click Cargo จากระยะไกลอย่างเดียวจึงยกไม่ได้
- Cargo เป็น server-authoritative และรับแรง XY แยกต่อ holder ด้วย `AddForceAtPosition`; maximum force ไม่ scale ตาม mass จึงทำให้ของหนักเร่งช้ากว่า และแรงหลายคนสามารถช่วยกัน/ต้านกัน/สร้าง torque รอบ Z
- In-ship Cargo prototype ใช้ mass `0.5 kg` สำหรับทดสอบคนเดียวภายใต้ project gravity `-20 m/s²`; ห้ามใช้ค่านี้ไปทับ Cargo data ชนิดอื่น
- `PlayerHand.prefab` root scale ต้องเป็น `(1,1,1)`: นี่คือขนาด runtime เดิม (`visual ≈ 0.96 m`, collider `≈ 0.99 × 0.74 m`) หลังเลิกใช้โค้ด flip แบบแก้ transform scale ทุก frame
- Hand มี 3 presentation states โดยใช้ asset เดิม: `Cursor` แสดงกากบาทเฉพาะ owner, `Preview` แสดงรูปมือเฉพาะ owner เมื่อ hover Cargo ที่เอื้อมถึงหรือกดเมาส์ค้าง และ `Holding` แสดงรูปมือทุก peer พร้อมติด Cargo grip point แบบแข็ง; การชี้ player/พื้น/วัตถุอื่นเฉย ๆ ต้องไม่เปลี่ยนเป็นรูปมือ
- Cargo status panel ต้องผ่านสองเงื่อนไข: `[DEBUG] Cargo Debug Mode` ต้องเปิดในเครื่องนั้น และ local pointer ray ต้อง hit Cargo ภายใน `initialGrabRange`; ไม่ hover หรือ Debug OFF = ไม่แสดง และห้ามเปิด UI จากตำแหน่ง Hand ที่ถูก clamp หรือจาก player collider
- Hand collider ยังคงเป็น trigger/query-only แต่ prefab ต้องเริ่ม disabled; runtime เปิดเฉพาะตอนกดภายในระยะ, ถือจริง หรือ server ตรวจ remote Hand จึงไม่มี passive-preview collision/query response
- Hand sprite มี right-facing pose เป็นค่าเริ่มต้น `(0,0,120)`; เมื่อตำแหน่งมืออยู่ซ้ายกว่าศูนย์กลาง player ให้หมุน visual เป็น `(0,180,120)` โดยไม่อ่าน player facing direction
- เพื่อกันมือสั่น ห้ามเพิ่ม `NetworkRigidbody` กลับเข้า Hand, ห้ามเปิด Rigidbody/NetworkTransform interpolation และห้ามเพิ่ม transform writer นอก late-frame path หลังกล้อง
- Shared tuning อยู่ที่ `Assets/Data/Holding/Default Grip Configuration.asset`; รัน `Tools > Cargo > Validate Weighted Holding` หรือ test filters `WeightedHoldingEditModeTests` / `WeightedHoldingPlayModeTests`
- Host strict-contact, exact hand gap, force direction, release, stale intent และ hard reach ผ่านแล้ว; Cursor/Preview/Holding gameplay verification ส่งต่อ tester ส่วน remote client, live two-player, late join/disconnect, latency/jitter และ dedicated build ยัง pending
- ห้ามแก้ Hand/Cargo sorting แบบกระจายค่าเองใน feature นี้; ใช้ Feature Plan 02 เป็น source of truth สำหรับลำดับ Player/Cargo/Hand

## กฎสำหรับ AI ก่อนเริ่ม Implement

- อ่านไฟล์แผนของ feature ที่จะทำตั้งแต่ต้นจนจบ
- อ่าน checklist ด้านบนของไฟล์แผน และเปลี่ยน `Status` เป็น `In Progress`
- ตรวจ `git status` ก่อนแก้ ห้ามทับหรือลบงานเดิมของผู้ใช้
- ตรวจ Unity Editor ด้วย `unity status --format json`
- หากแก้ Scene, Prefab หรือ `.asset` และมี Unity Editor เชื่อมต่ออยู่ ให้แก้ผ่าน Unity Editor/Pipeline ห้ามแก้ YAML ด้วยมือ
- ใช้ `apply_patch` สำหรับไฟล์ source code และ Markdown
- ตรวจ implementation ปัจจุบันอีกครั้ง อย่าเชื่อเลขบรรทัดหรือ assumptions ในแผนโดยไม่ตรวจ source ล่าสุด
- ถ้าจำเป็นต้องเปลี่ยน architecture หรือ scope ให้แก้แผนและบันทึกเหตุผลก่อนเขียน implementation
- ทำงานตาม dependency และ phase order ที่ระบุ ห้ามเริ่ม feature 02/03 โดยข้าม acceptance gate ของ feature 01

## กฎสำหรับ Checklist

- `[ ]` หมายถึงยังไม่เสร็จหรือยังไม่ได้ยืนยัน
- `[x]` หมายถึง implement และ verify แล้วทั้งคู่
- งานที่ implement บางส่วนแต่ยังไม่ผ่าน test ต้องคงเป็น `[ ]` และเขียนเหตุผลใต้หัวข้อ `Implementation Record`
- ห้ามติ๊ก `[x]` จากการ compile ผ่านเพียงอย่างเดียว ถ้ารายการนั้นต้องใช้ PlayMode, multiplayer หรือ visual verification
- เมื่อเริ่มงาน ให้ใส่ชื่อ AI/agent, วันที่ และ branch ใน `Implementation Record`
- เมื่อจบแต่ละ work session ให้อัปเดต checklist, รายการไฟล์ที่แก้, ผลทดสอบ และงานที่เหลือ
- เมื่อ checklist ทั้งหมดผ่าน ให้เปลี่ยน `Status` เป็น `Implemented` และระบุวันที่ตรวจครั้งสุดท้าย

## Verification Minimum

ทุก feature ต้องผ่านตามความเกี่ยวข้อง:

1. C# compilation ไม่มี error
2. EditMode tests
3. PlayMode tests
4. Offline smoke test
5. Host smoke test
6. Host + remote client test
7. Prefab/Scene validation
8. Visual QA หรือ screenshot comparison สำหรับงาน rendering

ถ้า test บางประเภทไม่เกี่ยวข้อง ให้บันทึก `N/A` พร้อมเหตุผล ห้ามละเว้นเงียบ ๆ

## Unity 2.5D Project Rules

- Gameplay ใช้ 3D Physics (`Rigidbody`, `Collider`, `Physics`)
- ภาพตัวละครและ Cargo ใช้ `SpriteRenderer` ได้ตามปกติ
- Gameplay Rigidbody เคลื่อนที่บนระนาบ X/Y เท่านั้น
- Physics root ต้องไม่ใช้ Z เพื่อแก้ลำดับภาพ
- ถ้าจำเป็นต้องปรับ Z ให้ปรับเฉพาะ `VisualRoot`
- ห้ามผสม `Rigidbody2D`/`Collider2D` กับ 3D Physics บน gameplay object เดียวกัน
- Network authority ต้องมีเจ้าของการ simulation ชัดเจน ห้ามให้ทุก client คำนวณ state เดียวกันแยกกัน
- Rendering order ต้องมาจาก centralized policy ไม่กระจาย magic number ตาม SpriteRenderer หลายจุด

## Recommended Work Session Flow

```text
Read plan
  -> Audit current files and prefabs
  -> Update plan if reality changed
  -> Mark Status: In Progress
  -> Implement one phase
  -> Compile
  -> Run tests for that phase
  -> Update checklist and evidence
  -> Continue or hand off with explicit remaining work
```

## Implementation Record Format

ใช้รูปแบบนี้ที่ท้ายไฟล์แผนทุกไฟล์:

```markdown
## Implementation Record

- Status: In Progress
- Started: YYYY-MM-DD
- Updated: YYYY-MM-DD
- Agent: <name/model if known>
- Branch: <branch>

### Files changed

- `path/to/file`

### Verification evidence

- Compile: PASS/FAIL/N/A — command or note
- EditMode: PASS/FAIL/N/A — test names/result path
- PlayMode: PASS/FAIL/N/A — test names/result path
- Offline: PASS/FAIL/N/A — scenario
- Host: PASS/FAIL/N/A — scenario
- Host + Client: PASS/FAIL/N/A — scenario
- Visual QA: PASS/FAIL/N/A — screenshot/artifact path

### Remaining work / deviations

- None, or explain each unchecked item and architecture deviation.
```

## Scope Safety

- ไม่แก้ third-party assets หากไม่จำเป็น
- ไม่รัน 2D-to-3D converter ครอบทั้ง `Assets` โดยไม่มี dry-run และ path filter
- ไม่เปลี่ยน Prefab GUID หรือสร้าง replacement prefab โดยไม่จำเป็น เพราะ Scene และ NetworkPrefab list อ้างอิง GUID เดิม
- ไม่ลบ legacy component หรือไฟล์จนกว่าจะยืนยันว่าไม่มี serialized reference เหลืออยู่
- การเปลี่ยน Network authority ต้องทดสอบ dedicated-server-compatible flow ถึงแม้การทดสอบหลักจะใช้ Host
