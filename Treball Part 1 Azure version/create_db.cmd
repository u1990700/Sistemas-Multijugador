@echo off
chcp 65001 >nul

if exist users.db (
  echo [91mJa existeix una base de dades anomenada "users.db"[0m
  echo.
  echo Elimina-la abans si la vols recrear des de zero
  goto finish
)

if exist games.db (
  echo [91mJa existeix una base de dades anomenada "games.db"[0m
  echo.
  echo Elimina-la abans si la vols recrear des de zero
  goto finish
)

sqlite3 users.db "CREATE TABLE IF NOT EXISTS `users` (`user_id` INTEGER PRIMARY KEY, `user_name` varchar(63), `user_password` varchar(255));"
sqlite3 users.db "CREATE UNIQUE INDEX `user_name_UNIQUE` ON `users` (`user_name` ASC);"
sqlite3 games.db "CREATE TABLE games (game_id TEXT PRIMARY KEY, name TEXT UNIQUE, player1 TEXT, player2 TEXT, points_player1 INTEGER DEFAULT 0, points_player2 INTEGER DEFAULT 0, circle_x INTEGER DEFAULT NULL, circle_y INTEGER DEFAULT NULL, circle_visible INTEGER DEFAULT 0, next_circle_time INTEGER DEFAULT NULL, winner TEXT, player1_x INTEGER DEFAULT NULL, player1_y INTEGER DEFAULT NULL, player2_x INTEGER DEFAULT NULL, player2_y INTEGER DEFAULT NULL);"

if exist users.db (
  echo [32mS'ha creat la base de dades "users.db"[0m
  echo.
  echo Si executes "sqlite3.exe" la pots carregar amb la comanda ".load users.db"
) else (
  echo [91mError: no s'ha pogut crear la base de dades "users.db"[0m
)

if exist games.db (
  echo [32mS'ha creat la base de dades "games.db"[0m
  echo.
  echo Si executes "sqlite3.exe" la pots carregar amb la comanda ".load games.db"
) else (
  echo [91mError: no s'ha pogut crear la base de dades "games.db"[0m
)

:finish
echo.
echo Prem qualsevol tecla per finalitzar...
pause >nul
