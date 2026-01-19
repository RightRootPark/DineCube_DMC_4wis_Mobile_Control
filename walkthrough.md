# Walkthrough - 4WS Mobile Base Kinematics

DainCube DMC 컨트롤러를 위한 4륜 조향(4WS) 모바일 베이스 기구학 구현을 완료했습니다.

## 주요 변경 사항

### 1. 하드웨어 매핑 (5축 구성)
코드에서 출력되는 `newang` 배열은 다음과 같이 하드웨어 축에 매핑됩니다:
- **Axis 1 (`q[0]`)**: 액셀러레이터 (유압 밸브 압력)
- **Axis 2 (`q[1]`)**: 우측 전방 (RF) 조향
- **Axis 3 (`q[2]`)**: 우측 후방 (RR) 조향
- **Axis 4 (`q[3]`)**: 좌측 전방 (LF) 조향
- **Axis 5 (`q[4]`)**: 좌측 후방 (LR) 조향

### 2. 제어 모드 구현 (`cx_uctoja`)
입력 명령(`x, y, c`)의 조합에 따라 자동으로 모드를 전환합니다. (임계값: 1.0)

| 모드 | 조건 | 동작 방식 | 조향 설정 |
| :--- | :--- | :--- | :--- |
| **Spot Turn** | `c`만 입력 | 제자리 회전 | RF/LR: -45°, RR/LF: +45° (마름모) |
| **Crab** | `y, c` 입력 | 게걸음 주행 | 모든 바퀴 조향각 = `c` |
| **4WS** | `x, c` 입력 | 일반 주행 (역위상) | 전륜 = `c`, 후륜 = `-c` |
| **Stop** | 조건 미충족 | 정지 | 모든 출력 0 |

### 3. 코드 정리
기존 SCARA 로봇용 링크 길이(`s_H1`, `s_H2` 등)와 설정 로직을 제거하고, 4WS 전용 로직으로 최적화했습니다.

## 코드 확인
- [main.c](file:///c:/RRWorkSpace/DainCube/scara_kinematics/Driven_EXT/main.c) 파일에서 변경된 `cx_jatouc` 및 `cx_uctoja` 함수를 확인하실 수 있습니다.

```c
// 예시: 4WS Inverse Kinematics 로직 (cx_uctoja 내부)
if (fabs(x) > THRESHOLD && fabs(c) > THRESHOLD && fabs(y) <= THRESHOLD)
{
    accel = x;
    rf = lf = c;    // 전륜
    rr = lr = -c;   // 후륜 (역위상)
}
```

## 검증 결과
- 논리 설계에 따른 각 모드별 출력값이 정확히 매핑되는 것을 확인했습니다.
- "1 Accelerator" 제약 조건을 만족하도록 설계되었습니다.
