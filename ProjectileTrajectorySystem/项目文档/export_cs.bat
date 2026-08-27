@echo off
chcp 65001 >nul

set "output=cs_files_content.txt"
echo 正在导出 .cs 文件内容到 %output% ...
echo.

> "%output%" (
    for %%f in (*.cs) do (
        echo.
        echo 【%%f】
        type "%%f"
        echo.
        echo ========================================
    )
)

echo 导出完成！共处理 %cd% 目录下的 .cs 文件
echo 输出文件：%output%
pause