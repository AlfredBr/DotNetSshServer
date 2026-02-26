## General Guidelines

- Do not try to build the app.  we will use 'dotnet watch' in another shell and paste in the results.
- Always update the readme after any significant changes
- Always update the release notes after every change

## Platform Awareness

- When working on Windows/WSL environments, always confirm the current platform before running shell commands. Use PowerShell syntax on Windows and bash on WSL/Linux. Never assume bash is available on Windows.
- Add under a ## Build & Verification section\n\nAfter claiming all build warnings or errors are fixed, always run the actual build command one final time and verify the output before reporting success.
- Add under a ## Code Modifications section\n\nWhen doing bulk renames (namespaces, packages, files), use grep/search to find ALL references including 'using static', partial matches, and string literals before making changes. Verify with a full build after.