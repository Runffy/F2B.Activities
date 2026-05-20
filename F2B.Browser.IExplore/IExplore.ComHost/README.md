# IExplore.ComHost (x86)

Small **32-bit** helper executable for the x64 OpenRPA plugin. Contains only IE launch + window-wait logic (no `F2B.Browser.IExplore.dll`).

## Build

Open `IExplore.ComHost.sln` or build `IExplore.ComHost` from `F2B.Browser.IExplore.sln`.

Output:

```
F2B.Browser.IExplore\bin\Debug\x86\
  IExplore.ComHost.exe
  IExplore.ComHost.pdb
```

## Deploy to OpenRPA

```
%ProjectsDirectory%\extensions\
  F2B.Browser.IExplore.dll     (x64 plugin)
  x86\
    IExplore.ComHost.exe       (only this exe — no second plugin DLL)
```

## CLI

```
IExplore.ComHost.exe launch "http://127.0.0.1:17654/demo.html" --timeout 45000 --title "IExplore Test Host" --url-contains demo.html
```

Stdout: `OK method=InternetExplorer.Application hwnd=123456`
