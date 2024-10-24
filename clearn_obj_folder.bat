@echo off
setlocal
echo 删除当前目录及所有子目录下的obj文件夹及其内容...

rem 调用递归函数查找并删除 obj 文件夹
call :searchAndDelete "%cd%"

echo 操作完成。
endlocal
pause
exit /b

:searchAndDelete
set "currentFolder=%~1"
rem 遍历当前文件夹中的所有子文件夹
for /d %%d in ("%currentFolder%\*") do (
    if exist "%%d\obj" (
        echo 删除文件夹: %%d\obj
        rd /s /q "%%d\obj"
    )
    rem 对子文件夹进行递归调用
    call :searchAndDelete "%%d"
)
exit /b
