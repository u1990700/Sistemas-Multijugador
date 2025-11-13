<?php
// game.php — versión optimizada

session_start();                   // si usas la sesión para identificar al jugador
$player_session_id = $_SESSION['user'] ?? null;
session_write_close();             // <- MUY IMPORTANTE: evitar bloqueo entre peticiones

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
header('Pragma: no-cache');



// Helper para leer parámetros tanto de GET como de POST
function inparam($key, $default = null)
{
    return $_POST[$key] ?? $_GET[$key] ?? $default;
}

// Conectar a SQLite con PRAGMAs que reducen bloqueos
try {
    $db = new PDO('sqlite:../private/games.db');
    $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    $db->exec('PRAGMA journal_mode=WAL;');
    $db->exec('PRAGMA synchronous=NORMAL;');
    $db->exec('PRAGMA busy_timeout=250;');
} catch (PDOException $e) {
    echo json_encode(['error' => 'Connexió amb la base de dades fallida: ' . $e->getMessage()]);
    exit;
}

$accio = inparam('action', '');


// Asegurar que existe la columna/tabla esperada (opcional duro)
// Aquí asumimos que ya existe tabla games con columnas: 
// game_id TEXT PK, player1 TEXT, player2 TEXT, player1_x INT, player1_y INT, player2_x INT, player2_y INT, circle_x INT, circle_y INT, winner TEXT NULL

switch ($accio) {
    case 'join': {
        $numJugador = 0;

        // Obtenemos los parámetros
        session_start();
        $game_name = inparam('game_name');
        $circle_x = inparam('circle_x', 0);
        $circle_y = inparam('circle_y', 0);
        $player_id = $_SESSION['user'];

        // Hacemos la consulta buscando la partida
        $stmt = $db->prepare('SELECT * FROM games WHERE name = :name');
        $stmt->execute([':name' => $game_name]);

        // Obtenemos una fila
        $joc = $stmt->fetch(PDO::FETCH_ASSOC);

        if (!$joc) {
            echo json_encode(['error' => 'Joc no trobat']);
            exit;
        }

        if ($joc['player1'] == $player_id) {
            $stmt = $db->prepare('UPDATE games SET circle_x = :x, circle_y = :y WHERE name = :game_name');
            $stmt->execute([':x' => $circle_x, ':y' => $circle_y, ':game_name' => $game_name]);
            $numJugador = 1;

        }elseif ($joc['player2'] == $player_id) {
            $numJugador = 2;
            $circle_x = $joc['circle_x'];
            $circle_y = $joc['circle_y'];

        }else {
            echo json_encode(['error' => 'Jugador no trobat a la partida']);
            exit;
        }

        echo json_encode([
            'game_name' => $game_name,
            'game_id' => $joc['game_id'],
            'num_jugador' => $numJugador,
            'circle_x' => $circle_x,
            'circle_y' => $circle_y,
            'player_id' => $player_id

        ]);
        exit;
    }


    case 'status': {
        $game_id = inparam('game_id', '');
        if ($game_id === '') {
            echo json_encode(['error' => 'Falta game_id']);
            exit;
        }

        $stmt = $db->prepare('SELECT * FROM games WHERE name = :game_id');
        $stmt->execute([':game_id' => $game_id]);
        $joc = $stmt->fetch(PDO::FETCH_ASSOC);

        if (!$joc) {
            echo json_encode(['error' => 'Joc no trobat']);
            exit;
        }

        echo json_encode([
            'ok' => 'todo ok',
            'player1_x' => $joc['player1_x'],
            'player1_y' => $joc['player1_y'],
            'player2_x' => $joc['player2_x'],
            'player2_y' => $joc['player2_y'],
            'circle_x' => $joc['circle_x'],
            'circle_y' => $joc['circle_y'],
            'points_player1' => (int) ($joc['points_player1'] ?? 0),
            'points_player2' => (int) ($joc['points_player2'] ?? 0),
        ]);
        exit;
    }


    case 'movement': {
        // Aceptamos POST preferentemente
        $game_id = inparam('game_id', '');
        $player_x = (int) inparam('player_x', 0);
        $player_y = (int) inparam('player_y', 0);

        if ($game_id === '' || !$player_session_id) {
            echo json_encode(['error' => 'Paràmetres invàlids o sessió no vàlida']);
            exit;
        }

        $stmt = $db->prepare('SELECT player1, player2, winner FROM games WHERE name = :game_id');
        $stmt->execute([':game_id' => $game_id]);
        $joc = $stmt->fetch(PDO::FETCH_ASSOC);

        if (!$joc || !empty($joc['winner'])) {
            echo json_encode(['error' => 'Joc finalitzat o no trobat']);
            exit;
        }

        if ($joc['player1'] === $player_session_id) {
            $stmt = $db->prepare('UPDATE games SET player1_x = :x, player1_y = :y WHERE name = :game_id');
            $stmt->execute([':x' => $player_x, ':y' => $player_y, ':game_id' => $game_id]);
        } elseif ($joc['player2'] === $player_session_id) {
            $stmt = $db->prepare('UPDATE games SET player2_x = :x, player2_y = :y WHERE name = :game_id');
            $stmt->execute([':x' => $player_x, ':y' => $player_y, ':game_id' => $game_id]);
        } else {
            echo json_encode(['error' => 'Jugador invàlid']);
            exit;
        }

        echo json_encode(['ok' => 1]);
        exit;
    }

    case 'actualizarCirculo': {
        $game_id = inparam('game_id', '');
        $circle_x = (int) inparam('circle_x', 0);
        $circle_y = (int) inparam('circle_y', 0);

        if ($game_id === '') {
            echo json_encode(['error' => 'Falta game_id']);
            exit;
        }

        $stmt = $db->prepare('UPDATE games SET circle_x = :cx, circle_y = :cy WHERE name = :game_id');
        $stmt->execute([':cx' => $circle_x, ':cy' => $circle_y, ':game_id' => $game_id]);

        echo json_encode(['ok' => 1]);
        exit;
    }

    // --- ping (para tu medidor de RTT) ---
    case 'ping': {
        echo json_encode([
            'ok' => 1,
            'server_time_ms' => (int) round(microtime(true) * 1000)
        ]);
        exit;
    }

    // --- marcar que el círculo ha sido recogido ---
    case 'circle_collected': {
        $game_id = inparam('game_id', '');
        if ($game_id === '') {
            echo json_encode(['error' => 'Falta game_id']);
            exit;
        }

        // Poner el círculo a NULL (señal de "no hay círculo ahora")
        $stmt = $db->prepare('UPDATE games SET circle_x = NULL, circle_y = NULL WHERE name = :game_id');
        $stmt->execute([':game_id' => $game_id]);

        echo json_encode(['ok' => 1]);
        exit;
    }

    // --- sumar punto y eliminar círculo ---
    case 'add_point': {
        $game_id = inparam('game_id', '');
        $player_id = $player_session_id;
        if ($game_id === '' || !$player_id) {
            echo json_encode(['error' => 'Falta game_id o sesión inválida']);
            exit;
        }

        // Cargar juego
        $stmt = $db->prepare('SELECT * FROM games WHERE name = :game_id');
        $stmt->execute([':game_id' => $game_id]);
        $joc = $stmt->fetch(PDO::FETCH_ASSOC);

        if (!$joc) {
            echo json_encode(['error' => 'Juego no encontrado']);
            exit;
        }

        // Actualizar puntuación según jugador
        if ($joc['player1'] === $player_id) {
            $stmt = $db->prepare('UPDATE games SET points_player1 = points_player1 + 1, circle_x = NULL, circle_y = NULL WHERE name = :game_id');
        } elseif ($joc['player2'] === $player_id) {
            $stmt = $db->prepare('UPDATE games SET points_player2 = points_player2 + 1, circle_x = NULL, circle_y = NULL WHERE name = :game_id');
        } else {
            echo json_encode(['error' => 'Jugador inválido']);
            exit;
        }

        $stmt->execute([':game_id' => $game_id]);

        // Devolver puntuación actualizada
        $stmt = $db->prepare('SELECT points_player1, points_player2 FROM games WHERE name = :game_id');
        $stmt->execute([':game_id' => $game_id]);
        $p = $stmt->fetch(PDO::FETCH_ASSOC);

        echo json_encode([
            'ok' => 1,
            'p1_points' => (int) $p['points_player1'],
            'p2_points' => (int) $p['points_player2']
        ]);
        exit;
    }


    default:
        echo json_encode(['error' => 'Acció desconeguda']);
        exit;
}

