@echo off
rem Gọi Blender cho các công cụ Sân Gạch Đỏ.
rem   sgd kiem_tra [Ma ...]          kiểm tra mí khép (bỏ trống = mọi nhân vật)
rem   sgd canh file.json [...] [--chi-anh --khung 1,36]   render cảnh
rem   sgd mo_hinh Ma [--chi-cat] [--lam-lai] [--nguon meshy^|tripo]   dựng mô hình 3D qua Fal (cần FAL_KEY)
rem   sgd dau_3d Ma [--nguon meshy^|rodin^|ca_hai]   dựng riêng phần đầu từ ảnh chân dung qua Fal (cần FAL_KEY)
rem   sgd chieu_mat [Ma ...] [--chi-da] [--muc 0.7]   chiếu mặt gốc lên mô hình 3D (nắn hình + vẽ da)
setlocal
if "%SGD_BLENDER%"=="" set "SGD_BLENDER=E:\KitPlatform\.dev\blender\blender-4.5.10-windows-x64\blender.exe"
set "HERE=%~dp0"
if /I "%1"=="kiem_tra" goto kt
if /I "%1"=="canh" goto canh
if /I "%1"=="mo_hinh" goto mh
if /I "%1"=="chieu_mat" goto cm
if /I "%1"=="dau_3d" goto d3
echo Cach dung: sgd kiem_tra [Ma ...]  ^|  sgd canh file.json [--chi-anh --khung 1,36]  ^|  sgd mo_hinh Ma [--chi-cat]
exit /b 1
:kt
shift
"%SGD_BLENDER%" --background --python "%HERE%kiem_tra.py" -- %1 %2 %3 %4 %5 %6 %7 %8 %9
exit /b %errorlevel%
:canh
shift
"%SGD_BLENDER%" --background --python "%HERE%render_shot.py" -- %1 %2 %3 %4 %5 %6 %7 %8 %9
exit /b %errorlevel%
:mh
shift
"%SGD_BLENDER%" --background --python "%HERE%tao_mo_hinh_3d.py" -- %1 %2 %3 %4 %5 %6 %7 %8 %9
exit /b %errorlevel%
:cm
shift
"%SGD_BLENDER%" --background --python "%HERE%chieu_mat_nv.py" -- %1 %2 %3 %4 %5 %6 %7 %8 %9
exit /b %errorlevel%
:d3
shift
"%SGD_BLENDER%" --background --python "%HERE%tao_dau_3d.py" -- %1 %2 %3 %4 %5 %6 %7 %8 %9
exit /b %errorlevel%
