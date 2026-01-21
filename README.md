# 4WS Mobile Robot Control Script (DMC Controller)

본 프로젝트는 **DainCube DMC 로봇 컨트롤러**를 사용하여 5축 유압 구동 모바일 로봇(4WS)을 제어하기 위한 스크립트 및 문서 저장소입니다.  
로봇은 **TCP 서버**로 동작하며, 외부 클라이언트(PC)로부터 **5개 축(가속, 조향4)** 명령을 수신하여 하드웨어에 직접 출력합니다.
본 스크립트는 상위 제어기(Host PC)에서 계산된 각도를 그대로 반영하는 **Pass-through Driver** 역할을 합니다.

## 📌 주요 기능 (Features)

*   **TCP Server 통신**: 포트 `5009`를 통해 외부 제어기와 통신.
*   **Direct Drive**: 수신된 5개의 값을 1:1로 각 축(Axis 1~5)에 매핑.
*   **Binary Protocol**: 로봇 상태(조향각, 에러)를 **24바이트 바이너리 패킷**으로 고속 전송.
*   **Watchdog**: 0.2초간 데이터 수신이 없으면 안전을 위해 자동 정지.
*   **Echo Mode Test**: 모터 구동 없이 통신 상태를 검증할 수 있는 시뮬레이션 스크립트 제공.

## 🛠 하드웨어 구성 (Hardware Configuration)

DMC 컨트롤러의 5개 축 매핑 정보입니다.

| Axis No. | Role | Variable | Description |
| :--- | :--- | :--- | :--- |
| **Axis 1** | Accelerator | `q[0]` | 유압 밸브/가속 페달 제어 |
| **Axis 2** | RF Steer | `q[1]` | 우측 전방 조향 |
| **Axis 3** | RR Steer | `q[2]` | 우측 후방 조향 |
| **Axis 4** | LF Steer | `q[3]` | 좌측 전방 조향 |
| **Axis 5** | LR Steer | `q[4]` | 좌측 후방 조향 |

## 📡 통신 프로토콜 (Communication Protocol)

### 1. Request (PC -> Robot)
*   **Method**: ASCII CSV String
*   **Port**: `5009` (Main), `5008` (Test Script)
*   **Format**: `accel,rf,rr,lf,lr`
    *   *예시: `100.0, 10.5, -10.5, 10.5, -10.5`*

### 2. Response (Robot -> PC)
*   **Method**: **Binary Stream** (Fixed-Point Integer x100)
*   **Packet Size**: 24 Bytes (6 x 4 Bytes, Big Endian)
*   **Content**:
    1.  `Accel` (Int32, x100)
    2.  `RF Angle` (Int32, x100)
    3.  `RR Angle` (Int32, x100)
    4.  `LF Angle` (Int32, x100)
    5.  `LR Angle` (Int32, x100)
    6.  `Error Code` (Int32, Raw)
*   *예시: 12.34도 -> 정수 1234 (0x000004D2) 전송*

## 🚀 사용/설치 방법 (Usage)

### A. 메인 제어 (실제 로봇 구동)
1.  **로드**: `4ws_control` 스크립트를 DMC 컨트롤러에 업로드.
2.  **실행**: 스크립트 실행 -> `Server Starting... Port 5009` 확인.
3.  **접속**: PC 프로그램에서 포트 **5009**로 연결.

### B. 통신 테스트 (Echo Mode)
1.  **로드**: `comm_test_script` 스크립트 실행.
2.  **기능**: 모터를 움직이지 않고, PC에서 보낸 값을 그대로 돌려줌(Echo).
3.  **접속**: PC 프로그램에서 포트 **5008**로 연결.
4.  **확인**: PC에서 W/A/S/D 입력 시 그래프가 반응하는지 확인.

## 🎮 PC 제어 프로그램 (WPF Tester)

`Contrul_tester` 폴더에 포함된 C# 기반 WPF 프로그램입니다.

### 주요 기능
*   **Drive Modes**:
    *   **4WS**: 전/후진 및 조향 (+/- 89도 제한). 
        * *참고: 90도 특이점 방지를 위해 89도로 제한됨.*
    *   **Crab**: 게걸음 주행 (최대 +/- 135도).
    *   **Pivot**: 제자리 회전 (Spot Turn).
*   **Ackermann Geometry**: 윤거/축거 **1050mm** 기준으로 내륜/외륜 조향각 자동 계산.
*   **Safety**:
    *   **Watchdog**: 2초간 수신 데이터 없으면 연결 해제.
    *   **Fragmentation**: 바이너리 패킷(24byte) 조각 모음 처리 적용.

### 실행 방법
1.  **IP 설정**: `192.168.1.207` (기본값).
2.  **Port**: `5009` (Main) 또는 `5008` (Test).
3.  **Connect**: 연결 후 우측 상단 LED가 초록색으로 변경됨.

## ⚠️ 주의 사항 (Safety)

*   **유압 안전**: 실제 유압 장비 제어 시, 반드시 **Jack-up** 상태에서 먼저 테스트하십시오.
*   **데이터 타입**: 로봇에서 PC로 보내는 데이터는 `x100`된 정수형이므로, PC 측에서 `/100.0` 처리가 필수입니다.
