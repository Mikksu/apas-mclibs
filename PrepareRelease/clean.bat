@echo off
setlocal enabledelayedexpansion

:: 配置要遍历的文件夹路径
set "TARGET_DIR=..\..\..\Release\"

:: 配置要删除的文件模式（可添加多个模式）
set "DELETE_PATTERNS=*.pdb apas.corelib.dll apas.mclib.sdk.dll MathNet.Numerics.dll Newtonsoft.Json.dll"

:: 遍历指定文件夹内的所有子文件夹
for /d /r "%TARGET_DIR%" %%D in (*) do (
    echo Entering Directory: %%D
    pushd "%%D"
    for %%P in (%DELETE_PATTERNS%) do (
        for %%F in (%%P) do (
            if exist "%%F" (
                echo Deleting File: %%D\%%F
                del /f /q "%%F"
            )
        )
    )
    popd
)

echo Clean Done!
exit /b