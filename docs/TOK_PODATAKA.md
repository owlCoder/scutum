# Data flow — platform-level example

This conceptual example is a starting point. KT1 requires each student to provide a separate DFD for their own component, including its actual processes, actors, and stores.

```mermaid
flowchart LR
  Student[Student] -->|Lab request| API[API and server-side authorization]
  API -->|Check context| Access[Identity and authorization]
  Access -->|Allow / deny| API
  API -->|Authorized revision| Catalog[(Published revisions)]
  API -->|Save response| Attempt[(Attempts)]
  Attempt -->|Response snapshot| Eval[Evaluation]
  Eval -->|Outcome and feedback| Result[(Result history)]
  API -->|Security event| Audit[(Audit log)]
  Result -->|Authorized view| Student
```

## Component DFD requirements

1. Input and output data, external actors, and request initiators.
2. Processes and the stores they access.
3. Trust boundaries and server-side authorization checks.
4. Negative flows and handling of sensitive data.
5. The distinction between implemented and planned behavior.
