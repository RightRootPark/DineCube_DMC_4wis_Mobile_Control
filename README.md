# DineCube_DMC_4wis_Mobile_Control
DBC(DineCube Motion Controller)를 이용한 4륜 독립 조향(4WIS) 모바일 로봇 제어 프로젝트입니다.

## � 주요 기능 (Features)
*   **4WIS Kinematics**: 4륜 독립 조향 알고리즘 (4WS, Crab, Spot Turn).
*   **Real-time Communication**: TCP/IP 기반의 실시간 제어 및 피드백.
*   **Multi-Tasking Architecture**: 통신(`server_task`)과 제어(`motion_task`)를 분리하여 응답성 극대화.
*   **Robust Protocol**: Header(0xFEFE) 기반의 Binary 통신으로 데이터 정합성 보장.
*   **Live Monitoring**: 실제 로봇의 엔코더 값(`#Here`)을 실시간으로 모니터링.

## 🛠 시스템 아키텍처 (System Architecture)
### 1. Robot Controller (Server)
*   **Hardware**: DainCube Motion Controller (DMC)
*   **Scripts**:
    *   `4ws_server_task`: PC와의 고속 통신 담당 (100Hz Loop). 패킷 수신 및 글로벌 변수(`g_in_...`) 업데이트.
    *   `4ws_motion_task`: 로봇 구동 담당. 글로벌 변수를 읽어 `MoveJ` 실행.
*   **Feedback**: `#Here` 명령어를 사용하여 실제 관절 위치를 읽어 반환.

### 2. PC Client (Control Tester)
*   **Framework**: C# WPF (.NET 9.0)
*   **Function**: 조이스틱/UI 입력을 받아 로봇에 명령 전송 및 상태 모니터링.
*   **Design**: 30FPS UI Throttling, Async Socket, Cycle Monitoring.

## � 설치 및 실행 (Installation & Run)

### Robot Side
1.  DMC 컨트롤러에 `4ws_server_task`와 `4ws_motion_task` 파일을 전송합니다.
2.  **Multi-Tasking**으로 두 스크립트를 동시에 실행합니다. (예: Task 1, Task 2 할당)

### PC Side
1.  이 저장소를 클론합니다.
2.  `Contrul_tester` 폴더로 이동하여 빌드 및 실행합니다.
    ```bash
    cd Contrul_tester
    dotnet run
    ```
3.  UI에서 로봇의 IP와 Port(**5009**)를 입력하고 `Connect`를 클릭합니다.

## 🎮 조작 방법 (Controls)
*   **W/S**: 전진/후진 (속도 제어)
*   **A/D**: 좌/우 조향 (Steering)
*   **Mode Select**: 4WS(일반), Crab(게걸음), Pivot(제자리 회전)

## 🔧 최적화 내역 (Optimization Log)
*   **Protocol**: Text(CSV) -> **Binary with Header (0xFEFE)** (안정성 강화).
*   **Cycle Time**: Client Send(4Hz, 250ms -> **200ms**), Server Loop(100Hz).
*   **Kinematics**: Pivot Mode 각도 튜닝 (RF:-135, RR:135, LF:45, LR:-45).
