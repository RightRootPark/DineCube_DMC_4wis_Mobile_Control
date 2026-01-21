# 4WS Mobile Robot Control Script (DMC Controller)

본 프로젝트는 **DainCube DMC 로봇 컨트롤러**를 사용하여 5축 유압 구동 모바일 로봇(4WS)을 제어하기 위한 스크립트 및 문서 저장소입니다.  
로봇은 **TCP 서버 모드**로 동작하며, 외부 클라이언트로부터 **5개 축(가속, 조향4)에 대한 개별 명령**을 수신하여 하드웨어에 직접 출력합니다.
본 스크립트는 기구학적 계산(Spot Turn, Crab 등)을 수행하지 않으며, 상위 제어기(Host PC)에서 계산된 각도를 그대로 반영하는 **Pass-through Driver** 역할을 합니다.

## 📌 주요 기능 (Features)

*   **TCP Server 통신**: 포트 `5009`를 통해 외부 제어기와 통신.
*   **Direct Drive**: 수신된 5개의 값을 1:1로 각 축(Axis 1~5)에 매핑.
*   **Watchdog**: 0.2초간 데이터 수신이 없으면 안전을 위해 자동 정지.
*   **안전 기능**: 통신 단절(2초) 시 자동 정지 및 재접속 대기, 절댓값 연산을 위한 안전 로직 구현. (Client Watchdog 적용)

## 🛠 하드웨어 구성 (Hardware Configuration)

DMC 컨트롤러의 5개 축을 다음과 같이 매핑하여 사용합니다.

| Axis No. | Role | Variable | Description |
| :--- | :--- | :--- | :--- |
| **Axis 1** | Accelerator | `q[0]` | 유압 밸브/가속 페달 제어 |
| **Axis 2** | RF Steer | `q[1]` | 우측 전방 조향 |
| **Axis 3** | RR Steer | `q[2]` | 우측 후방 조향 |
| **Axis 4** | LF Steer | `q[3]` | 좌측 전방 조향 |
| **Axis 5** | LR Steer | `q[4]` | 좌측 후방 조향 |

## 📡 통신 프로토콜 (Communication Protocol)

*   **Role**: Server (Robot) <-> Client (Remote PC/Device)
*   **Port**: `5009` (Default)
*   **Data Format**: ASCII String (CSV)
    ```text
    accel,rf,rr,lf,lr
    ```
    *   `accel`: Axis 1 출력 (가속/Valve)
    *   `rf`: Axis 2 출력 (우측 전방 조향각)
    *   `rr`: Axis 3 출력 (우측 후방 조향각)
    *   `lf`: Axis 4 출력 (좌측 전방 조향각)
    *   `lr`: Axis 5 출력 (좌측 후방 조향각)
    *   *예시: `100.0, 10.5, -10.5, 10.5, -10.5` (4WS 주행)*

### Response (Robot -> Client)
*   **Data Format**: **Binary Stream** (Fixed-Point Integer x100)
    *   **Packet Size**: 24 Bytes (6 x 4 Bytes)
    *   **Endianness**: Big Endian (Network Standard)
    *   **Content**:
        1.  `accel` (4 bytes, Int32): Value * 100
        2.  `rf` (4 bytes, Int32): Angle * 100
        3.  `rr` (4 bytes, Int32): Angle * 100
        4.  `lf` (4 bytes, Int32): Angle * 100
        5.  `lr` (4 bytes, Int32): Angle * 100
        6.  `error_code` (4 bytes, Int32): Raw Integer
    *   *예시: 12.34 도 -> 1234 (0x000004D2) 송신*
    *   **Negative Values**: 2의 보수 표현이 아닌, 절댓값 연산 후 상위 바이트 보정 방식을 사용하여 음수 정밀도를 유지합니다.

## 🚀 사용/설치 방법 (Usage)

1.  **스크립트 로드**: `4ws_control` 파일을 DMC 컨트롤러의 프로젝트 폴더로 복사합니다.
2.  **파라미터 설정**: 스크립트 상단의 `THRESHOLD`, `STEER_SPOT` 변수를 로봇 기구학에 맞게 조정합니다.
3.  **실행**:
    *   컨트롤러에서 스크립트를 실행합니다.
    *   `Server Starting...` 메시지가 TP(티칭 펜던트)에 출력되는지 확인합니다.
4.  **클라이언트 접속**:
    *   외부 PC에서 로봇의 IP 주소와 포트(5009)로 TCP 접속을 시도합니다.
    *   접속 성공 시 `Client Connected!` 메시지가 출력됩니다.

## 🎮 PC 제어 프로그램 (WPF Tester)

로봇을 원격 제어할 수 있는 PC용 프로그램이 `Contrul_tester` 폴더에 포함되어 있습니다.

### 실행 방법
1. **빌드**: `Contrul_tester` 폴더에서 `dotnet run` 명령어를 실행하거나, Visual Studio로 열어서 빌드합니다.
2. **접속**:
    *   **IP**: 로봇(DMC)의 IP 주소 입력 (기본값: `127.0.0.1`)
    *   **Port**: `5009`
    *   `Connect` 버튼 클릭 -> 로봇 TP에 "Client Connected" 표시 확인.
    *   **Status Monitor**: 연결 시 하단의 "Robot Status Monitor" 패널에 로봇의 현재 상태(각도, 에러)가 실시간 표시됩니다.

### 기능설명
*   **전/후진 (W/S)**: 버튼을 누르고 있으면 가속(초당 20), 떼면 즉시 정지(0).
*   **제자리 회전 (A/D)**: 버튼을 누르고 있으면 Spot Turn 수행.
*   **게걸음 (Crab)**: CW/CCW 버튼으로 조향각과 이동을 동시 제어.
*   **비상 정지 (Space)**: `STOP` 버튼 또는 스페이스바 입력 시 즉시 모든 출력을 0으로 초기화.
*   **Watchdog 대응**: 아무 키도 누르지 않아도 연결 유지를 위해 `0,0,0` 하트비트 신호를 0.1초마다 자동 전송합니다.
*   **Safety Features**:
    *   **Connection Watchdog**: 서버로부터 2초 이상 데이터가 수신되지 않으면 "Connection Timeout" 경고와 함께 연결을 해제하여 좀비 상태를 방지합니다.
    *   **Connect Timeout**: 연결 시도 시 3초 이내에 응답이 없으면 즉시 실패 처리합니다.

## 📂 파일 구조 (File Structure)

*   `4ws_control`: 메인 제어 스크립트 (DMC Script Language)
*   `Contrul_tester/`: WPF 제어 프로그램 소스 코드
    *   `MainWindow.xaml.cs`: UI 이벤트 핸들러
    *   `MainFunction.cs`: 핵심 제어 로직 및 통신 모듈 (Refactored)
*   `walkthrough.md`: 제어 로직 및 기구학 설명 문서
*   `Server mode manual.txt`: DMC 서버 모드 명령어 레퍼런스
*   `README.md`: 프로젝트 설명서

## ⚠️ 주의 사항 (Safety)

*   본 스크립트는 실제 유압 장비를 제어하므로, 초기 테스트 시 **유압을 차단하거나 잭업(Jack-up)** 상태에서 조향 동작을 먼저 검증하십시오.
*   통신 타임아웃/단절 시 안전을 위해 모든 출력을 0으로 초기화하는 로직이 포함되어 있습니다.
