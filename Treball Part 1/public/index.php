<?php

/**
 * IMPORTANTE
 * En php.ini hay que descomentar la linea "extension=openssl"
 */

/*
    - IMPLEMENTACIO DE LA PRACTICA 1 DE SISTEMES MULTIJUGADOR -
    - AUTORS: Ismael Rodriguez, Robert Lopez, Carlos Urbina

    - DESCRIPCIO: Aquest fitxer gestiona el registre, login, logout i recuperacio de contrasenya
      mitjançant SMS. Utilitza una base de dades SQLite per emmagatzemar els usuaris.
      Inclou verificació CAPTCHA.
      Les contrasenyes es guarden de forma segura amb hash bcrypt.
      S'utilitza la API de Nexmo per enviar SMS i Resend per enviar emails 
      (Te un limit baix d'ussos per el que es planteja cambiar en un futur proper).
      La interfície s'administra mitjançant plantilles HTML amb placeholders.
      La gestió de sessions es fa amb PHP_SESSION i paràmetres adequats.

*/
$template = 'home';
$db_connection = 'sqlite:..\private\users.db';
$configuration = array(
    '{FEEDBACK}' => '',
    '{LOGIN_LOGOUT_TEXT}' => 'Identificar-me',
    '{JUGAR_TEXT}' => 'Jugar',
    '{LOGIN_LOGOUT_URL}' => '?page=login',
    '{METHOD}' => 'POST',
    '{REGISTER_URL}' => '?page=register',
    '{SITE_NAME}' => 'La meva pàgina',
    '{RECUPERAR_PSSW}' => '?page=recuperar',

);
session_set_cookie_params([
    'lifetime' => 0,         // cookie d'inici de sesio (s'elimina al tancar el navegador)
    'path' => '/',
    'domain' => '',        // per defecte
    'secure' => false,
    'httponly' => true,
    'samesite' => 'Lax'
]);
session_start();

//función de verificación de CAPTCHA
require_once __DIR__ . '/captcha_guard.php';

$templates = array("jugar", "crear_partida", "unir_partida");

if (isset($_GET['page']) && in_array($_GET['page'], $templates) && empty($_SESSION['user'])) {
    header('Location: ?page=home');
    exit;
}

if (isset($_GET['page']) && $_GET['page'] === 'logout') {
    $_SESSION = [];
    if (ini_get('session.use_cookies')) {
        $params = session_get_cookie_params();
        setcookie(session_name(), '', time() - 42000, $params['path'], $params['domain'], $params['secure'], $params['httponly']);
    }
    session_destroy();
    header('Location: ?page=login');
    exit;
    
} else if (!empty($_SESSION['user']) && !in_array($_GET['page'] ?? '', $templates)) {
    $template = 'perfil';
    $configuration['{FEEDBACK}'] = 'Has iniciat sessió com <b>' . htmlentities($_SESSION['user']) . '</b>';
    $configuration['{LOGIN_LOGOUT_TEXT}'] = 'Tancar "sessió"';
    $configuration['{LOGIN_LOGOUT_URL}'] = '?page=logout';
    $configuration['{JUGAR_URL}'] = '?page=jugar';

} else if (isset($_POST['register'])) {
    // CAPTCHA check (antes de crear el usuario)
    if (!captcha_verify_and_consume($_POST['captcha'] ?? null)) {
        $configuration['{FEEDBACK}'] = 'ERROR Codi CAPTCHA invàlid o expirat. Torna-ho a provar.';
        $template = 'register';
        $html = file_get_contents('plantilla_' . $template . '.html', true);
        $html = str_replace(array_keys($configuration), array_values($configuration), $html);
        echo $html;
        exit;
    }

    // --- Validación servidor ---
    $username = trim($_POST['user_name'] ?? '');
    $password = $_POST['user_password'] ?? '';

    // Fuerte: 10+, minúscula, mayúscula, dígito y símbolo
    $strongPwd = '/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{10,}$/';

    // Valida usuario (opcional: ajusta a tu gusto)
    if ($username === '') {
        $configuration['{FEEDBACK}'] = '<mark>ERROR: El nom d\'usuari és obligatori.</mark>';
        $template = 'register';
        $configuration['{REGISTER_USERNAME}'] = '';
    }
    // Valida contraseña
    else if (!preg_match($strongPwd, $password)) {
        $configuration['{FEEDBACK}'] =
            '<mark>ERROR: Contrasenya dèbil.</mark> ' .
            'Ha de tenir com a mínim 10 caràcters, incloent ' .
            'una majúscula, una minúscula, un número i un símbol.';
        $template = 'register';
        $configuration['{REGISTER_USERNAME}'] = htmlentities($username);
    } else {
        try {
            $db = new PDO($db_connection);
            $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
            $db->exec("PRAGMA busy_timeout = 5000");
            $db->exec("PRAGMA journal_mode = WAL");

            $sql = 'INSERT INTO users (user_name, user_password) VALUES (:user_name, :user_password)';
            $query = $db->prepare($sql);
            $passHash = password_hash($password, PASSWORD_BCRYPT);

            $query->bindValue(':user_name', $username);
            $query->bindValue(':user_password', $passHash);

            if ($query->execute()) {
                $configuration['{FEEDBACK}'] = 'Creat el compte <b>' . htmlentities($username) . '</b>';
                $configuration['{LOGIN_LOGOUT_TEXT}'] = 'Tancar sessió';
                $template = 'login';
                $configuration['{LOGIN_USERNAME}'] = htmlentities($username);

            }
            $db = null;
        } catch (Exception $e) {
            $configuration['{FEEDBACK}'] =
                "<mark>ERROR: No s'ha pogut crear el compte <b>" . htmlentities($username) . "</b></mark>";
            $template = 'register';
            $configuration['{REGISTER_USERNAME}'] = htmlentities($username);

        }
    }

} else if (isset($_POST['request_reset'])) {

    // Establim conexio amb la BD
    $db = new PDO($db_connection);
    $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    $db->exec("PRAGMA busy_timeout = 5000");
    $db->exec("PRAGMA journal_mode = WAL");

    // Obtenim dades del formulari
    $username = $_POST['user_name'] ?? '';
    $phone = $_POST['phone'] ?? '';

    // Busquem l'usuari a la BD
    $sql = "SELECT user_id FROM users WHERE user_name = :user_name";
    $query = $db->prepare($sql);
    $query->bindValue(':user_name', $username);
    $query->execute();
    $user = $query->fetch(PDO::FETCH_ASSOC);

    // Si l'usuari existeix, generem i enviem codi de reset
    if ($user) {
        // Generem codig aleatori de 6 digits
        $resetCode = str_pad(random_int(0, 999999), 6, '0', STR_PAD_LEFT);

        $_SESSION['reset_user'] = $user['user_id'];
        $_SESSION['reset_code'] = $resetCode;

        // === Enviar SMS ===
        $apiKey = "d73012ba";
        $apiSecret = "NgKoGfdHa9LnFKrU";
        $from = "MiApp";
        $to = $phone;
        $text = "El teu codi de recuperació es: " . $resetCode;
        $url = "https://rest.nexmo.com/sms/json";
        $data = [
            'api_key' => $apiKey,
            'api_secret' => $apiSecret,
            'to' => $to,
            'from' => $from,
            'text' => $text
        ];
        // Enviament mitjançant cURL
        $ch = curl_init($url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
        curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($data));
        // Afegim certificat CA per a connexió segura (genera errors sense això)
        curl_setopt($ch, CURLOPT_CAINFO, __DIR__ . '/../private/cacert.pem');
        $response = curl_exec($ch);
        curl_close($ch);

        $configuration['{FEEDBACK}'] = "S'ha enviat un codi de recuperació al teu telèfon.";
        $template = "reset";
        // si no existeix, feedback d'error i netejem les dades
    } else {
        $configuration['{FEEDBACK}'] = "Usuari no trobat";
        $template = "recuperar";
    }

    // processar reset de contrasenya
} else if (isset($_POST['verify_reset'])) {

    // Establim conexio amb la BD
    $db = new PDO($db_connection);
    $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    $db->exec("PRAGMA busy_timeout = 5000");
    $db->exec("PRAGMA journal_mode = WAL");

    // Obtenim dades del formulari
    $code = $_POST['reset_code'] ?? '';
    $newpass = $_POST['new_password'] ?? '';

    // Si el codi es correcte i hi ha usuari a la sessio, actualitzem la contrasenya
    if (isset($_SESSION['reset_user']) && $code === $_SESSION['reset_code']) {
        // generar hash de la nueva password
        $hash = password_hash($newpass, PASSWORD_BCRYPT);
        // actualizar la password a la base de dades
        $sql = "UPDATE users SET user_password = :pwd WHERE user_id = :id";
        $query = $db->prepare($sql);
        $query->bindValue(':pwd', $hash);
        $query->bindValue(':id', $_SESSION['reset_user']);
        $query->execute();

        unset($_SESSION['reset_user'], $_SESSION['reset_code']);
        $configuration['{FEEDBACK}'] = "Contrasenya cambiada correctamente. Ja pots identificar-te.";
        $template = "login";
    } else {
        $configuration['{FEEDBACK}'] = "Codi invàlid. Torna-ho a provar.";
        $template = "reset";
    }

} else if (isset($_POST['login'])) {

    // Pequeño retardo anti-bruteforce (~0.3s)
    usleep(300000);

    // CAPTCHA check (antes de consultar la BD)
    if (!captcha_verify_and_consume($_POST['captcha'] ?? null)) {
        $configuration['{FEEDBACK}'] = 'ERROR Codi CAPTCHA invàlid o expirat. Torna-ho a provar.';
        $template = 'login';
        $html = file_get_contents('plantilla_' . $template . '.html', true);
        $html = str_replace(array_keys($configuration), array_values($configuration), $html);
        echo $html;
        exit;
    }

    $db = new PDO($db_connection);
    $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
    $db->exec("PRAGMA busy_timeout = 5000");
    $db->exec("PRAGMA journal_mode = WAL");

    $sql = 'SELECT * FROM users WHERE user_name = :user_name LIMIT 1';
    $query = $db->prepare($sql);
    $query->bindValue(':user_name', $_POST['user_name']);
    $query->execute();
    $result_row = $query->fetchObject();

    if ($result_row && password_verify($_POST['user_password'], $result_row->user_password)) {
        // Protege contra fijación de sesión
        session_regenerate_id(true);
        $_SESSION['user'] = $result_row->user_name;

        $configuration['{FEEDBACK}'] = '"Sessió" iniciada com <b>' . htmlentities($_SESSION['user'], ENT_QUOTES, 'UTF-8') . '</b>';
        $configuration['{LOGIN_LOGOUT_TEXT}'] = 'Tancar "sessió"';
        $configuration['{LOGIN_LOGOUT_URL}'] = '?page=logout';
        $template = 'home';

        $db = null;
        session_write_close();              // asegura que la sesión se persiste
        header('Location: ?page=perfil');     // redirige a home
        exit;


    } else {
        $configuration['{FEEDBACK}'] = '<mark>ERROR: Usuari desconegut o contrasenya incorrecta</mark>';

        $template = 'login';
        $configuration['{LOGIN_USERNAME}'] = htmlentities($_POST['user_name'] ?? '', ENT_QUOTES, 'UTF-8');
    }


    // navegación por GET (mostrar vistas)
// crear partida
} else if (isset($_POST['crear_partida'])) {

    if (empty($_SESSION['user'])) {
        header('Location: ?page=login');
        exit;
    }

    $nom_partida = trim($_POST['nom_partida'] ?? '');
    if ($nom_partida === '') {
        $configuration['{FEEDBACK}'] = '<mark>ERROR: Has d\'introduir un nom de partida.</mark>';
        $template = 'crear_partida';
    } else {
        try {
            $db = new PDO('sqlite:../private/games.db');
            $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
            $db->exec("PRAGMA journal_mode=WAL");
            $db->exec("PRAGMA busy_timeout=5000");

            // Comprobar si ya existe una partida con ese nombre
            $stmt = $db->prepare('SELECT game_id FROM games WHERE name = :name');
            $stmt->execute([':name' => $nom_partida]);
            $existing = $stmt->fetch(PDO::FETCH_ASSOC);

            if ($existing) {
                $configuration['{FEEDBACK}'] = '<mark>ERROR: Ja existeix una partida amb aquest nom.</mark>';
                $template = 'crear_partida';
            } else {
                // Crear la partida con ese nombre único
                $game_id = bin2hex(random_bytes(8));
                $player = $_SESSION['user'];

                $stmt = $db->prepare('INSERT INTO games 
                    (game_id, name, player1, player1_x, player1_y, player2_x, player2_y, circle_x, circle_y)
                    VALUES (:id, :name, :p1, 0, 0, 0, 0, 0, 0)');
                $stmt->execute([
                    ':id' => $game_id,
                    ':name' => $nom_partida,
                    ':p1' => $player
                ]);

                header("Location: game.html?game_name=" . urlencode($nom_partida) . "&player=$player");
                exit;
            }
        } catch (Exception $e) {
            $configuration['{FEEDBACK}'] = "<mark>ERROR creant la partida: " . htmlentities($e->getMessage()) . "</mark>";
            $template = 'crear_partida';
        }
    }



// unir-se a partida 
} else if (isset($_POST['unir_partida'])) {

    if (empty($_SESSION['user'])) {
        header('Location: ?page=login');
        exit;
    }

    $nom_partida = trim($_POST['codi_partida'] ?? '');
    if ($nom_partida === '') {
        $configuration['{FEEDBACK}'] = '<mark>ERROR: Has d\'introduir el nom de la partida.</mark>';
        $template = 'unir_partida';
    } else {
        try {
            $db = new PDO('sqlite:../private/games.db');
            $db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
            $db->exec("PRAGMA journal_mode=WAL");
            $db->exec("PRAGMA busy_timeout=5000");

            $stmt = $db->prepare('SELECT * FROM games WHERE name = :name');
            $stmt->execute([':name' => $nom_partida]);
            $partida = $stmt->fetch(PDO::FETCH_ASSOC);

            if (!$partida) {
                $configuration['{FEEDBACK}'] = '<mark>ERROR: No existeix cap partida amb aquest nom.</mark>';
                $template = 'unir_partida';
            } elseif (!empty($partida['player2'])) {
                $configuration['{FEEDBACK}'] = '<mark>ERROR: Aquesta partida ja està plena.</mark>';
                $template = 'unir_partida';
            } else {
                $player = $_SESSION['user'];
                $stmt = $db->prepare('UPDATE games SET player2 = :p2 WHERE name = :name');
                $stmt->execute([':p2' => $player, ':name' => $nom_partida]);

                header("Location: game.html?game_name=" . urlencode($nom_partida) . "&player=$player");
                exit;
            }
        } catch (Exception $e) {
            $configuration['{FEEDBACK}'] = "<mark>ERROR unint-se a la partida: " . htmlentities($e->getMessage()) . "</mark>";
            $template = 'unir_partida';
        }
    }

    
} else if (isset($_GET['page'])) {

    if ($_GET['page'] == 'register') {
        $template = 'register';
        $configuration['{REGISTER_USERNAME}'] = '';
        $configuration['{LOGIN_LOGOUT_TEXT}'] = 'Ja tinc un compte';

    } else if ($_GET['page'] == 'login') {
        $template = 'login';
        $configuration['{LOGIN_USERNAME}'] = '';

    } else if ($_GET['page'] == 'recuperar') {
        $template = 'recuperar';
        $configuration['{LOGIN_LOGOUT_TEXT}'] = 'Identificar-me';
        $configuration['{LOGIN_LOGOUT_URL}'] = '?page=login';

    } else if ($_GET['page'] == 'jugar') {
        $template = 'jugar';
        $configuration['{FEEDBACK}'] = 'Escull una opció per començar a jugar:';
        $configuration['{CREAR_PARTIDA_URL}'] = '?page=crear_partida';
        $configuration['{UNIR_PARTIDA_URL}'] = '?page=unir_partida';

    } else if ($_GET['page'] == 'crear_partida') {
        $template = 'crear_partida';
        $configuration['{FEEDBACK}'] = 'Has triat crear una nova partida.';

    } else if ($_GET['page'] == 'unir_partida') {
        $template = 'unir_partida';
        $configuration['{FEEDBACK}'] = 'Introdueix el codi de la partida per unir-t’hi.';
    
    } else {
        $template = 'home';
        
    }
}

// process template and show output
$html = file_get_contents('plantilla_' . $template . '.html', true);
$html = str_replace(array_keys($configuration), array_values($configuration), $html);
echo $html;
