## 인천 학생 과학관

인공지능 AI Puzzle Game Project

---

## Lebai 로봇 제어 시스템

### 통신 방식

| 항목       | 내용                     |
| ---------- | ------------------------ |
| 프로토콜   | HTTP POST + JSON-RPC 2.0 |
| 기본 포트  | 3021                     |
| 엔드포인트 | `http://{IP}:{PORT}`     |

### 주요 JSON-RPC API 메소드

#### 시스템 제어

| 메소드            | 파라미터 | 용도                                       |
| ----------------- | -------- | ------------------------------------------ |
| `start_sys`       | `[{}]`   | 로봇 시작 (L Master '시작' 버튼과 동일)    |
| `stop_sys`        | `[{}]`   | 로봇 정지                                  |
| `estop`           | `[{}]`   | 비상 정지 (하드웨어 급정지, 브레이크 작동) |
| `powerdown`       | `[{}]`   | 전원 끄기 (제어박스 포함)                  |
| `reboot`          | `[{}]`   | 제어박스 재시작                            |
| `get_robot_state` | `[{}]`   | 로봇 상태 확인                             |

#### 모션 제어

| 메소드           | 파라미터                  | 용도                           |
| ---------------- | ------------------------- | ------------------------------ |
| `movej`          | `p, a, v, t, r`           | 관절 공간 이동                 |
| `movel`          | `p, a, v, t, r`           | 직선 이동 (TCP 공간)           |
| `movec`          | `via, p, rad, a, v, t, r` | 원호 이동                      |
| `move_joint`     | pose, param               | 관절 이동 (현재 사용 중)       |
| `speedj`         | `a, v, t`                 | 관절 속도 제어                 |
| `speedl`         | `a, v, t, frame`          | 직선 속도 제어                 |
| `stop_move`      | `[{}]`                    | 모션 정지                      |
| `pause_move`     | `[{}]`                    | 모션 일시정지                  |
| `resume_move`    | `[{}]`                    | 모션 재개                      |
| `teach_mode`     | `[{}]`                    | 티칭 모드 진입 (자유 드라이브) |
| `end_teach_mode` | `[{}]`                    | 티칭 모드 종료                 |

#### 상태 조회

| 메소드                | 용도                    |
| --------------------- | ----------------------- |
| `get_kin_data`        | 현재 관절 위치 (라디안) |
| `get_tcp`             | 현재 TCP 설정           |
| `get_velocity_factor` | 현재 속도 비율 (0-100)  |
| `get_payload`         | 현재 부하 설정          |

#### 그리퍼 제어 (LMG-90)

| 메소드      | 파라미터                         | 용도                          |
| ----------- | -------------------------------- | ----------------------------- |
| `init_claw` | `[{}]`                           | 그리퍼 초기화 (열기/닫기 1회) |
| `set_claw`  | `force(0-100), amplitude(0-100)` | 그리퍼 제어                   |
| `get_claw`  | `[{}]`                           | 그리퍼 상태 조회              |

#### 설정

| 메소드                       | 파라미터             | 용도               |
| ---------------------------- | -------------------- | ------------------ |
| `set_tcp`                    | `[x, y, z, α, β, γ]` | TCP 좌표 설정      |
| `set_velocity_factor`        | `factor(0-100)`      | 속도 비율 설정     |
| `set_payload`                | `mass, cog{x,y,z}`   | 부하 설정          |
| `disable_collision_detector` | `[{}]`               | 충돌 감지 비활성화 |
| `enable_collision_detector`  | `[{}]`               | 충돌 감지 활성화   |

### 로봇 상태 값

| 상태              | 값  | 설명      |
| ----------------- | --- | --------- |
| IDLE              | 5   | 대기      |
| RUNNING           | 7   | 실행 중   |
| STOPPED           | 12  | 정지됨    |
| Emergency Stopped | 1   | 비상 정지 |
| PAUSED            | 4   | 일시정지  |
| TEACHING          | 6   | 티칭 모드 |

### 요청 예시

```json
{
  "jsonrpc": "2.0",
  "method": "move_joint",
  "params": [
    {
      "pose": {
        "kind": 1,
        "joint": { "joint": [0, 0, 0, 0, 0, 0] }
      },
      "param": { "velocity": 0.5, "acc": 1.0 }
    }
  ],
  "id": 1
}
```

### 관련 파일

- `Assets/Scripts/Robot/LebaiRobotController.cs` - 로봇 제어 메인 스크립트
- `Assets/lebai-manual-en.pdf` - Lebai LM3 매뉴얼 (L Master v2.2)

### 로그 파일

- 빌드 실행 시: exe 파일과 같은 폴더에 `LebaiRobotLog.txt` 생성
- 에디터 실행 시: 프로젝트 루트에 생성
- 앱 시작 시 기존 로그 삭제 후 새로 생성

### 참고 문서

- [Lebai Help - Motion](https://help.lebai.ltd/en/sdk/motion.html) - 모션 제어 API
- [Lebai Help - Gripper](https://help.lebai.ltd/en/sdk/claw.html) - 그리퍼 제어 API
- [Lebai Help - Config](https://help.lebai.ltd/en/api/config.html) - 로봇 설정 API
- [Lebai .NET SDK](https://github.com/lebai-robotics/lebai-dotnet-sdk) - C# SDK
- [Lebai JSON-RPC Demo](https://github.com/lebai-robotics/jsonrpc-demo) - JSON-RPC 예제

---

## 티칭 시스템

JSON 파일을 사용하여 로봇의 동작 시퀀스를 정의하고 실행할 수 있습니다.
Unity를 다시 빌드하지 않고도 JSON 파일만 수정하여 로봇 동작을 조정할 수 있습니다.

### 파일 위치

- **에디터**: 프로젝트 루트 폴더
- **빌드**: exe 파일과 같은 폴더
- **기본 파일명**: `robot_teaching.json`

### JSON 구조

```json
{
    "name": "티칭 이름",
    "description": "설명",
    "totalDuration": 180.0,
    "steps": [
        {
            "stepNumber": 1,
            "name": "스텝 이름",
            "time": 0.0,
            "duration": 3.0,
            "action": { ... }
        }
    ]
}
```

### 스텝 필드

| 필드         | 타입   | 설명                                                       |
| ------------ | ------ | ---------------------------------------------------------- |
| `stepNumber` | int    | 스텝 번호 (1부터 시작)                                     |
| `name`       | string | 스텝 이름 (UI에 표시됨)                                    |
| `time`       | float  | **시작 시간** (초) - 티칭 시작 후 몇 초에 이 동작 시작     |
| `duration`   | float  | **동작 소요 시간** (초) - 이 동작이 완료되는데 걸리는 시간 |
| `action`     | object | 실행할 동작                                                |

### time과 duration 핵심 공식

```
다음 스텝 time = 현재 스텝 time + 현재 스텝 duration
```

| 스텝 | time | duration | 설명                                        |
| ---- | ---- | -------- | ------------------------------------------- |
| 1    | 0    | 3.0      | 0초에 시작, 3초 동안 실행                   |
| 2    | 3.0  | 2.0      | 3초에 시작 (스텝1 완료 직후), 2초 동안 실행 |
| 3    | 5.0  | 5.0      | 5초에 시작, 5초 동안 실행                   |

### 액션 타입

#### 1. move_joint (관절 이동)

```json
{
    "type": "move_joint",
    "joints": [J1, J2, J3, J4, J5, J6],
    "velocity": 0.8,
    "acceleration": 1.2
}
```

- `joints`: 6개 관절 각도 (도 단위, -175 ~ +175)
- `velocity`: 속도 (선택적)
- `acceleration`: 가속도 (선택적)
- `duration`이 `t` 파라미터로 전송되어 정확한 시간 내에 동작 완료

#### 2. set_gripper (그리퍼 제어)

```json
{
  "type": "set_gripper",
  "gripperPosition": 100,
  "gripperForce": 50
}
```

- `gripperPosition`: 그리퍼 열림 정도 (0=닫힘, 100=완전히 열림)
- `gripperForce`: 그리퍼 힘 (0~100)

#### 3. wait (대기)

```json
{
  "type": "wait"
}
```

`duration` 동안 아무 동작 없이 대기

#### 4. set_do (디지털 출력 - 버큠 그리퍼)

```json
{
  "type": "set_do",
  "device": "FLANGE",
  "pin": 0,
  "value": 1
}
```

- `device`: 디지털 출력 장치
  - `"FLANGE"`: 플랜지 DO (2개)
  - `"ROBOT"`: 컨트롤 박스 DO (4개)
  - `"EXTRA"`: 확장 보드 DO (12개)
- `pin`: 핀 번호 (0부터 시작)
- `value`: 0=OFF, 1=ON

**버큠 그리퍼 사용 예시:**

```json
{
    "stepNumber": 5,
    "name": "버큠 ON (흡착)",
    "time": 10.0,
    "duration": 0.5,
    "action": {
        "type": "set_do",
        "device": "FLANGE",
        "pin": 0,
        "value": 1
    }
},
{
    "stepNumber": 10,
    "name": "버큠 OFF (해제)",
    "time": 20.0,
    "duration": 0.5,
    "action": {
        "type": "set_do",
        "device": "FLANGE",
        "pin": 0,
        "value": 0
    }
}
```

### 속도/가속도 권장 설정값

| 상황      | velocity | acceleration | duration | 설명                  |
| --------- | -------- | ------------ | -------- | --------------------- |
| 빠른 이동 | 1.5~2.0  | 2.0~3.0      | 2~3초    | 큰 움직임, 빠른 전환  |
| 일반 이동 | 0.8~1.2  | 1.0~1.5      | 3~5초    | 일반적인 작업         |
| 정밀 작업 | 0.3~0.5  | 0.5~0.8      | 5~10초   | 물체 집기/놓기        |
| 매우 느린 | 0.1~0.2  | 0.3~0.5      | 10초+    | 아주 정밀한 위치 조정 |

### 관절 범위

| 관절 | 범위          | 설명        |
| ---- | ------------- | ----------- |
| J1   | -175° ~ +175° | 베이스 회전 |
| J2   | -175° ~ +175° | 어깨        |
| J3   | -175° ~ +175° | 팔꿈치      |
| J4   | -175° ~ +175° | 손목 회전 1 |
| J5   | -175° ~ +175° | 손목 굽힘   |
| J6   | -175° ~ +175° | 손목 회전 2 |

### Unity Inspector 설정

1. `LebaiRobotController` 컴포넌트에서 "UI - 티칭 시스템" 섹션 찾기
2. `Teaching Button`: 티칭 시작 버튼 연결
3. `Stop Teaching Button`: 티칭 중지 버튼 연결
4. `Teaching Status Text`: 상태 표시 텍스트 연결
5. `Teaching File Name`: JSON 파일명 (기본값: `robot_teaching.json`)

---

## 동작 원리

### 티칭 실행 흐름

```
1. JSON 파일 로드
2. 스텝을 time 순으로 정렬
3. 각 스텝에 대해:
   - time까지 대기 (절대 시간 기준)
   - 스텝 실행 (move_joint / set_gripper / wait)
   - move_joint는 duration 동안 대기 후 다음 스텝으로
4. 모든 스텝 완료 후 종료
```

### move_joint 동작

```
명령 전송 → 로봇 이동 시작 → duration 대기 → 이동 완료 → 다음 스텝
```

- `t` 파라미터로 정확한 시간 제어 (duration 값 사용)
- 코드에서 duration만큼 대기 후 다음 스텝 실행 (이동 완료 보장)

### UI 슬라이더 이벤트 처리

티칭 실행 중에는 슬라이더 값 변경 시 그리퍼 명령이 자동 전송되지 않습니다.

```csharp
if (isTeachingRunning || isMovingToHome) return;
```

### isBusy 플래그

모든 버튼/슬라이더 핸들러에서 `isBusy` 체크로 중복 명령 방지

### Init vs Connect 차이

| 기능    | Init (홈 포지션)              | Connect (연결)            |
| ------- | ----------------------------- | ------------------------- |
| 동작    | 로봇을 [0,0,0,0,0,0]으로 이동 | 로봇과 네트워크 연결      |
| 그리퍼  | 0%로 닫힘                     | 초기화만 (init_claw)      |
| 로딩 UI | `loadingIndicator`            | `connectLoadingIndicator` |

---

## 자주 하는 실수

### 1. 그리퍼가 이동 중에 열림/닫힘

**원인**: 이전 버전에서는 move_joint 후 대기 없이 바로 다음 스텝 실행

**해결**: 코드에서 move_joint 실행 후 duration만큼 대기하도록 수정됨

### 2. time 값 계산 오류

**잘못된 예**:

```json
{ "stepNumber": 1, "time": 0.0, "duration": 3.0 },
{ "stepNumber": 2, "time": 2.0, "duration": 2.0 }  // 스텝1이 끝나기 전에 시작
```

**올바른 예**:

```json
{ "stepNumber": 1, "time": 0.0, "duration": 3.0 },
{ "stepNumber": 2, "time": 3.0, "duration": 2.0 }  // 스텝1 완료 후 시작
```

### 3. velocity/acceleration을 너무 높게 설정

- 처음에는 낮은 값(0.3~0.5)으로 테스트
- duration이 설정되어 있으면 velocity/acceleration은 최대 허용치로만 작동
