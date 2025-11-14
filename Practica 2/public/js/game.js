// game.js — versión optimizada (menos lag)


// --- Variables globales ---


let idJoc = null;
let idJugador = null;
let numJugador = null;

let Player1;
let Player2;

let p1_points = 0;
let p2_points = 0;



let circle = { x: 0, y: 0, radius: 15, visible: false };

let netStatusTimer = null;     // intervalo para leer estado
let netMoveTimer = null;       // intervalo para enviar movimiento
let circleInterval = null;     // intervalo para crear círculo (solo J1)

// Para throttling de movimiento
const NET_MOVE_HZ = 10;       // ~10 Hz (cada 100 ms) es suficiente
const NET_STATUS_HZ = 0.15;    // 
const MOVE_EPS = 1.5;          // umbral de cambio


const POLLING_SLOW_MS = 50;    // Polling lent (1 Hz) per a l'estat general (quan no et mous)
const FAST_POLLING_DURATION = 1000; // Temps (ms) que el polling ràpid es manté actiu després d'una tecla
let pollingTimeout = null;       // Per gestionar el retorn al mode lent


let lastRemoteUpdate = Date.now(); // Per rastrejar l'última vegada que vam rebre dades

const MAX_SPEED = 3; // Definir la velocitat màxima per claredat


let lastSentX = null;
let lastSentY = null;

// --- Funcions útils (afegides en converses anteriors, no mostrades aquí) ---
// function getUrlParameter(name) { ... } 


// --- Inicio del juego ---
function startGame() {
  // Aquí assumim que idJoc s'obté de la URL amb getUrlParameter()
  // idJoc = getUrlParameter('game_name'); 

  Player1 = new component(30, 30, "red", 10, 120);
  Player2 = new component(30, 30, "blue", 300, 120);

  myGameArea.start();

  // Círculo inicial local (el servidor lo sobreescribirá con el real)
  createCircleLocal();

  // Solo el Jugador 1 generará nuevos círculos cuando no haya uno visible
  // (seguiremos sincronizados porque persistimos circle_x/y en servidor)
  circleInterval = setInterval(() => {
    if (numJugador === 1 && !circle.visible) {
      createCircleAndSync();
    }
  }, 2000);

  addNetStatsLabel();        // añade el marcador a la UI
  startLatencyMonitor();     // empieza a medir el ping
  unirseAlJoc();
}

// --- Lienzo ---
const myGameArea = {
  canvas: document.createElement("canvas"),
  start: function () {
    this.canvas.width = 480;
    this.canvas.height = 270;
    this.context = this.canvas.getContext("2d");
    document.body.insertBefore(this.canvas, document.body.childNodes[0]);
    this.interval = setInterval(updateGameArea, 10); // ~50 FPS
  },
  clear: function () {
    this.context.clearRect(0, 0, this.canvas.width, this.canvas.height);
  },
};

// --- Entidad base ---
function component(width, height, color, x, y) {
  this.width = width;
  this.height = height;
  this.speedX = 0;
  this.speedY = 0;
  this.x = x;
  this.y = y;
  this.update = function () {
    const ctx = myGameArea.context;
    ctx.fillStyle = color;
    ctx.fillRect(this.x, this.y, this.width, this.height);
  };
  this.newPos = function () {
    this.x += this.speedX;
    this.y += this.speedY;
    // Limitar dentro del canvas
    if (this.x < 0) { this.x = 0; this.speedX = 0; }
    if (this.x + this.width > myGameArea.canvas.width) {
      this.x = myGameArea.canvas.width - this.width; this.speedX = 0;
    }
    if (this.y < 0) { this.y = 0; this.speedY = 0; }
    if (this.y + this.height > myGameArea.canvas.height) {
      this.y = myGameArea.canvas.height - this.height; this.speedY = 0;
    }
  };
}

// --- Bucle de render ---
function updateGameArea() {
  myGameArea.clear();

  Player1.newPos();
  Player1.update();

  Player2.newPos();
  Player2.update();

  drawCircle();

  // Colisiones y puntos (local)
  if (checkCollision(Player1)) {
    if (circle.visible) {
      circle.visible = false;
      // avisar al servidor que el círculo ya no está
      enviarPuntoAlServidor();
    }
  }
  if (checkCollision(Player2)) {
    if (circle.visible) {
      circle.visible = false;
      // avisar al servidor que el círculo ya no está
      enviarPuntoAlServidor();
    }
  }

  // Pintar puntuación
  const ctx = myGameArea.context;
  ctx.fillStyle = "black";
  ctx.font = "16px Arial";
  ctx.fillText("P1: " + p1_points, 10, 20);
  ctx.fillText("P2: " + p2_points, 400, 20);

  // Condición de victoria local
  if (p1_points >= 10 || p2_points >= 10) {
    clearInterval(myGameArea.interval);
    clearInterval(circleInterval);
    if (netStatusTimer) clearInterval(netStatusTimer);
    if (netMoveTimer) clearInterval(netMoveTimer);

    ctx.fillStyle = "green";
    ctx.font = "32px Arial";
    const winner = p1_points >= 10 ? "¡Gana el Jugador 1!" : "¡Gana el Jugador 2!";
    ctx.fillText(winner, 120, 140);
  }
}

// --- Alta en el juego ---
function unirseAlJoc() {
  // Aquí s'hauria de passar idJoc si s'hagués implementat getUrlParameter
  fetch(`game.php?action=join&circle_x=${Math.round(circle.x)}&circle_y=${Math.round(circle.y)}`, {
    method: 'GET',
    cache: 'no-store'
  })

    .then(r => r.json())
    .then(data => {
      idJoc = data.game_id;
      idJugador = data.player_id;
      numJugador = data.num_jugador;

      // Sincronizar círculo inicial desde servidor
      if (Number.isFinite(data.circle_x) && Number.isFinite(data.circle_y)) {
        circle.x = Number(data.circle_x);
        circle.y = Number(data.circle_y);
        circle.visible = true;
      }

      // Arrancar bucles de red
      arrancarRed();
    })
    .catch(console.error);
}

function setPollingSpeed(intervalMs) {
    if (netStatusTimer) {
        clearInterval(netStatusTimer);
    }
    netStatusTimer = setInterval(comprovarEstatDelJoc, intervalMs);
}

// game.js (Funció arrancarRed - Per Test Ràpid)
function arrancarRed() {
  // Comença amb polling lent (1 Hz)
  setPollingSpeed(POLLING_SLOW_MS);
}

// --- Leer estado del servidor ---
function comprovarEstatDelJoc() {
  if (!idJoc) return;

  fetch(`game.php?action=status&game_id=${idJoc}`, { method: 'GET', cache: 'no-store' })
    .then(response => response.json())
    .then(joc => {
      if (joc.error) {
        console.warn(joc.error);
        return;
      }
      
            let PlayerRemot = numJugador === 1 ? Player2 : Player1;
            let remot_speedX = numJugador === 1 ? joc.player2_x : joc.player1_x;
            let remot_speedY = numJugador === 1 ? joc.player2_y : joc.player1_y;

            // 1. Sincronització normal de velocitat
            if (remot_speedX != null || remot_speedY != null) {
                // Sols actualitzem si la velocitat real no és zero
                if (remot_speedX != 0 || remot_speedY != 0) { 
                    PlayerRemot.speedX = Number(remot_speedX);
                    PlayerRemot.speedY = Number(remot_speedY);
                    lastRemoteUpdate = Date.now(); // Actualitza el temps només si hi ha moviment
                } else {
                    // Si el servidor ens diu que la velocitat és 0 (aturada), l'apliquem immediatament
                    PlayerRemot.speedX = 0;
                    PlayerRemot.speedY = 0;
                }
            }

            // 2. Mecanisme de Seguretat (Per la desincronització de l'aturada)
            const MAX_INACTIVITY_MS = 2 * 200; // Utilitzem 200ms com a referència conservadora de latència
            if (Date.now() - lastRemoteUpdate > MAX_INACTIVITY_MS) {
                // Si el jugador remot s'ha mogut abans, forcem l'aturada per evitar la desincronització
                PlayerRemot.speedX = 0;
                PlayerRemot.speedY = 0;
            }
      
      // Círculo desde servidor
      if (joc.circle_x !== null && joc.circle_y !== null) {
        circle.x = Number(joc.circle_x);
        circle.y = Number(joc.circle_y);
        circle.visible = true;
      } else {
        // El servidor diu que NO hi ha cercle. Això vol dir que ha desaparegut/marcat.
        
        if (circle.visible) {
            // El cercle era visible localment i ara el servidor diu que no ho és.
            // Això implica que l'altre jugador acaba de marcar un punt.
            // FORCEM l'actualització de la puntuació.
            sincronitzarPuntuacioRemota(); 
        }
        circle.visible = false;
      }

    })
    .catch(console.error);

}


// game.js (Nova Funció per al Jugador Remot)
function sincronitzarPuntuacioRemota() {
    fetch(`game.php?action=status&game_id=${idJoc}`, { method: 'GET', cache: 'no-store' })
        .then(response => response.json())
        .then(joc => {
            // L'acció status no conté la puntuació, hauríem de fer una acció nova o reintroduir-la a status.
            // DONAT L'ESTAT ACTUAL DEL CODI, EL MÉS RÀPID ÉS CREAR UNA FUNCIÓ NOVA A PHP
            // o reintroduir-la a status, però usant una acció addicional és més net.

            // Utilitzant l'acció add_point amb una petita modificació per només llegir:
             fetch('game.php?action=status_points&game_id='+idJoc, { method: 'GET', cache: 'no-store' })
                .then(r => r.json())
                .then(data => {
                    if (data.p1_points != null) p1_points = Number(data.p1_points);
                    if (data.p2_points != null) p2_points = Number(data.p2_points);
                    document.getElementById("p1_score").innerText = p1_points;
                    document.getElementById("p2_score").innerText = p2_points;
                });
        })
        .catch(console.error);
}



// game.js (Fragment de la funció enviarMovimentSiCambio)

// game.js (Fragment de la funció enviarMovimentSiCambio)
function enviarMovimentSiCambio() {
  if (!idJoc || !numJugador) return;

  // Llegeix les velocitats del jugador local
  const px_speed = numJugador === 1 ? Player1.speedX : Player2.speedX;
  const py_speed = numJugador === 1 ? Player1.speedY : Player2.speedY;

  // Només envia si la velocitat (estat d'entrada) ha canviat RESPECTE L'ÚLTIM ENVIAMENT
  if (lastSentX === null || px_speed !== lastSentX || py_speed !== lastSentY) {
    lastSentX = px_speed; // Ara guarda la velocitat
    lastSentY = py_speed; // Ara guarda la velocitat

    const body = new URLSearchParams();
    body.set('game_id', idJoc);
    body.set('player_speed_x', String(px_speed)); // <-- Envia la velocitat
    body.set('player_speed_y', String(py_speed)); // <-- Envia la velocitat

    fetch('game.php?action=movement', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body,
      cache: 'no-store'
    })
      .then(r => r.json())
      .then(data => {
        if (data.error) console.warn(data.error);
      })
      .catch(console.error);
  }
}

// game.js (Gestor keydown - CORREGIT)
document.addEventListener("keydown", function (event) {
    // Caldrà comprovar que la tecla no estigui ja premuda per evitar múltiples missatges
    // Simplificant, enviem SEMPRE en keydown si la velocitat canvia.
    if (!numJugador) return; 
    
    /*
    // 1. Activar Polling Ràpid i Reiniciar Temporitzador
    setPollingSpeed(POLLING_FAST_MS); 
    if (pollingTimeout) clearTimeout(pollingTimeout);
    
    // 2. Programar el retorn a l'estat lent
    pollingTimeout = setTimeout(() => {
        setPollingSpeed(POLLING_SLOW_MS);
    }, FAST_POLLING_DURATION);

    // Aquí podem afegir una comprovació per evitar processar tecles ja premudes 
    // si no vols que el navegador repeteixi l'acció amb keydown.
    */
    switch (event.key.toLowerCase()) {
        case "w": moveup(); break;
        case "a": moveleft(); break;
        case "s": movedown(); break;
        case "d": moveright(); break;
        default: return; // No processar altres tecles
    }
    enviarMovimentSiCambio();
});

// game.js (Gestor keyup - CORREGIT)
document.addEventListener("keyup", function (event) {
  
   if (!numJugador) return; 
    /*
    // 1. Activar Polling Ràpid per enviar l'estat d'aturada
    setPollingSpeed(POLLING_FAST_MS); 
    if (pollingTimeout) clearTimeout(pollingTimeout);
    
    // 2. Programar el retorn a l'estat lent
    pollingTimeout = setTimeout(() => {
        setPollingSpeed(POLLING_SLOW_MS);
    }, FAST_POLLING_DURATION);
    */
    let player = numJugador === 1 ? Player1 : Player2;
    
    switch (event.key.toLowerCase()) {
        case "w": 
        case "s": 
            player.speedY = 0; // Atura la velocitat vertical
            break;
        case "a": 
        case "d": 
            player.speedX = 0; // Atura la velocitat horitzontal
            break;
        default: return;
    }
    // Envia l'estat de velocitat = 0 (el missatge d'aturada)
    enviarMovimentSiCambio(); 
});

// --- Movimiento local ---
function moveup() {
  let player = numJugador === 1 ? Player1 : Player2;
  player.speedY = -MAX_SPEED; 
}
function movedown() {
  let player = numJugador === 1 ? Player1 : Player2;
  player.speedY = MAX_SPEED;
}
function moveleft() {
  let player = numJugador === 1 ? Player1 : Player2;
  player.speedX = -MAX_SPEED;
}
function moveright() {
  let player = numJugador === 1 ? Player1 : Player2;
  player.speedX = MAX_SPEED;
}

// --- Círculo ---
function createCircleLocal() {
  const radius = 15;
  const x = Math.random() * (myGameArea.canvas.width - 2 * radius) + radius;
  const y = Math.random() * (myGameArea.canvas.height - 2 * radius) + radius;
  circle = { x, y, radius, visible: true };
}

// Sólo J1 crea y sincroniza
function createCircleAndSync() {
  createCircleLocal();
  if (!idJoc) return;

  const body = new URLSearchParams();
  body.set('game_id', idJoc);
  body.set('circle_x', String(Math.round(circle.x)));
  body.set('circle_y', String(Math.round(circle.y)));

  fetch('game.php?action=actualizarCirculo', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
    cache: 'no-store'
  })
    .then(r => r.json())
    .then(data => {
      if (data.error) console.warn(data.error);
    })
    .catch(console.error);
}

function enviarPuntoAlServidor() {
  if (!idJoc) return;

  const body = new URLSearchParams();
  body.set('game_id', idJoc);

  fetch('game.php?action=add_point', {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
    cache: 'no-store'
  })
    .then(r => r.json())
    .then(data => {
      if (data.error) {
        console.warn(data.error);
        return;
      }
      // Actualitzar puntuacions locals des del servidor (FET PER ESDEVENIMENT)
      if (data.p1_points != null) p1_points = Number(data.p1_points);
      if (data.p2_points != null) p2_points = Number(data.p2_points);

      document.getElementById("p1_score").innerText = p1_points;
      document.getElementById("p2_score").innerText = p2_points;
    })
    .catch(console.error);
}
function drawCircle() {
  if (circle && circle.visible) {
    const ctx = myGameArea.context;
    ctx.beginPath();
    ctx.arc(circle.x, circle.y, circle.radius, 0, 2 * Math.PI);
    ctx.fillStyle = "black";
    ctx.fill();
  }
}

function checkCollision(player) {
  if (!circle.visible) return false;
  const playerCenterX = player.x + player.width / 2;
  const playerCenterY = player.y + player.height / 2;
  const dx = playerCenterX - circle.x;
  const dy = playerCenterY - circle.y;
  const distance = Math.sqrt(dx * dx + dy * dy);
  return distance < (circle.radius + Math.max(player.width, player.height) / 2);
}


function addNetStatsLabel() {
  const lbl = document.createElement('div');
  lbl.id = 'net_stats';
  lbl.style.position = 'absolute';
  lbl.style.bottom = '20px';
  lbl.style.left = '5%';
  lbl.style.transform = 'translateX(-50%)';
  lbl.style.padding = '4px 8px';
  lbl.style.background = 'rgba(255,255,255,0.8)';
  lbl.style.border = '1px solid #ddd';
  lbl.style.font = '12px Arial, sans-serif';
  lbl.style.borderRadius = '6px';
  lbl.textContent = 'RTT: — ms';
  document.body.appendChild(lbl);
}


let lastRttMs = null;
let rttEma = null; // media exponencial para suavizar (opcional)

function startLatencyMonitor() {
  setInterval(() => {
    const t0 = performance.now();
    // cache-busting con ts y no-store
    fetch(`game.php?action=ping&ts=${Date.now()}`, { method: 'GET', cache: 'no-store' })
      .then(r => r.json())
      .then(() => {
        const rtt = Math.round(performance.now() - t0);
        lastRttMs = rtt;
        // EMA con alpha=0.3 para suavizar, opcional
        rttEma = (rttEma == null) ? rtt : Math.round(0.3 * rtt + 0.7 * rttEma);
        const label = document.getElementById('net_stats');
        if (label) label.textContent = `RTT: ${rtt} ms (avg: ${rttEma} ms)`;
      })
      .catch(() => {
        const label = document.getElementById('net_stats');
        if (label) label.textContent = `RTT: error`;
      });
  }, 1000);
}