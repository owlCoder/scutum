# Development workflow

1. Update `main` and create a short-lived branch for the component or task.
2. Reference the relevant requirement IDs from the project specification.
3. Implement clear module responsibilities, server-side checks, and validation.
4. Update the component architecture and data flow diagrams.
5. Include a negative test for access boundaries or invalid input where applicable.
6. Open a pull request describing verification and dependencies.
7. Do not rewrite `main`, commit secrets, or remove another team's work without agreement.

Build the initial API with `dotnet build backend/Platform.Api/Platform.Api.csproj`.
