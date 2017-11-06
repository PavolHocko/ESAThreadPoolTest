@Echo off 

for /l %%i in (0, 1, 20) do (
for /l %%x in (300, 200, 2500) do (
   ESAThreadPoolTest %%x 1
   ESAThreadPoolTest %%x 2
))

pause