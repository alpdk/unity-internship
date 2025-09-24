# Unity Game Development tooling in JetBrains Rider (Code task)

For using this code you need to create an execution file through one of the commands below:

For linux:
```aiignore
dotnet publish -c Release -r linux-x64 --self-contained false /p:PublishSingleFile=true /p:AssemblyName=tool
```

For windows:
```aiignore
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:AssemblyName=tool
```

For compfortable, you can run this line to move execution function to the root directory of the project:

Linux:
```aiignore
mv bin/Release/net9.0/linux-x64/publish/tool tool
```

Windows:
```aiignore
move bin\Release\net9.0\win-x64\publish\tool.exe .\
```
OR
```aiignore
Move-Item bin\Release\net9.0\win-x64\publish\tool.exe .\
```

After that, you can run the code by the code below:

Linux:
```aiignore
./tool <full path to the Unity project Directory> <full path to the output directory>
```

Windows:
```aiignore
./tool.exe <full path to the Unity project Directory> <full path to the output directory>
```

This project was made originally on Linux, so may be some troubles with Windows. If you will face them, please infor me.