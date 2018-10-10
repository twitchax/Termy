Terminal.applyAddon(attach);
Terminal.applyAddon(fit);
Terminal.applyAddon(fullscreen);
Terminal.applyAddon(search);
Terminal.applyAddon(webLinks);

var term,
    protocol,
    socketURL,
    socket,
    pid;

var terminalContainer = document.getElementById('terminal-container');
var password = new URLSearchParams(window.location.search).get('p') || 'null';

createTerminal();

function createTerminal() {
    // Clean terminal
    while (terminalContainer.children.length) {
        terminalContainer.removeChild(terminalContainer.children[0]);
    }
    term = new Terminal({
        cursorBlink: true
    });
    term.on('resize', function(size) {
        if (!pid) {
            return;
        }
        var cols = size.cols,
            rows = size.rows,
            url = '/terminals/' + pid + '/size?cols=' + cols + '&rows=' + rows + '&p=' + password;

        fetch(url, {
            method: 'POST'
        });
    });
    protocol = (location.protocol === 'https:') ? 'wss://' : 'ws://';
    socketURL = protocol + location.hostname + ((location.port) ? (':' + location.port) : '') + '/terminals/';

    term.open(terminalContainer, true);
    term.fit();

    setTimeout(() => {
        cols = term.cols;
        rows = term.rows;
        fetch('/terminals?cols=' + cols + '&rows=' + rows + '&p=' + password, {
            method: 'POST'
        }).then(function(res) {
            res.text().then(function(pid) {
                window.pid = pid;
                socketURL += pid;
                socketURL += '/?p=' + password;
                socket = new WebSocket(socketURL);
                socket.onopen = runRealTerminal;
            });
        });
    }, 0);
}

function runRealTerminal() {
    term.attach(socket);
    term._initialized = true;
}