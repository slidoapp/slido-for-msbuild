# Slido Build Tasks

> Build tasks for **msbuild** by Slido developers.

## ChangeFileVersion Task

> Build task to change the Version in the File table of MSI package. 

This task has been added to handle cases where files need to be downgraded. By default, Windows Installer does not overwrite files if the version in the package is the same or lower than the one already installed (for `*.dll` files, this corresponds to the `AssemblyVersion`). If a problematic file needs to be downgraded, there is no built-in mechanism to achieve this. To work around this, we must “trick” Windows Installer into believing that a higher version is being installed, even though the actual file version is lower.

To use this task, first add the `<UsingTask>` element to the `.targets` file and specify the `TaskName` and `AssemblyFile` attributes. The `TaskName` is in this case `Slido.Build.ChangeFileVersion`.

Then add `<ItemGroup>` with e.g. `<ChangeFile>` elements as in following example. `Include` attribute contains the file name that should have the version updated and the `<Version>` element contains the new version value. 

```xml
<ItemGroup>
  <ChangeFile Include="Sentry.dll">
    <Version>5.11.1.1</Version>
  </ChangeFile>
  <ChangeFile Include="Serilog.dll">
    <Version>4.3.0.1</Version>
  </ChangeFile>
</ItemGroup>
```

To use the task in particular target, Create `<ChangeFileVersion>` element with `FIles` and `MsiPath` attributes. In the example the task is executed in the `AfterCompileAndLink` target where the MSI database is already build.

```xml
<Target Name="AfterCompileAndLink"> 
  <ChangeFileVersion Files="@(ChangeFile)" MsiPath="$(TargetPath)" />
</Target>
```

## License

Source code is licensed under [MIT License](LICENSE.txt).
