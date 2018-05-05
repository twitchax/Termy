const fs = require('fs')
const path = require('path');
const express = require('express');
const http = require('http');
const https = require('https');
const os = require('os');
const pty = require('node-pty');
const expressWs = require('express-ws')

const port = process.env.PORT || 80;
const password = process.env.PASSWORD || 'null';
const shell = process.env.SHELL || 'bash';

const app = express();

var server;

if(port == 443) {
    var options = {
        pfx: fs.readFileSync('/etc/secrets/cert.pfx'),
        passphrase: fs.readFileSync('/etc/secrets/certpw', 'utf8'), 
    };
    server = https.createServer(options, app);
} else {
    server = http.createServer(app);
}

server.timeout = 300000;  

// Allows websockets.
let expressWsInstance = expressWs(app, server);

let terminals = {},
    logs = {};

let validatePassword = function (req, res, next) {
    if (password === 'null' || req.query.p == password) {
        next();
    } else {
        res.sendStatus(401);
    }
};

app.use('/xterm', express.static(path.join(__dirname, 'node_modules/xterm/dist')));

app.get('/', validatePassword, function (req, res) {
    res.sendFile(path.join(__dirname, '/index.html'));
});

app.get('/style.css', function (req, res) {
    res.sendFile(path.join(__dirname, '/style.css'));
});

app.get('/main.js', function (req, res) {
    res.sendFile(path.join(__dirname, '/main.js'));
});

app.post('/terminals', validatePassword, function (req, res) {
    var cols = parseInt(req.query.cols),
        rows = parseInt(req.query.rows),
        term = pty.spawn(shell, [], {
            name: 'xterm-color',
            cols: cols || 80,
            rows: rows || 24,
            cwd: process.env.PWD,
            env: process.env
        });

    console.log('Created terminal with PID: ' + term.pid);
    terminals[term.pid] = term;
    logs[term.pid] = '';
    term.on('data', function (data) {
        logs[term.pid] += data;
    });
    res.send(term.pid.toString());
    res.end();
});

app.post('/terminals/:pid/size', validatePassword, function (req, res) {
    var pid = parseInt(req.params.pid),
        cols = parseInt(req.query.cols),
        rows = parseInt(req.query.rows),
        term = terminals[pid];

    term.resize(cols, rows);
    console.log('Resized terminal ' + pid + ' to ' + cols + ' cols and ' + rows + ' rows.');
    res.end();
});

app.ws('/terminals/:pid', function (ws, req) {

    if(password !== 'null' && req.query.p !== password)
        ws.close();

    var term = terminals[parseInt(req.params.pid)];
    console.log('Connected to terminal ' + term.pid);
    ws.send(logs[term.pid]);

    term.on('data', function (data) {
        try {
            ws.send(data);
        } catch (ex) {
            // The WebSocket is not open, ignore
        }
    });
    ws.on('message', function (msg) {
        term.write(msg);
    });
    ws.on('close', function () {
        term.kill();
        console.log('Closed terminal ' + term.pid);
        // Clean things up
        delete terminals[term.pid];
        delete logs[term.pid];
    });
});

server.listen(port, function () {
    console.log("Express server listening on port " + port);
});