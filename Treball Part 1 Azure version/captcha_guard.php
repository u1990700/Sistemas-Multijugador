<?php
declare(strict_types=1);
if (session_status() !== PHP_SESSION_ACTIVE) {
    session_start();
}

function captcha_verify_and_consume(?string $user_input): bool {
    if (!isset($_SESSION['captcha_answer']) || !isset($_SESSION['captcha_expires'])) {
        return false;
    }
    $expected = $_SESSION['captcha_answer'];
    $exp = (int)$_SESSION['captcha_expires'];
    // Un solo uso: consumir siempre
    unset($_SESSION['captcha_answer'], $_SESSION['captcha_expires']);

    if (time() > $exp) return false;
    if ($user_input === null) return false;
    return (mb_strtoupper(trim($user_input)) === $expected);
}
