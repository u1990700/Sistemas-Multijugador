// Afegeix aquí el teu codi JavaScript
function refreshCaptcha(imgId='captcha-img') {
    const img = document.getElementById(imgId);
    if (img) {
        img.src = 'captcha.php?_=' + Date.now();
    }
}
