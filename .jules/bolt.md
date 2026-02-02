## 2025-01-22 - [Polling Latency in Async IO]
**Learning:** Polling `NetworkStream.DataAvailable` with `Task.Delay(10)` resulted in ~33ms latency on Linux, significantly higher than the expected 10-15ms.
**Action:** Always prefer `await Stream.ReadAsync()` over polling `DataAvailable`. Isolate portable logic from UI-bound code (WPF) to enable verification on other platforms.
