#!/bin/bash

set -e

# 1. Get certbot (https://certbot.eff.org/#pip-other).

# 2. Run in manual mode with DNS challenge (https://certbot.eff.org/docs/using.html#manual).

sudo ./certbot-auto certonly --manual --preferred-challenges dns --server https://acme-v02.api.letsencrypt.org/directory

# 3. Put the challenge in the DNS Zones File as a TXT from root.

sudo openssl pkcs12 -export -out .hidden/cert.pfx -inkey /etc/letsencrypt/live/termy.in/privkey.pem -in /etc/letsencrypt/live/termy.in/cert.pem -certfile /etc/letsencrypt/live/termy.in/fullchain.pem

# 4. Put the PFX, and the password, in the k8s cluster as a secret.