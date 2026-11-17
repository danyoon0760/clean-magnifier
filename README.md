# 깔끔한 돋보기 (CleanMagnifier)

화면의 일부를 실시간으로 확대해 보여 주는 Windows 돋보기입니다.
작은 사각형(소스)으로 확대할 영역을 고르면, 큰 창(뷰어)에 그 영역이 확대되어 나타납니다.

## 다운로드

[Releases](../../releases/latest) 에서 받으세요.

| 파일 | 설명 |
| --- | --- |
| `CleanMagnifier-portable-win-x64.zip` | 압축만 풀면 바로 실행됩니다. .NET 설치가 필요 없지만 크기가 큽니다. **처음이라면 이걸 받으세요.** |
| `CleanMagnifier-win-x64.zip` | 작은 실행 파일입니다. [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 이 설치된 PC에서만 실행됩니다. |

설치 과정은 없습니다. 압축을 풀고 `CleanMagnifier.exe` 를 실행하면 됩니다.

## 사용법

- **소스 사각형**을 끌어 확대할 곳을 고르고, 가장자리를 끌어 크기를 바꿉니다.
- **뷰어 창**에서 마우스 휠로 배율을 바꿉니다.
- 뷰어를 **우클릭**하면 설정 메뉴가 열립니다.
  - 배율, 화면 비율, 보간(픽셀 그대로 / 부드럽게), 새로고침(30 / 60 fps)
  - 소스 테두리 표시, 십자선, 색 반전, 마우스 따라가기
  - 테두리 색, 항상 위, 위치 초기화
- 알림 영역(트레이) 아이콘의 메뉴로 돋보기를 앞으로 가져오거나 종료할 수 있습니다.
- 종료는 우클릭 메뉴나 트레이의 **종료**로 합니다.

설정과 마지막 위치는 `%AppData%\CleanMagnifier\settings.json` 에 저장됩니다.

## 요구 사항

- Windows 10 2004(20H1) 이상, x64
  - 그 이전 버전에서는 돋보기 창이 확대 화면에 겹쳐 보일 수 있습니다.

## 직접 빌드하기

[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 가 필요합니다.

```powershell
dotnet test CleanMagnifier.slnx

# .NET 런타임이 필요한 작은 단일 EXE → publish\
dotnet publish CleanMagnifier -p:PublishProfile=Desktop

# .NET 설치 없이 실행되는 단일 EXE → publish-portable\
dotnet publish CleanMagnifier -p:PublishProfile=Portable
```

`publish-to-desktop.ps1` 은 Desktop 프로필로 빌드한 뒤 바탕화면에 `깔끔한 돋보기.exe` 로 복사합니다.
