# Feature Plan 03 — ระบบจับติดแน่นและแรงดึงตามน้ำหนักแบบ Multiplayer

Status: **Prototype Implemented — Cursor/Hand UX + Remote Client Validation Pending**
Priority: **ยืนยัน Remote Client/Multiplayer และเชื่อม Sprite Ordering หลัง Feature Plan 02**
Depends on: `01-cargo-2-5d-foundation.md`, `02-sprite-render-ordering.md`
Last plan update: 2026-08-26

> AI instruction: ก่อน implement ให้เปลี่ยน Status เป็น `In Progress` และกรอก `Implementation Record` ท้ายไฟล์ หลังจบแต่ละ phase ต้องอัปเดต checklist, tuning values, changed files และผลทดสอบ ห้ามติ๊กงานที่ทดสอบเฉพาะ compile หากหัวข้อนั้นต้องพิสูจน์ด้วย PlayMode/Multiplayer

## Master Checklist

### ก่อนเริ่มงาน

- [x] อ่าน `Assets/Docs/README.md` และแผนนี้ครบทั้งไฟล์
- [x] ยืนยันว่า dependency ทั้งสองแผนผ่าน acceptance gate แล้ว หรือบันทึกเหตุผลหากต้องเริ่มก่อน
- [x] เปลี่ยน Status เป็น `In Progress` และลงวันที่ใน Implementation Record
- [x] บันทึก `git status` และห้ามทับ unrelated changes ของผู้ใช้
- [x] Audit grab/hold/release scripts, SpringJoint, hand physics, cursor input, RPC, ownership, cargo mass data และ disconnect cleanup ทั้งหมด
- [ ] วัด baseline wobble, hand-to-grip gap, latency, release velocity และ multi-player behavior ก่อนแก้ — Host baseline บันทึกแล้ว; remote latency/multiplayer baseline ยัง pending
- [x] ยืนยัน input devices, จำนวนผู้ถือสูงสุดต่อ Cargo, จำนวนผู้เล่น, tick rate และ dedicated-server requirement

### Core architecture

- [x] แยก `Cursor Intent` ออกจาก `Actual Hand` อย่างชัดเจน
- [x] กำหนด server-authoritative hold state ที่มี cargo ID, player/hand ID, local grab point, version และ tick/sequence
- [x] แทน SpringJoint ด้วยการติด Actual Hand เข้ากับ grab point แบบแข็ง 100%
- [x] Implement force-limited cargo solver จาก cursor error, point-velocity damping และ grip strength โดยไม่ scale แรงตาม Cargo mass
- [x] Apply แรงของแต่ละ holder ที่ grab point ของตัวเอง เพื่อให้ช่วยกัน, ต้านกัน และสร้าง torque ได้
- [x] บังคับ force, point, velocity และ rotation ให้อยู่ใน XY plane; หมุนได้เฉพาะแกน Z
- [x] กำหนด grab range, strict collider-contact gate, soft/hard reach, break/release และ invalid-state rules
- [x] กำหนด collision policy: เมื่อ Hand collider ถูก arm ต้องเป็น trigger/query-only จึงตรวจ overlap ได้แต่ไม่ผลัก Cargo; passive preview ปิด collider
- [x] กำหนด presentation state: free Hand เห็นเฉพาะ owner; held Hand เห็นทุก peer และติด Cargo grip point
- [x] กำจัด idle-hand shake จาก physics/network/camera timing โดยเหลือ position writer เดียวหลัง camera LateUpdate

### Networking และ prediction

- [ ] Server validate grab request จาก distance, room/state, target, holder capacity และ permission — owner/permission, spawned target, layer, initialized Cargo, strict Hand contact, player distance และ capacity เสร็จแล้ว; project-specific room/death-state hook ยังไม่มี
- [x] ส่ง Cursor Intent ด้วย bounded rate, quantization และ change threshold; ห้ามส่ง transform ทุก render frame
- [x] Grab/release transition เป็น reliable, owner-only, sequence/versioned และ idempotent
- [x] Cargo Rigidbody เป็น server authoritative; client ห้าม set Cargo transform โดยตรง
- [x] แสดง local intent ทันทีและ resolve Actual Hand จาก authoritative/interpolated Cargo โดยไม่ให้ client ครอบครอง Cargo physics
- [ ] รองรับ host, remote client, simultaneous grab, late join, disconnect, despawn และ room transition — Host และ cleanup code ผ่าน; remote/live simultaneous/late join/disconnect ยัง pending
- [x] Runtime authoritative path ไม่พึ่ง camera, cursor, renderer หรือ local input; dedicated-server build verification ยัง pending

### Tuning และ migration

- [x] เพิ่ม shared grip configuration สำหรับ force, damping, reach, update rate และ presentation
- [x] ล็อก scope เรื่อง player body reaction; v1 ให้ Cargo ดึง Actual Hand แต่ยังไม่ดึงตัวผู้เล่น
- [x] ลบ SpringJoint creation/configuration และ joint tuning fields ที่ไม่ใช้แล้ว
- [x] Migrate Player Hand/Cargo prefab ผ่าน Unity MCP และยืนยัน scene instance ในเรือใช้ prefab ที่แก้แล้ว
- [x] เพิ่ม debug view ของ Cursor Intent, Actual Hand/grab point, force vector, clamp และ holder state
- [x] เพิ่ม runtime/editor validator สำหรับ grip setup ที่ผิด

### Verification

- [x] Unity compile ผ่านโดยไม่มี error ใหม่จาก feature นี้
- [x] Unit/EditMode tests ของ force calculation, clamp, damping, contact gate, visibility states, hand-size regression, prototype lift margin และ planar projection ผ่าน 10/10
- [ ] PlayMode tests ของ grab, hold, pull, release, break, despawn และ reconnect ผ่าน — force/rigid-grip/query-only collision tests 4/4 และ Host lifecycle probes ผ่าน; reconnect/remote lifecycle ยัง pending
- [ ] Test Cargo เบา/กลาง/หนักด้วย grip force เดียวกัน — light/heavy automated comparison ผ่าน; medium/live tuning ยัง pending
- [ ] Test ผู้เล่นสองคนช่วยดึงทางเดียวกัน — independent-force math ผ่าน; live two-player pending
- [ ] Test ผู้เล่นสองคนดึงสวนทางกัน — cancellation math/PlayMode Rigidbody ผ่าน; live two-player pending
- [x] Test การดึงนอกศูนย์ที่ทำให้เกิดเฉพาะ torque รอบ Z ผ่าน PlayMode
- [x] Actual Hand ติด grab point แบบไม่มี SpringJoint wiggle หรือช่องว่าง; Host วัด gap `0.00000000 m`
- [x] Cargo weight/environment resistance ทำให้ Actual Hand อยู่กับ Cargo และแยกจาก Cursor Intent ได้; Host เห็น error/force clamp จริง
- [x] Release รักษา velocity และ Host probe วัด velocity delta `0`
- [ ] Test host + remote client ภายใต้ latency/jitter/packet loss จำลอง
- [x] Host probe และ automated tests ไม่พบ Z position/velocity หรือ X/Y rotation drift
- [x] กรอก Implementation Record, tuning, tests และ evidence ของ prototype ครบ
- [ ] เปลี่ยน Status เป็น `Complete` เมื่อ acceptance criteria ผ่านครบเท่านั้น

### Cursor / Hand UX follow-up — 2026-08-26

- [x] Cargo info panel แสดงเฉพาะ local cursor ที่ ray hit Cargo ภายใน `initialGrabRange`
- [x] Cursor sprite เปลี่ยนเป็น Hand preview เฉพาะเมื่อ hover Cargo ที่เอื้อมถึง โดยยังไม่เปิด Hand collider; ชี้ player/พื้น/วัตถุอื่นต้องคงเป็น Cursor
- [x] การกดเมาส์ค้างที่ตำแหน่งใดก็แสดง Hand preview แต่ตำแหน่งนอกระยะต้องไม่ query/grab Cargo
- [x] Hand ใช้ right-facing pose เป็นค่าเริ่มต้นและ mirror ซ้าย/ขวาจากตำแหน่งเทียบ player center โดยไม่อิง player facing
- [x] Hand collider เป็น trigger/query-only และเปิดเฉพาะตอนกดภายในระยะ, ตอนถือจริง หรือ server ตรวจ remote Hand
- [x] คง strict-contact grab gate, rigid held-hand attachment และ server-authoritative force solver เดิม
- [x] Cargo Status UI debug gate เป็น local-only: ปุ่ม `=` เปิด/ปิด gate ของ Cargo ทุกตัว แต่ panel แสดงเฉพาะตอน local pointer hover Cargo
- [x] Unity compile และ Weighted Holding validator ผ่านหลังแก้
- [ ] Tester ทวน state transition, Cargo-only hover, left/right orientation, no-action preview, collider arming และ multiplayer visibility ใน PlayMode

## 1. เป้าหมาย

แทนระบบถือของที่ใช้ SpringJoint ด้วย grip ที่ภาพนิ่งและติดแน่น:

- ระหว่างถือ **Actual Hand ต้องอยู่ตรง Cargo grab point 100%** ไม่มี joint stretch หรือ wiggle
- Mouse/controller กำหนด **Cursor Intent** หรือจุดที่ผู้เล่นต้องการให้มือไป ไม่ใช่ตำแหน่งมือจริง
- ระยะระหว่าง Cursor Intent กับ Actual Hand สร้างแรงดึง Cargo ที่มีเพดาน
- Cargo หนักเร่งช้ากว่า Cargo เบาเมื่อใช้ grip force เท่ากัน
- แรงของผู้เล่นหลายคนรวมกันตามฟิสิกส์: ช่วยกันดึง, ดึงสวนกัน หรือสร้าง torque จากคนละจุด
- Collision และ external force สามารถขวาง Cargo จึงทำให้ Actual Hand อยู่ห่างจาก Cursor Intent ได้

ความรู้สึกที่ต้องได้คือ ผู้เล่นสั่ง “อยากให้มือไปที่ไหน” แต่ตำแหน่งจริงของ Cargo เป็นผู้กำหนดว่ามือที่จับอยู่ไปได้ไกลแค่ไหน

## 2. Behavior Contract

### 2.1 ต้องมีสองตำแหน่ง

ผู้เล่นแต่ละคนมี:

1. **Cursor Intent** — input target บน XY gameplay plane
2. **Actual Hand** — grip point จริงที่ตาม Cargo ระหว่างถือ

เมื่อไม่ได้ถือของ Hand ใช้ free-hand behavior เดิมได้ แต่เมื่อถือ:

```text
ActualHandWorldPosition = Cargo.TransformPoint(LocalGrabPoint)
```

Actual Hand ห้ามไล่ตาม Cursor ผ่าน physics joint อีกต่อไป แต่ต้องตาม authoritative/interpolated Cargo grip point โดยตรง

### 2.2 การสร้างแรง

สมการตั้งต้นที่แนะนำ:

```text
error           = ProjectToXY(CursorIntent - ActualHand)
desiredVelocity = ClampMagnitude(error * positionGain, maxIntentSpeed)
pointVelocity   = Cargo.GetPointVelocity(ActualHand)
force           = (desiredVelocity - pointVelocity) * velocityGain
force           = ClampMagnitude(force, maxGripForce)
```

Apply ด้วย `Rigidbody.AddForceAtPosition(force, ActualHand, ForceMode.Force)` หรือ solver ที่เทียบเท่าและทำงานฝั่ง authoritative server

คุณสมบัติที่ห้ามเสีย:

- ห้ามคูณ maximum grip force ด้วย Cargo mass เพราะแรงคงที่คือสิ่งที่ทำให้ของหนักเร่งช้ากว่า
- Damping ใช้ point velocity เพื่อให้ off-center grip เสถียรตอน Cargo หมุน
- Clamp error, desired speed และ force เพื่อป้องกัน cursor jump/network correction สร้างแรงระเบิด
- Project vector ทุกตัวลง XY และตัด Z ออกอย่างชัดเจน
- Cargo หมุนได้เฉพาะ Z; freeze X/Y rotation

ค่า gain เป็น tuning ไม่ใช่ architecture ถ้าใช้ controller แบบอื่นต้องบันทึกสมการและพิสูจน์ว่ายังรักษาพฤติกรรมทั้งหมด

### 2.3 ผู้ถือหลายคน

Authoritative Cargo solver ประเมิน holder ทุกคนใน physics tick เดียวกัน:

```text
for each valid holder:
    force = CalculateHolderForce(holder, cargo)
    cargoRigidbody.AddForceAtPosition(force, holder.grabPoint)
```

ผลที่ต้องเกิด:

- **ช่วยกัน:** แรงทิศเดียวกันรวมกัน
- **ต้านกัน:** แรงสวนทางหักล้างบางส่วนหรือทั้งหมด
- **หมุน:** แรงคนละตำแหน่งสร้าง torque รอบ Z
- **น้ำหนัก:** acceleration = total force / mass ตาม Rigidbody
- **External influence:** collision, conveyor, explosion หรือ scripted force ยังมีผลปกติ

ห้ามเฉลี่ย Cursor Intent ทุกคนเป็นเป้าหมายเดียว เพราะจะทำลายพฤติกรรมดึงสวนและ torque

### 2.4 ความหมายของ “ติด 100%”

สิ่งที่ติดแข็งคือ **Actual Hand กับ Cargo grab point** ไม่ใช่ Cargo กับ Cursor Intent

- หลัง interpolation/presentation แล้วต้องไม่มีช่องว่างระหว่างมือกับ grip point
- Cursor Intent เคลื่อนไปไกลกว่ามือได้เพื่อแสดงทิศแรงที่ต้องการ
- ห้ามบังคับเลื่อน operating-system cursor; หากต้องการให้ซ่อนแล้ววาด custom intent marker
- สามารถมี tension line/force indicator ได้ แต่ต้องเป็น visual-only

## 3. สิ่งที่ไม่อยู่ใน scope

- ไม่ใช้ SpringJoint/ConfigurableJoint softness เพื่อยึดมือกับ Cargo
- ไม่โอน client ownership ของ Cargo เพียงเพราะ client จับของ
- ไม่ teleport Cargo ไป Cursor Intent
- ไม่ให้เคลื่อนที่แกน Z หรือหมุนแกน X/Y
- ไม่ redesign player locomotion ใน v1
- v1 ให้ Cargo ดึง Actual Hand ออกจาก Cursor Intent แต่ยังไม่ดึง player body; body reaction แยกเป็น feature ภายหลัง
- ไม่แก้ responsiveness ด้วยการเชื่อ client transform โดยไม่มี validation

## 4. ปัญหาปัจจุบันที่ต้อง Audit

- Physics joint ทำให้เกิด stretch, overshoot และ wobble
- Hand/Cargo อาจใช้ physics dimension หรือ authority model คนละแบบหลัง migration 2D → 3D
- Joint break/recreate อาจทำให้ impulse spike, stale reference และ release state ไม่ตรงกัน
- Local input/dictionary อาจไม่ replicate ไปทุก peer
- Host-only assumptions อาจพังบน remote client หรือ dedicated server
- Hand collider อาจชน held Cargo ของตัวเองและป้อนแรงย้อนกลับ
- NetworkTransform correction อาจต่อสู้กับ local joint simulation

ตอน implement ต้องระบุ script, prefab, RPC path, ownership และ cleanup ที่พบจริงใน Implementation Record

## 5. Target Architecture

### `CursorIntentProvider`

- อ่าน mouse/controller เฉพาะ local owning player
- แปลง screen input เป็นจุดบน fixed gameplay plane
- จำกัด bounds/filter ตาม gameplay rule
- ส่ง intent แบบ bounded ไป Network Grab Coordinator
- ไม่มีสิทธิ์ขยับ Cargo โดยตรง

### `PlayerHandController`

- ดูแล free-hand presentation ตอนยังไม่ถือ
- ระหว่างถือให้ visible hand ตาม resolved Cargo grab point
- ใช้ interpolation/prediction เพื่อภาพลื่น แต่ไม่สร้าง physics joint คู่แข่ง
- เปิด Actual Hand/Cursor Intent ให้ debug/UI อ่านแบบ read-only

### `NetworkGrabCoordinator`

- รับ grab/release request
- Validate ฝั่ง server
- สร้าง, version, replicate และลบ holder record
- Cleanup ตอน disconnect, despawn, death, cargo destruction และ room transition
- รองรับ repeated/out-of-order message แบบ idempotent

### `CargoHoldSolver`

- รันเฉพาะ authoritative Cargo simulation
- วน valid holders ทุก FixedUpdate/network physics tick
- คำนวณแรงแต่ละคนแยกกัน
- Apply force ที่ grab point และ enforce planar invariant
- ไม่อ่าน local input device และไม่ render

### `GripConfiguration`

Configuration กลางควรมี:

- Position gain
- Velocity gain/damping
- Maximum grip force ต่อ holder
- Maximum intent speed
- Soft reach และ hard break/clamp distance
- Grab distance และ cooldown
- Intent send rate/change threshold/quantization
- Prediction/interpolation settings
- Release/re-grab policy

Cargo-specific modifier มาจาก `CargoItemData` ได้ แต่ต้องระบุ stacking rule และ source of truth ชัดเจน

## 6. Replicated Holder State

ข้อมูลขั้นต่ำเชิงแนวคิด:

```text
CargoNetworkId
PlayerNetworkId
HandId
LocalGrabPointXY
LatestCursorIntentXY
StateVersion
LastAcceptedInputSequenceOrTick
GripState
```

กฎ:

- Local grab point เป็น authoritative หลัง server validate request แรก
- Cursor Intent เป็น untrusted input: ตรวจ finite, bounds, rate, sequence และ reach
- Attach/detach ใช้ reliable delivery และ idempotent transition
- Intent ความถี่สูงใช้ unreliable sequenced ได้ โดย server เก็บค่าล่าสุดที่ valid
- Late join รับ holder list และ visual attachment state ปัจจุบัน
- ห้ามใช้ local-only dictionary เป็น truth เพียงแห่งเดียว

## 7. Data Flow

```text
Local input
   ↓
Cursor Intent Provider
   ↓ bounded/quantized intent
Server Grab Coordinator ── validate attach/release และ holder state
   ↓
Authoritative Cargo Hold Solver
   ↓ force ต่อ holder ณ local grab point
Cargo Rigidbody simulation
   ↓ network state
ทุก client interpolate Cargo
   ↓
Actual Hand ติดกับ interpolated Cargo grab point
```

Local client predict intent marker/hand presentation ได้ แต่ authoritative Cargo transform ต้องมาจาก server simulation เสมอ

## 8. Grab / Hold / Release Lifecycle

### Grab request

1. Local player เลือก Cargo และ candidate point
2. Client ส่ง Cargo ID, Hand ID, candidate point และ request sequence
3. Server validate player/cargo state, room, layer, range, holder capacity, line-of-sight หากต้องใช้ และ XY finite
4. Server project/clamp จุดลง Cargo collider แล้วเก็บเป็น Cargo-local XY
5. Server publish holder record/version
6. ทุก client ติด Actual Hand ของผู้เล่นนั้นเข้ากับ grip point

### Hold tick

1. Server อ่าน latest valid Cursor Intent ของแต่ละ holder
2. Clamp/timeout/release invalid หรือ stale intent ตาม policy
3. Solver คำนวณแรงแยกและ apply ณ grab point
4. Cargo 2.5D constraint ตัด Z position/velocity และ X/Y rotation drift
5. Network replicate Cargo; client resolve hand visual จาก Cargo state

### Release

1. Client ขอ release หรือ server trigger จาก break/invalid state
2. Server ลบ holder หนึ่งครั้งและ replicate version ใหม่
3. Cargo รักษา linear/angular velocity ปัจจุบัน; ไม่เพิ่ม release impulse หาก design ไม่ได้ระบุ
4. Hand กลับ free-hand behavior โดยไม่ teleport Cargo
5. ล้าง indicator/debug state

### Forced cleanup

ต้อง release holder ที่เกี่ยวข้องเมื่อ:

- Player disconnect/despawn/death
- Cargo despawn/destruction
- Room/scene transition
- Hand component disable
- Rigidbody invalid หรือ authority loss
- Server timeout ตาม policy

Cleanup ต้องเรียกซ้ำได้อย่างปลอดภัยแม้อีก object ถูกทำลายแล้ว

## 9. Collision และ Physics Policy

- Cargo ใช้ authoritative dynamic 3D Rigidbody/Collider จาก Cargo 2.5D plan
- Actual Hand เป็น kinematic visual/query body ทั้งตอนว่างและตอนถือ ไม่ใช่ dynamic body ที่ joint กับ Cargo
- Hand collider เป็น trigger/query-only ทุกครั้งที่เปิด: owner เปิดตอน click+in-range หรือ holding, server เปิด remote validation probe; passive Cursor/Preview ปิด collider และไม่มี collision response
- ห้ามใช้ `Physics.IgnoreCollision` กับ Hand/Cargo เพราะจะปิด trigger interaction ที่ strict contact gate ต้องใช้
- Hand query และ Cargo force ใช้ 3D physics ให้สอดคล้องกัน
- Cargo/environment collision อื่นยังมีผลปกติ
- Cargo mass, drag, angular drag และ center of mass ยังเป็น simulation input ที่มีความหมาย
- Enforce Rigidbody constraints และ planar safeguard หลัง simulation/network correction เมื่อจำเป็น
- ห้ามเขียน transform โดยตรงบน authoritative non-kinematic Rigidbody ระหว่างถือปกติ

## 10. Networking และ Responsiveness

### Authority

- Server เป็นเจ้าของ Cargo Rigidbody และ holder truth
- Owning client เป็นเจ้าของ input collection กับ immediate visual feedback เท่านั้น
- Grab ไม่ให้ arbitrary transform/force authority แก่ client
- Server ตรวจ room membership, distance และ Cargo state ก่อนรับ input

### Intent transport

ค่าเริ่มต้นสำหรับ tuning อาจเป็น 20–30 Hz แล้ววัดจริง:

- ส่งเมื่อ intent เปลี่ยนเกิน threshold หรือถึง keepalive
- Quantize XY ตาม gameplay area/player-relative range
- Reject NaN/infinity และ clamp world bounds
- ใช้ sequence/tick ที่เพิ่มขึ้นเสมอและ ignore sample เก่า
- Interpolate/extrapolate ช่วงสั้นบน server พร้อม strict timeout เพื่อลด sawtooth จาก jitter

ค่าดังกล่าวเป็น hypothesis ต้องบันทึกค่าจริงจาก profiler/test ใน Implementation Record

### Client presentation

- แสดง local Cursor Intent ทันที
- Resolve Actual Hand จาก predicted/interpolated Cargo grip point
- Correct prediction ให้ลื่น แต่ final hand-to-grip gap ต้องเป็นศูนย์
- Remote hand ใช้ Cargo interpolation + holder data ไม่ replay remote OS cursor
- Tension line ใช้ visual endpoints เท่านั้น

## 11. Reach และ Failure Rules

ต้องกำหนดค่าจริงระหว่าง implement:

- **Grab range:** ระยะสูงสุดตอนเริ่มจับ
- **Soft reach:** ช่วง error ที่ force scale ปกติ
- **Hard reach:** จุด clamp intent หรือ release grip
- **Break policy:** server rule ที่ deterministic ไม่อิง joint break force
- **Stale input timeout:** เมื่อไม่ได้ intent update
- **Maximum holders:** จำกัดและ validate ต่อ Cargo
- **Blocked/embedded recovery:** วิธีแก้เมื่อเริ่มใน geometry ผิดปกติ

Policy v1 ที่แนะนำ: clamp intent ที่ hard reach และ release หลัง grace period สั้น ๆ หรือ invalid player state เพื่อไม่ให้ network correction ครั้งเดียวทำของหลุด แต่ยังป้องกัน infinite reach/force

## 12. Debugging และ Validation

Development-only visualization ต่อ holder:

- Cursor Intent
- Actual Hand/grab point
- Error vector
- Applied force vector และสถานะ clamp
- Holder ID, sequence/tick age และ grip state
- Cargo mass, velocity, angular velocity และ planar drift

Validator ต้องรายงาน:

- Cargo ไม่มี authoritative Rigidbody/Collider
- Hand ยังใช้ dynamic joint ขณะ rigid grip
- SpringJoint/legacy holding component ยัง active
- Layer collision policy ผิด
- Physics root Z ไม่เป็นศูนย์หรือ Rigidbody axes ผิด
- Network coordinator/authority reference หาย
- Holder ID ซ้ำหรือ local grab point เป็นไปไม่ได้

Debug rendering ต้องปิดได้และ dedicated server ต้องไม่พึ่งมัน

## 13. ขั้นตอน Implementation

### Phase A — Audit และ baseline

1. Trace input → hand → joint → Cargo → network flow เดิม
2. บันทึก joint settings, ownership, collision filter และ cleanup paths
3. สร้าง reproducible test สำหรับ wobble, heavy cargo, opposing players, release และ disconnect
4. วัด baseline gap/oscillation/network behavior
5. ล็อก v1 body-reaction scope และ holder count

**Exit gate:** ทำ failure เดิมซ้ำได้และ document authority path ครบ

### Phase B — State และ solver แบบแยกส่วน

1. เพิ่ม shared grip config และ pure force calculation
2. Unit test zero error, clamp, damping, mass response, point velocity และ planar projection
3. เพิ่ม holder record และ authoritative solver โดยยังไม่ migrate visual
4. Test synthetic one/multiple-holder inputs กับ Rigidbody ทดสอบ
5. ยืนยันแรง add/cancel และ torque ถูกต้อง

**Exit gate:** Automated tests พิสูจน์ physical properties โดยไม่ต้องใช้ live cursor

### Phase C — Grab networking

1. เพิ่ม validated/idempotent grab/release request
2. Replicate holder state และ local grab points
3. เพิ่ม bounded intent transport และ stale-input handling
4. เพิ่ม disconnect/despawn/room cleanup
5. Test host, remote, late join และ dedicated server

**Exit gate:** Holder truth ตรงกันทุก peer และ lifecycle cases ผ่าน

### Phase D — Hand และ prefab migration

1. แยก Cursor Intent จาก Actual Hand ใน Hand code
2. Attach Actual Hand presentation กับ Cargo grip point
3. ลบ SpringJoint creation, break handling และ obsolete fields
4. ตั้ง hand/cargo collision filtering และ prefab references ผ่าน Unity Editor/Unity CLI
5. เชื่อม Hand sorting จาก Feature Plan 02
6. เพิ่ม prediction/interpolation และ optional tension visual

**Exit gate:** Hand-to-grip gap เป็นศูนย์และไม่มี legacy joint ขับ held Cargo

### Phase E — Tuning และ full verification

1. Tune force/damping/reach กับ Cargo เบา/กลาง/หนัก
2. Test cooperative, opposing และ off-center two-player pulls
3. จำลอง latency, jitter และ packet loss
4. Profile server physics/network traffic ที่จำนวน player/holder สูงสุด
5. Regression test collision, room transition, release velocity และ 2.5D constraints
6. กรอก Implementation Record ด้วยสมการ/ค่าจริง/evidence

**Exit gate:** Acceptance criteria และ Test Matrix ผ่านบน host กับ remote client

## 14. Test Matrix

| กรณี | ผลที่ต้องได้ |
|---|---|
| จับ Cargo เบาที่หยุดนิ่ง | Hand ติดโดยไม่มีช่องว่าง และ Cargo ตาม intent อย่างลื่น |
| Input เดิมกับ Cargo หนัก | Cargo หนักเร่งช้ากว่า; Hand อยู่ที่ grip point และตาม Cursor Intent ไม่ทันได้ |
| Cursor หยุด | Damping ทำให้ Cargo settle โดยไม่ oscillate ต่อเนื่อง |
| Cargo ชนกำแพง | Cargo หยุด/ตอบสนองตามฟิสิกส์; Hand ยังติดและ Cursor ดึงต่อได้ |
| ผู้เล่นหนึ่งคนดึงนอกศูนย์ | Cargo เคลื่อนและหมุนเฉพาะรอบ Z |
| สองผู้เล่นดึงทิศเดียวกัน | แรงรวมกันและ Cargo ตอบสนองมากขึ้น |
| สองผู้เล่นดึงสวนกัน | แรงหักล้าง ไม่มี averaged-target teleport หรือ joint explosion |
| สองผู้เล่นดึงคนละจุด | เกิด torque ที่คาดไว้และมือทั้งสองติด grip point ตัวเอง |
| Release ระหว่างเคลื่อน | Cargo รักษา velocity และไม่มี spike |
| Holder disconnect | Force และ visual grip ถูกลบหนึ่งครั้ง |
| Cargo despawn ขณะถูกถือ | Hand ทุกคนกลับ free state ไม่มี stale reference |
| Late join | Client ใหม่รับ Cargo/holder state และมืออยู่ถูกจุด |
| Packet loss/jitter | Input bounded/recoverable และไม่มี correction ระเบิด |
| Intent ผิดรูป/ไกลเกิน | Server reject/clamp โดย holder state ไม่เสีย |
| ถือเป็นเวลานาน | ไม่มี Z drift, X/Y rotation drift, holder leak หรือ jitter เพิ่มขึ้น |

## 15. Acceptance Criteria

- ไม่มี SpringJoint หรือ soft positional joint ขับ hand-to-Cargo attachment
- Actual Hand ซ้อนตรง Cargo grab point ระหว่างถือ
- Cursor Intent แยกจากมือจริงและถูก Cargo weight, collision หรือผู้เล่นอื่นดึงห่างได้
- Grip force เท่ากันทำให้ Cargo หนักเร่งน้อยกว่า Cargo เบา
- Holder หลายคน apply แรงอิสระที่จุดของตน รองรับช่วยกัน, ต้านกัน และ torque
- Cargo เป็น server authoritative และ client ตั้ง transform โดยตรงไม่ได้
- Grab/release/cleanup ตรงกันบน host, remote, late join, disconnect, despawn และ room transition
- Release ไม่มี one-frame force/velocity spike
- Cargo เคลื่อน Position/Velocity เฉพาะ XY และ Rotation เฉพาะ Z
- Local interaction responsive ตาม latency target และ correction สุดท้ายยังคง hand-to-grip gap = 0
- Legacy joint code/fields และ collision conflict ถูกลบหรือปิดชัดเจน
- Automated tests และ Test Matrix ผ่านพร้อม evidence

## 16. ความเสี่ยงและ Rollback

| ความเสี่ยง | วิธีลดความเสี่ยง |
|---|---|
| Gain สูงทำให้ oscillate แบบใหม่ | ใช้ velocity damping, force/speed clamp และ fixed-timestep tests |
| Client รู้สึกหน่วง | Predict เฉพาะ presentation/intent marker; Cargo truth ยังอยู่ server |
| สอง holder สร้าง torque มากเกิน | Clamp force ต่อ holder, tune angular drag แต่รักษา force-at-point behavior |
| Hand collider ตีกับ held Cargo | ใช้ contact/layer policy ชัดเจนและ restore ตอน release |
| Stale input ออกแรงตลอด | Sequence input และกำหนด server timeout/release policy |
| Disconnect ทิ้ง ghost force | Server-owned holder records cleanup ทุก lifecycle แบบ idempotent |
| Solver ทำให้น้ำหนักไม่มีผล | Maximum force ไม่ scale ตาม mass และ test หลายมวลด้วย input เดียวกัน |

ระหว่าง migration อาจเก็บระบบเก่าหลัง temporary feature switch ใน branch งานเท่านั้น เมื่อตรวจผ่านแล้วให้ลบ switch Rollback เฉพาะไฟล์ feature นี้ ห้าม revert Cargo 2.5D หรือ Sprite Ordering foundation

## 17. Expected Files/Assets to Touch

ต้องยืนยัน path จริงระหว่าง audit ก่อนแก้:

- Player Hand controller, input, network และ prefab
- Holding/grab/joint scripts เดิม
- Cargo controller/physics integration และ Cargo prefabs
- Player/Cargo network spawn และ lifecycle code
- Cargo item physics/grip data assets
- Physics layer/contact settings ผ่าน Unity-supported editing
- Grip config, coordinator, solver, debug, validator และ tests ใหม่
- Test scene/prefab สำหรับ multiplayer และ force tuning

## 18. Implementation Record

### Work session

- **Started:** 2026-08-25
- **Prototype implementation completed:** 2026-08-25
- **User-feedback tuning:** 2026-08-25 — restore legacy Hand size และลด in-ship prototype mass ให้หนึ่งคนยกทดสอบได้
- **User-feedback tuning:** 2026-08-25 — แก้ idle-hand shake, free/held visibility และเปลี่ยน Hand เป็น trigger/query-only ตลอดเวลา
- **Cursor/Hand UX follow-up started:** 2026-08-26 — จำกัด hover ด้วย raw cursor reach และเพิ่ม Cursor/Preview/Holding presentation states; gameplay verification ส่งต่อ tester ตามคำสั่งผู้ใช้
- **Cursor/Hand UX correction:** 2026-08-26 — Preview ต้องเกิดจาก Cargo hover เท่านั้น ไม่ใช่เพียง cursor อยู่ใกล้ player; interaction reach ใช้ `initialGrabRange` และ Hand เลือก orientation จากตำแหน่งเทียบ player center
- **Cargo Status UI correction:** 2026-08-26 — Debug Mode เป็น gate local ของ Cargo ทุกตัว; `=` เปิด gate แต่ไม่บังคับให้ panel ค้างอยู่ และ panel ต้องซ่อนทันทีเมื่อ pointer ไม่ hover
- **Implementer:** Codex + Unity MCP
- **Current status:** Prototype Implemented — Cursor/Hand UX + Remote Client Validation Pending

### Audited legacy system

- Current grab/hold scripts: `CargoGrabController` raycast/RPC/client-side SpringJoint และ `PlayerHand` owner-driven kinematic cursor follower
- Current joint setup: SpringJoint ถูกเพิ่มบน Cargo ทุก peer; connectedBody คือ Hand kinematic; spring/damper คำนวณจาก cargo mass; maxDistance 0.1 m
- Current authority/input flow: Mouse only; client เลือก target แล้ว RPC โดย server ไม่ตรวจ contact/range และไม่ได้เก็บ holder truth; ClientRpc เป็นผู้สร้าง/ลบ joint
- Legacy baseline collision policy: Hand layer `GroundForObject` เคยเป็น solid BoxCollider และชน Cargo layer `Object`; ไม่มี per-holder collision suppression/restore
- Baseline wobble/latency measurements: Host baseline รับ grab ที่ระยะ Hand–Cargo 4.5826 m โดย Hand ไม่แตะ; เมื่อมีสิ่งกีดขวาง joint ค้างด้วย gap 3.5628 m และลาก Cargo จาก `(0,-2.02)` ไปประมาณ `(-2.43,-6.83)`; release จากสภาวะหยุดไม่มี velocity spike. Remote latency/jitter/multiplayer baseline ยังไม่ได้วัด

### Final decisions and tuning

- Solver equation/implementation: Server `CargoHoldSolver.FixedUpdate` คำนวณต่อ holder ด้วย `error = ClampXY(intent - gripPoint, hardReach)`, `desiredVelocity = Clamp(error * positionGain, maxIntentSpeed)`, `force = Clamp((desiredVelocity - Cargo.GetPointVelocity(gripPoint)) * velocityGain, maxGripForce)` แล้วใช้ `Rigidbody.AddForceAtPosition(..., ForceMode.Force)`; ไม่มี mass multiplier และไม่มี joint
- Position gain: `3`
- Velocity gain/damping: `20` โดย damping ใช้ point velocity ณ grab point
- Maximum grip force: `60 N` ต่อ holder. Project gravity คือ `-20 m/s²`; ดังนั้น test Cargo `1 kg` หนัก `20 N` และเหลือแรงยกขึ้นสูงสุด `40 N`. Cargo `3 kg` อยู่ใกล้จุดสมดุลของหนึ่งมือ และ Cargo หนักกว่านั้นต้องอาศัยแรงผู้เล่นหลายคนหรือแรงภายนอก
- Maximum intent speed: `3 m/s`
- Grab/contact/reach: free-hand radius `1.0 m`, initial player-to-contact range `1.6 m`, ต้องให้ query-only Hand trigger แตะ/overlap solid generated Cargo collider ภายใน tolerance `0.035 m`, soft reach `0.75 m`, hard reach `1.75 m`
- Input transport: owner ส่ง intent สูงสุด `25 Hz`, keepalive `0.2 s`, change threshold `0.01 m`, quantization `0.01 m`, world bound `±1000 m`; intent RPC เป็น unreliable sequenced และ grab/release RPC เป็น reliable owner-only
- Stale input/break policy: release เมื่อ accepted intent เก่ากว่า `0.8 s`; เกิน hard reach ต่อเนื่อง `0.45 s` จึง release; invalid Hand, Player/Cargo despawn และ component disable cleanup แบบเรียกซ้ำได้
- Maximum holders: 4 ต่อ Cargo (ตรงกับ SessionService max players 4)
- Body reaction scope: v1 ดึง Actual Hand ออกจาก Cursor Intent เท่านั้น ไม่ดึง Player body
- Contact/range rule: client ไม่ส่ง grab request และ server ไม่รับ request ถ้า Hand ไม่แตะ Cargo จริง; การ click/raycast โดน Cargo จากระยะไกลอย่างเดียวจึงยกไม่ได้
- Hand size compatibility: legacy `PlayerHand` เคยเขียน root scale เป็น `±1` ทุก frame แม้ prefab serialize scale `2`; หลังเปลี่ยนเป็น `SpriteRenderer.flipX` ต้องตั้ง prefab root scale เป็น `(1,1,1)` เพื่อคง visual ประมาณ `0.96 × 0.96 m` และ trigger collider ประมาณ `0.99 × 0.74 m` ไม่ให้ใหญ่เป็นสองเท่า
- Hand state/presentation: `Cursor` ใช้ default crosshair เฉพาะ owner, `Preview` ใช้ Hand sprite เฉพาะ owner เมื่อ ray hover Cargo ภายใน `initialGrabRange` หรือกดเมาส์ค้าง และ `Holding` ใช้ Hand sprite ทุก peer พร้อม resolve ตำแหน่งจาก Cargo-local grip point; player collider ไม่ใช่ preview target
- Hand orientation: right-facing prefab pose เป็นค่าเริ่มต้น `(0,0,120)`; ถ้ามืออยู่ซ้ายกว่า player center ให้หมุน visual เป็น `(0,180,120)` โดยไม่อ่าน player facing direction
- Hover policy: `CargoGrabController` raycast local pointer กับ Cargo/proximity volume แล้วเปิด panel/Hand preview เฉพาะเมื่อ `CursorIntentProvider.IsPointerWithinInteractionReach`; ตำแหน่ง Actual Hand ที่ถูก clamp และ collider ของ player ไม่สามารถเปิด panel ได้
- Collision policy: Hand ยังเป็น kinematic trigger/query-only แต่ prefab เริ่ม disabled; owner arm collider เฉพาะเมื่อกดเมาส์และ raw cursor อยู่ในระยะ, held Hand เปิด collider และ server เปิด query probe ให้ remote Hand เพื่อคง authoritative strict-contact validation โดยไม่มี collision response
- Shake prevention: ลบ `NetworkRigidbody` ออกจาก Hand, ปิด Rigidbody/NetworkTransform interpolation, ใช้ `Rigidbody.position` writer เพียงจุดเดียวต่อ rendered frame และให้ `PlayerHand` LateUpdate หลัง follow camera; cursor intent มี dead zone `0.01 m`
- Replicated truth: `CargoHoldState` อยู่บน Player NetworkObject และ server เป็นผู้เขียน ประกอบด้วย Cargo/Player/Hand references, local grab point, cursor intent, state version, accepted input sequence, grip/release state และ release reason; static registry เป็นเพียง server lookup index ไม่ใช่ source of truth
- Deviations from plan: เริ่มก่อน Feature 02 เพราะผู้ใช้สั่ง implement ต่อ; Sprite Ordering integration ยัง defer โดยไม่เปลี่ยน rendering policy ของ Feature 02. Feature 01 เป็น prototype implemented แต่ remote-client validation ยัง pending. ยังไม่ได้เพิ่ม custom intent marker/predicted Cargo transform เพราะ v1 ใช้ OS cursor เป็น intent และ snap Actual Hand จาก interpolated Cargo โดยตรง

### Changed files/assets

- `Assets/Script/Player/CargoGrabController.cs` — owner input, raw-reach-gated Cargo hover, Cursor/Preview input state, strict client/server contact gate, validated/versioned RPC, replicated holder state และ lifecycle cleanup
- `Assets/Script/Player/Hand Handle/PlayerHand.cs` — stable late-frame free Hand, Cursor/Preview/Holding presentation, state-driven collider arming, rigid Actual Hand และ late-join state resolution
- `Assets/Script/Player/Holding/GripConfiguration.cs` — shared solver/reach/network tuning
- `Assets/Script/Player/Holding/GripForceModel.cs` — pure planar force/acceleration/torque helpers
- `Assets/Script/Player/Holding/CargoHoldState.cs` — network-serializable authoritative holder record
- `Assets/Script/Player/Holding/GripContactUtility.cs` — trigger-Hand กับ solid-Cargo penetration/near-contact validation
- `Assets/Script/Player/Holding/CursorIntentProvider.cs` — local mouse-to-XY intent, raw pointer reach flag และ dead zone แยกจาก Actual Hand
- `Assets/Script/Cargo System/CargoProximitySensor.cs` — เก็บ generated padded hover volume เท่านั้น; ไม่เปิด UI จาก trigger Hand โดยตรง
- `Assets/Script/Cargo System/CargoHoldSolver.cs` — server-authoritative per-holder force-at-point solver และ debug vectors
- `Assets/Script/Cargo System/CargoController.cs` — Cargo runtime protection, local hover/debug Status UI gate และ damage immunity handling
- `Assets/Script/Cargo System/CargoDebugMode.cs` — local `=` toggle สำหรับ Status UI ของ Cargo ทุกตัว
- `Assets/Data/Holding/Default Grip Configuration.asset` — tuning source กลาง
- `Assets/Data/Cargo/Prototype/Cargo Prototype.asset` — test Cargo mass `5 → 0.5 kg` ให้หนึ่ง holder ยกได้ภายใต้ project gravity `-20 m/s²`; ไม่เปลี่ยน mass ของ Cargo data ชนิดอื่น
- `Assets/Prefab/Player/Player.prefab` — เพิ่ม CursorIntentProvider และ assign shared config
- `Assets/Prefab/Player/PlayerHand.prefab` — query-only trigger ที่เริ่ม disabled, kinematic Rigidbody ไม่มี interpolation/NetworkRigidbody และ owner-authoritative NetworkTransform
- `Assets/Prefab/Cargo/CargoController (new).prefab` — เพิ่ม CargoHoldSolver และ assign shared config; MainLevel Cargo instance รับการเปลี่ยนแปลงจาก prefab นี้
- `Assets/Editor/WeightedHoldingValidator.cs` — production setup validator รวม Hand size, trigger-only/default-disabled, interpolation และ no-NetworkRigidbody guards
- `Assets/Tests/EditMode/WeightedHoldingEditModeTests.cs` — force/contact/visibility/config/prefab, legacy Hand size และ one-holder lift-margin tests
- `Assets/Tests/EditMode/CargoPrototypeEditModeTests.cs` — Cargo protection, Debug Mode default/all-cargo behavior และ debug+hover Status UI gate regression tests
- `Assets/Tests/PlayMode/WeightedHoldingPlayModeTests.cs` — mass response, force cancellation/torque, exact grip point และ trigger-without-impulse tests
- `Assets/Docs/README.md` — สถานะ prototype, strict-contact rule และ pending validation สำหรับ AI รอบถัดไป
- `Assets/Docs/Plan/Features/03-weighted-multiplayer-holding.md` — checklist, decisions, tuning และ evidence นี้

### Tests and evidence

- Cursor/Hand UX follow-up compile: Unity `6000.3.10f1` recompile ผ่าน (`failed=false`, `errors=[]`); หลัง prefab/validator update Editor รายงาน `up_to_date`
- Cursor/Hand UX follow-up validator: `WeightedHoldingValidator.ValidateAll` PASS — ตรวจ Hand prefab trigger-only/default-disabled, shared config, network authority, Cargo solver, layer matrix และ legacy joint removal
- Cursor/Hand UX isolated probe: PASS — pointer-in-range ที่ไม่ hover Cargo คง `Cursor`/collider disabled, Cargo hover เป็น `Preview`/collider disabled, out-of-range click เป็น `Preview`/collider disabled; right side ใช้ `flipX=false, rotation=(0,0,120)` และ left side ใช้ `flipX=false, rotation=(0,180,120)`
- Cursor/Hand UX gameplay/visual/multiplayer tests: **Deferred to tester ตามคำสั่งผู้ใช้** — ต้องทวน Cargo-only hover ใน/นอกระยะ, left/right visual, passive preview ไม่มี collider, click-anywhere preview, grab เฉพาะ click+reach+contact, release transitions และ visibility ทุก peer
- Compile result: Unity `6000.3.10f1` recompile หลัง final cleanup patch ผ่าน, `failed=false`, `errors=[]`
- Validator result: `WeightedHoldingValidator.ValidateAll` PASS — shared config, replicated holder record, free/held Hand collision policy, server Cargo authority, force solver, layer matrix และไม่มี SpringJoint
- Unit/EditMode result: `WeightedHoldingEditModeTests` ผ่าน `10/10` — zero force, damping/force clamp, heavy acceleration, add/cancel/torque, planar projection, trigger contact, free/held visibility truth table, shared config, legacy Hand size และ prototype one-holder lift margin
- PlayMode result: `WeightedHoldingPlayModeTests` ผ่าน `4/4` — equal-force light/heavy response, opposing/off-center force behavior, exact local grip resolution และ trigger Hand ตรวจ Cargo ได้โดยไม่สร้าง collision impulse
- Cargo regression result: `CargoPrototypeEditModeTests` รวมอยู่ใน EditMode assembly ที่ผ่าน `21/21` (Cargo 11/11 + Weighted Holding 10/10); protection, Debug Mode all-cargo targeting, debug+hover Status UI gate และ single-prefab/data-driven sprite + generated collider workflow ยังอยู่ครบ
- Cargo protection/debug result: **PASS** — authoritative initialization grants `3.00 s` invincibility; `GrantInvincibility/ClearInvincibility` API ทำงาน และ Status UI truth table เป็น Debug OFF+hover = ซ่อน, Debug ON+ไม่ hover = ซ่อน, Debug ON+hover = แสดง
- Host strict-range probe: request ที่ Hand–Cargo distance `4.6187 m` ถูก reject และ state ยัง `Released`; เมื่อ Hand query trigger แตะ Cargo ที่ player-to-contact distance `0.6828 m` request ถูก accept
- Host rigid-grip probe: Actual Hand และ resolved Cargo grip point เท่ากันที่ `(-0.150000, -1.852795, -3.240000)`, gap `0.00000000 m`, Hand เป็น trigger ระหว่างถือ และ scene ไม่มี Joint
- Host force/weight-direction probe: error ประมาณ `(0, 1.003)` สร้างแรง capped `(0, 60) N`; Cargo ยังตอบสนองต่อ environment collision ขณะที่ Hand ติด Cargo และแยกจาก intent; Z position/velocity และ X/Y rotation drift ที่วัดได้เป็น `0`
- Host post-feedback probe: spawned Hand root scale `(1,1,1)`, collider bounds ประมาณ `(0.99, 0.74, 0.28) m`; in-ship Cargo initialize เป็น `0.5 kg`. ที่ gravity `-20 m/s²` Cargo หนัก `10 N` เทียบกับ one-holder cap `60 N`, จึงมี upward margin `50 N`
- Host release/cleanup probe: re-grab/release สำเร็จด้วย reason `PlayerRequested`, velocity delta `0`; stale-intent และ hard-reach probes release ด้วย reason ที่ถูกต้อง และ Hand คงเป็น query-only trigger
- Host idle-Hand probe: free owner Hand `visible=true`, collider ทุกตัว `isTrigger=true`, Rigidbody kinematic/interpolation `None`, NetworkTransform interpolation ปิด และไม่มี `NetworkRigidbody`; real Cargo overlap query ผ่าน ขณะ stable pointer/player/camera sample วัด world delta `0.00000000 m` (screen precision `0.00117 px`)
- Host + remote result: Host ผ่านตามรายการด้านบน; remote client, simultaneous real players, late join และ live disconnect ยังไม่ได้รัน
- Latency/jitter result: Pending remote transport simulation
- Profiler/network result: Pending max-player remote profile
- Screenshot/video/log paths: ไม่มี artifact ถาวร; evidence อยู่ใน Unity test result และค่าที่บันทึกใน record นี้

### Known limitations/follow-ups

- Tester ยังต้องยืนยัน Cursor/Preview/Holding transitions, Cargo-only hover gating และ left/right visual ใน PlayMode; session นี้ยืนยัน compile, configuration validator และ isolated state/orientation probe
- Input v1 คือ legacy mouse input; controller/gamepad ยังไม่อยู่ใน scope
- OS cursor เป็น Cursor Intent ส่วน Hand sprite เป็น Actual Hand; ยังไม่มี custom intent marker/tension line สำหรับสื่อแรงให้ผู้เล่นเห็น
- Room/death-specific gameplay state ยังไม่มี contract กลางให้ holding validator เรียก; server v1 ตรวจ owner permission, spawned/initialized state, layer, strict contact, distance, capacity และ finite/bounded intent ก่อน
- Dedicated-server authoritative code path ไม่อ่าน Camera/Input/Renderer แต่ dedicated build/run verification ยัง pending
- Baseline remote client, latency, jitter, packet loss, late join, live disconnect และ simultaneous holders ยัง pending จนมี test peer เพิ่ม
- ค่า contact tolerance `0.035 m` และ force/reach tuning ต้องทวนบน remote latency ก่อนถือว่า production-final
- Sprite sorting/Hand always-on-top integration ยังรอ Feature Plan 02 ตาม dependency เดิม
