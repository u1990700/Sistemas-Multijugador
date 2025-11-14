<?php
declare(strict_types=1);
if (session_status() !== PHP_SESSION_ACTIVE) { session_start(); }

$length = 5; $width = 160; $height = 50; $expires_s = 180;
$font_path = __DIR__ . '/fonts/DejaVuSans.ttf';

$alphabet = str_split('ABCDEFGHJKLMNPQRSTUVWXYZ23456789');
$code = '';
for ($i = 0; $i < $length; $i++) { $code .= $alphabet[random_int(0, count($alphabet)-1)]; }

$_SESSION['captcha_answer']  = $code;
$_SESSION['captcha_expires'] = time() + $expires_s;

$hasTrueColor = function_exists('imagecreatetruecolor');
$img = $hasTrueColor ? imagecreatetruecolor($width, $height) : imagecreate($width, $height);

$bg  = imagecolorallocate($img, 255, 255, 255);
$fg  = imagecolorallocate($img, 0, 0, 0);
$noise = imagecolorallocate($img, 120, 120, 120);
imagefilledrectangle($img, 0, 0, $width, $height, $bg);

// Ruido si hay truecolor
if ($hasTrueColor) {
  for ($i=0; $i<8; $i++) {
    imageline($img, random_int(0,$width), random_int(0,$height), random_int(0,$width), random_int(0,$height), $noise);
  }
  for ($i=0; $i<200; $i++) {
    imagesetpixel($img, random_int(0,$width-1), random_int(0,$height-1), $noise);
  }
}

// Texto (TTF si disponible)
if (function_exists('imagettftext') && file_exists($font_path)) {
  $font_size = 20; $angle = random_int(-12, 12);
  $bbox = imagettfbbox($font_size, $angle, $font_path, $code);
  $text_width  = $bbox[2] - $bbox[0];
  $text_height = $bbox[1] - $bbox[7];
  $x = (int)(($width - $text_width)/2);
  $y = (int)(($height + $text_height)/2);
  imagettftext($img, $font_size, $angle, $x, $y, $fg, $font_path, $code);
} else {
  $font = 5;
  $text_width = imagefontwidth($font) * strlen($code);
  $text_height = imagefontheight($font);
  $x = (int)(($width - $text_width)/2);
  $y = (int)(($height - $text_height)/2);
  imagestring($img, $font, $x, $y, $code, $fg);
}

header('Content-Type: image/png');
header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
header('Pragma: no-cache');
imagepng($img);
imagedestroy($img);
