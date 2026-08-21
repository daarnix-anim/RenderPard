# Project Rules

1. **Always read project specifications**: Before starting any new task, investigating bugs, or implementing new features, you MUST read the `project.md` file in the root of the workspace. This file contains the technical roadmap and specifications of the project.
2. **Review past tasks**: Check if there is a `task.md` or `roadmap.md` in the current conversation artifacts or the workspace to understand what was recently accomplished.
3. **Release Approval**: After making ANY code changes or edits, you MUST ask the user if they want to upload the changes to GitHub, push, and make a new release.
4. **Release Process**: When creating a release, the process is: (1) `dotnet publish RenderPard.UI\RenderPard.UI.csproj -c Release -o PublishOutput`. (2) Compile the installer using `& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" RenderPard_Installer.iss`. (3) Commit and push to git. (4) Upload the installer to the GitHub release using `gh release upload vX.X.X Output\RenderPard_Setup_vX.X.X.exe --clobber`.
