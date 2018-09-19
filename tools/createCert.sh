#!/bin/bash

set -e

# 1. Get certbot (https://certbot.eff.org/#pip-other).

# 2. Run in manual mode with DNS challenge (https://certbot.eff.org/docs/using.html#manual).

sudo ./certbot-auto certonly --manual --preferred-challenges dns --server https://acme-v02.api.letsencrypt.org/directory

# 3: Put the DNS names in: termy.in, *.termy.in.

# 4. Put the challenge in the DNS Zones File as a TXT from root.

sudo cat /etc/letsencrypt/live/termy.in-0001/fullchain.pem > .hidden/tls.crt
sudo cat /etc/letsencrypt/live/termy.in-0001/privkey.pem > .hidden/tls.key

# 5. Put the PFX, and the password, in the k8s cluster as a secret.