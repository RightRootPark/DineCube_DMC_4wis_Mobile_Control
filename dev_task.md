# 4WS Robot Controller Tester Development Task

## Project Overview
- **Goal**: Create a WPF application to act as a TCP client remote controller for the DMC Robot.
- **Path**: `C:\DTWorkSpace\AntiGravityProject\DMC_Robot controral\4WiscontrolWithScript\Contrul_tester`
- **Tech Stack**: C# / WPF (.NET 6.0 or later)

## Checklist

- [ ] **Project Setup**
    - [ ] Create `Contrul_tester` directory
    - [ ] Initialize WPF project (`dotnet new wpf`)
    - [ ] Verify build environment

- [ ] **UI Implementation (XAML)**
    - [ ] Layout Design (Grid/StackPanel)
    - [ ] Connection Controls (IP, Port, Connect/Disconnect Buttons)
    - [ ] Status Indicators (Connected/Disconnected, Sent Data Monitor)
    - [ ] Control Buttons
        - [ ] Move: Forward/Backward (Accel control)
        - [ ] Stop (Reset to 0)
        - [ ] Turn: Left/Right (Spot Turn, C control)
        - [ ] Crab: CW/CCW (Crab Mode, Y + C control)
    - [ ] Settings (Max Speed, Acceleration Rate)

- [ ] **Logic Implementation (C#)**
    - [ ] **TCP Communication Class**
        - [ ] Connect/Disconnect logic
        - [ ] Async Write loop (Heartbeat/Watchdog support)
    - [ ] **Control Logic**
        - [ ] Timer for inputs (100ms interval for UI updates & data transmission)
        - [ ] Long Press Handlers (Increment/Decrement values)
        - [ ] Data Formatting (`x,y,c` string generation)
    - [ ] **Safety Features**
        - [ ] Auto-send `0,0,0` when no buttons pressed (Heartbeat)
        - [ ] Input Validation

- [ ] **Verification**
    - [ ] Dry run logic check
    - [ ] Update README.md with tester usage
