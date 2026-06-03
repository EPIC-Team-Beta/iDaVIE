# Fitness Functions

These checks protect the Team 1 slice:

1. Team 1-owned refactored files must not contain throw-only
   `NotImplementedException` stubs.
2. Kernel contracts and the volume aggregate must not reference `UnityEngine`
   or `Valve.*`.
3. Team 1 code must not use config singletons, scene object searches, or direct
   static native wrapper calls.
4. Required architecture and ADR files must be present under `refactored/docs`.
5. A compile/static validation step should run when a refactored project file is
   available. Until then, the workflow performs static scans and reports the
   missing project explicitly.
6. F-19: every Team 1 public interface must have at least one hand-written test
   double in the test assembly named `Fake*`, `Stub*`, `Mock*`, `InMemory*`, or
   `Test*`. Generated mocks do not satisfy this gate.

The GitHub Actions workflow in `refactored/.github/workflows` encodes these
checks for the refactored slice.
