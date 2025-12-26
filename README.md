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
